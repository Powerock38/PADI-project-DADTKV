using Dadtkv;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Management;

namespace LeaseManager;

public class LeaseManagerServiceImpl : LeaseManagerService.LeaseManagerServiceBase
{
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

        Console.WriteLine($"Paxos order = {paxosOrder}");
    }
    
    private uint GetQuorumSize()
    {
        // TODO minus 1 because we don't count ourselves?
        int count = config.leaseManagers.Count - config.GetWhoISuspect(name).Count - 1;
        return (uint)Math.Ceiling(count * 0.5);
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
            Console.WriteLine("Already in a paxos instance");
            return;
        }

        if (currentSlotLeaseRequests.IsEmpty())
        {
            Console.WriteLine("Not lease request to process");
            return;
        }

        // Check if I am the leader
        if (slot % config.leaseManagers.Count != paxosOrder)
        {
            Console.WriteLine($"I am not the leader for slot {slot}");
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

        Console.WriteLine($"PAXOS Sending Prepare, epoch={paxosProposal.Epoch}");

        paxosNextEpoch++;

        // Broadcast prepare to all other lease managers
        foreach (var lm in config.leaseManagers.Where(lm => lm.name != name))
        {
            lm.service!.ReceivePrepareAsync(paxosProposal);
        }
    }

    public override Task<Empty> ReceivePrepare(PaxosProposal newProposal, ServerCallContext context)
    {
        if (newProposal.Epoch >= paxosPromisedEpoch || paxosPromisedEpoch == null)
        {
            Console.WriteLine(
                $"PAXOS ReceivePrepare, epoch={newProposal.Epoch}, my promised epoch is {paxosPromisedEpoch}");

            if (paxosPromisedEpoch == null || newProposal.Epoch > paxosPromisedEpoch)
            {
                paxosPromisedEpoch = newProposal.Epoch;
            }

            // Send promise to proposer
            var lm = config.leaseManagers.First(lm => lm.name == newProposal.LeaseManagerId);

            Console.WriteLine($"PAXOS sending promise to {lm.name}");
            lm.service!.ReceivePromise(new PaxosPromise
            {
                LeaseManagerId = name,
                Epoch = newProposal.Epoch,
                PreviousEpoch = paxosAcceptedEpoch ?? -1,
                PreviousAcceptedValue = paxosAcceptedValue?.IntoGRPC(),
            });
        }
        else
        {
            Console.WriteLine(
                $"PAXOS ReceivePrepare OUTDATED, epoch={newProposal.Epoch}, my promised epoch is {paxosPromisedEpoch}");
        }

        return Task.FromResult(new Empty());
    }

    public override Task<Empty> ReceivePromise(PaxosPromise promise, ServerCallContext context)
    {
        Console.WriteLine("PAXOS ReceivePromise");

        if (promise.Epoch != paxosProposal?.Epoch ||
            paxosPromisesReceivedFrom.Contains(promise.LeaseManagerId))
        {
            Console.WriteLine("PAXOS ReceivePromise OUTDATED");
            return Task.FromResult(new Empty());
        }

        paxosPromisesReceivedFrom.Add(promise.LeaseManagerId);

        uint quorum = GetQuorumSize();

        Console.WriteLine(
            $"PAXOS ReceivePromise, epoch={promise.Epoch}, quorum is {quorum}, responses={string.Join(", ", paxosPromisesReceivedFrom)}");

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
                Console.WriteLine($"PAXOS ReceivePromise: Broadcasting Accept: {paxosProposedValue}");
                // Broadcast accept! to ALL lease managers, even itself
                foreach (var lm in config.leaseManagers)
                {
                    lm.service!.ReceiveAcceptAsync(new PaxosAccept
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

            Console.WriteLine($"PAXOS ReceiveAccept, epoch={accept.Epoch}, accepting {paxosAcceptedValue}");

            // Broadcast accepted to all transaction managers
            foreach (var tm in config.transactionManagers)
            {
                tm.service!.ReceiveAcceptedAsync(accept);
            }

            // Ready to start a new round!
            //TODO: what about the requests that arrived while we were in the paxos instance?
            currentSlotLeaseRequests.Clear();
            leases = paxosAcceptedValue;
            paxosAcceptedValue = null;
        }

        return Task.FromResult(new Empty());
    }
}