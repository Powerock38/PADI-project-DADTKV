using Dadtkv;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Management;

namespace LeaseManager;

public class LeaseManagerServiceImpl : LeaseManagerService.LeaseManagerServiceBase
{
    private const bool paxosLogEnabled = false;

    private static void PaxosLog(string message)
    {
        if (paxosLogEnabled)
        {
            Console.WriteLine($"[PAXOS] {message}");
        }
    }

    private readonly ConfigReader config;
    private readonly string name;

    private LeaseDB leases = new();
    private readonly LeaseDB currentSlotLeaseRequests = new();

    // Paxos
    private readonly int paxosOrder;

    // Proposer
    private PaxosProposal? paxosProposal;
    private int paxosNextEpoch;
    private readonly List<string> paxosPromisesReceivedFrom = new(); // list of lease manager names
    private int? paxosPreviousAcceptedEpoch;
    private LeaseDB? paxosProposedValue;

    // Acceptor
    private int? paxosPromisedEpoch;
    private int? paxosAcceptedEpoch;
    private LeaseDB? paxosAcceptedValue;

    public LeaseManagerServiceImpl(string name, ConfigReader config)
    {
        this.name = name;
        this.config = config;

        paxosOrder = config.leaseManagers.FindIndex(lm => lm.name == name);
        PaxosLog($"order = {paxosOrder}");
    }

    private uint GetQuorumSize()
    {
        // Minus 1 because we don't count ourselves
        int count = config.leaseManagers.Count - config.GetWhoISuspect(name).Count - 1;
        return (uint)Math.Max(Math.Ceiling(count * 0.5), 0);
    }

    public override Task<Empty> RequestLeases(Lease request, ServerCallContext context)
    {
        currentSlotLeaseRequests.Add(request);

        Console.WriteLine(
            $"Received lease request from {request.TransactionManagerId}. Current slot requests: {currentSlotLeaseRequests}");

        return Task.FromResult(new Empty());
    }

    public void ProcessLeaseRequests(uint slot)
    {
        // Check if we are not already in a paxos instance
        if (paxosProposedValue != null)
        {
            return;
        }

        if (currentSlotLeaseRequests.IsEmpty())
        {
            return;
        }

        // Check if I am the leader
        if (slot % config.leaseManagers.Count != paxosOrder)
        {
            return;
        }

        Console.WriteLine($"I am the leader for slot {slot}");

        // propose the absolute list to a new instance of the Paxos algorithm
        paxosProposedValue = new LeaseDB();
        paxosProposedValue.AddRange(leases);
        paxosProposedValue.AddRange(currentSlotLeaseRequests);

        PaxosPrepare();
    }

    private void PaxosPrepare()
    {
        paxosPromisesReceivedFrom.Clear();
        paxosProposal = new PaxosProposal
        {
            Epoch = paxosNextEpoch,
            LeaseManagerId = name
        };

        PaxosLog($"Sending Prepare, epoch={paxosProposal.Epoch}");

        paxosNextEpoch++;

        // Broadcast prepare to all other lease managers
        foreach (var lm in config.leaseManagers.Where(lm => lm.name != name))
        {
            lm.GetService().ReceivePrepareAsync(paxosProposal);
        }
    }

    public override Task<Empty> ReceivePrepare(PaxosProposal newProposal, ServerCallContext context)
    {
        if (newProposal.Epoch >= paxosPromisedEpoch || paxosPromisedEpoch == null)
        {
            PaxosLog($"ReceivePrepare, epoch={newProposal.Epoch}, my promised epoch is {paxosPromisedEpoch}");

            if (paxosPromisedEpoch == null || newProposal.Epoch > paxosPromisedEpoch)
            {
                paxosPromisedEpoch = newProposal.Epoch;
            }

            // Send promise to proposer
            var lm = config.leaseManagers.First(lm => lm.name == newProposal.LeaseManagerId);

            PaxosLog($"sending promise to {lm.name}");
            lm.GetService().ReceivePromise(new PaxosPromise
            {
                LeaseManagerId = name,
                Epoch = newProposal.Epoch,
                PreviousEpoch = paxosAcceptedEpoch ?? -1,
                PreviousAcceptedValue = paxosAcceptedValue?.IntoGRPC(),
            });
        }
        else
        {
            PaxosLog($"ReceivePrepare OUTDATED, epoch={newProposal.Epoch}, my promised epoch is {paxosPromisedEpoch}");
        }

        return Task.FromResult(new Empty());
    }

    public override Task<Empty> ReceivePromise(PaxosPromise promise, ServerCallContext context)
    {
        PaxosLog("ReceivePromise");

        if (promise.Epoch != paxosProposal?.Epoch ||
            paxosPromisesReceivedFrom.Contains(promise.LeaseManagerId))
        {
            PaxosLog("ReceivePromise OUTDATED");
            return Task.FromResult(new Empty());
        }

        paxosPromisesReceivedFrom.Add(promise.LeaseManagerId);

        uint quorum = GetQuorumSize();

        PaxosLog(
            $"ReceivePromise, epoch={promise.Epoch}, quorum is {quorum}, responses={string.Join(", ", paxosPromisesReceivedFrom)}");

        if (promise.PreviousEpoch > paxosPreviousAcceptedEpoch)
        {
            paxosPreviousAcceptedEpoch = promise.PreviousEpoch;

            if (promise.PreviousAcceptedValue != null)
            {
                paxosProposedValue = LeaseDB.FromGRPC(promise.PreviousAcceptedValue);
            }
        }

        if (paxosPromisesReceivedFrom.Count == quorum)
        {
            if (paxosProposedValue != null)
            {
                PaxosLog($"ReceivePromise: Broadcasting Accept: {paxosProposedValue}");
                // Broadcast accept! to ALL lease managers, even itself
                foreach (var lm in config.leaseManagers)
                {
                    lm.GetService().ReceiveAcceptAsync(new PaxosAccept
                    {
                        Epoch = paxosProposal.Epoch,
                        AcceptedValue = paxosProposedValue.IntoGRPC()
                    });
                }

                // Ready to start a new round!
                paxosProposedValue = null;
            }
        }

        return Task.FromResult(new Empty());
    }

    public override Task<Empty> ReceiveAccept(PaxosAccept accept, ServerCallContext context)
    {
        if (accept.Epoch >= paxosPromisedEpoch)
        {
            paxosPromisedEpoch = accept.Epoch;
            paxosAcceptedEpoch = accept.Epoch;
            paxosAcceptedValue = LeaseDB.FromGRPC(accept.AcceptedValue);

            PaxosLog($"ReceiveAccept, epoch={accept.Epoch}, accepting {paxosAcceptedValue}");

            // Broadcast accepted to all transaction managers (paxos learners)
            foreach (var tm in config.transactionManagers)
            {
                tm.GetService().ReceiveAcceptedAsync(accept);
            }

            // Ready to start a new round!
            currentSlotLeaseRequests.Clear();
            leases = paxosAcceptedValue;
            paxosAcceptedValue = null;
        }

        return Task.FromResult(new Empty());
    }
}