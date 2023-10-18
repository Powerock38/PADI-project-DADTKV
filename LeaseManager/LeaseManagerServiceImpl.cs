using Dadtkv;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Management;

namespace LeaseManager;

public class LeaseManagerServiceImpl : LeaseManagerService.LeaseManagerServiceBase
{
    private readonly List<TransactionManagerStruct> transactionManagers;
    private readonly List<LeaseManagerStruct> leaseManagers;
    private readonly string name;

    private readonly List<Lease> currentSlotLeaseRequests = new();

    // Paxos
    private readonly int paxosOrder;

    // Proposer
    private PaxosProposal? paxosProposal;
    private int paxosNextProposalId = 0;
    private readonly List<string> paxosPromisesReceivedFrom = new();
    private int? paxosLastAcceptedId;
    private readonly List<Lease> paxosProposedValue;

    // Acceptor
    private int? paxosPromisedId;
    private int? paxosAcceptedId;
    private readonly List<Lease> paxosAcceptedValue;

    public LeaseManagerServiceImpl(string name, List<TransactionManagerStruct> transactionManagers,
        List<LeaseManagerStruct> leaseManagers)
    {
        this.name = name;
        this.transactionManagers = transactionManagers;
        this.leaseManagers = leaseManagers;

        paxosPromisedId = null;
        paxosProposedValue = new List<Lease>();
        paxosAcceptedValue = new List<Lease>();
        paxosOrder = leaseManagers.FindIndex(lm => lm.name == name);

        Console.WriteLine($"Paxos order = {paxosOrder}");
    }

    public override Task<Empty> RequestLeases(Lease request, ServerCallContext context)
    {
        Console.WriteLine($"Received lease request from {request.TransactionManagerId}");

        currentSlotLeaseRequests.Add(request);

        return Task.FromResult(new Empty());
    }

    public void ProcessLeaseRequests(uint slot)
    {
        // Check if we are not already in a paxos instance
        if (paxosProposedValue.Count > 0)
        {
            return;
        }

        /*When a new epoch i begins, a lease manager orders determin-
           istically any new lease request received since the start of epoch i − 1 and proposes this
           list to a new instance of the Paxos algorithm.*/

        // Check if I am the leader
        if (slot % leaseManagers.Count != paxosOrder)
        {
            Console.WriteLine($"I am not the leader for slot {slot}");
            return;
        }

        Console.WriteLine($"I am the leader for slot {slot}");

        //TODO Order the requests

        // propose the list to a new instance of the Paxos algorithm

        paxosProposedValue.Clear();
        paxosProposedValue.AddRange(currentSlotLeaseRequests);

        PaxosPrepare();
    }

    private int GetQorumSize()
    {
        //TODO handle down nodes
        return leaseManagers.Count / 2;
    }

    private void PaxosPrepare()
    {
        paxosPromisesReceivedFrom.Clear();
        paxosProposal = new PaxosProposal
        {
            ProposalId = paxosNextProposalId,
            LeaseManagerId = name
        };
        paxosNextProposalId++;

        // Broadcast prepare to all other lease managers
        foreach (var lm in leaseManagers.Where(lm => lm.name != name))
        {
            // TODO: use async or sync?
            lm.service!.ReceivePrepareAsync(paxosProposal);
        }
    }

    public override Task<Empty> ReceivePrepare(PaxosProposal newProposal, ServerCallContext context)
    {
        if (newProposal.ProposalId == paxosPromisedId)
        {
            // Send promise to proposer
            var lm = leaseManagers.First(lm => lm.name == newProposal.LeaseManagerId);
            var promise = new PaxosPromise
            {
                Proposal = newProposal,
                PreviousAcceptedId = paxosAcceptedId!.Value,
            };
            promise.PreviousAcceptedValue.AddRange(paxosAcceptedValue);
            lm.service!.ReceivePromiseAsync(promise);
        }
        else if (newProposal.ProposalId > paxosPromisedId)
        {
            paxosPromisedId = newProposal.ProposalId;

            // Send promise to proposer
            var lm = leaseManagers.First(lm => lm.name == newProposal.LeaseManagerId);
            var promise = new PaxosPromise
            {
                Proposal = newProposal,
                PreviousAcceptedId = paxosAcceptedId!.Value,
            };
            promise.PreviousAcceptedValue.AddRange(paxosAcceptedValue);
            lm.service!.ReceivePromiseAsync(promise);
        }

        return Task.FromResult(new Empty());
    }

    public override Task<Empty> ReceivePromise(PaxosPromise promise, ServerCallContext context)
    {
        if (promise.Proposal.ProposalId != paxosProposal?.ProposalId ||
            paxosPromisesReceivedFrom.Contains(promise.Proposal.LeaseManagerId))
        {
            return Task.FromResult(new Empty());
        }

        paxosPromisesReceivedFrom.Add(promise.Proposal.LeaseManagerId);

        if (promise.PreviousAcceptedId > paxosLastAcceptedId)
        {
            paxosLastAcceptedId = promise.PreviousAcceptedId;

            if (promise.PreviousAcceptedValue != null)
            {
                paxosProposedValue.Clear();
                paxosProposedValue.AddRange(promise.PreviousAcceptedValue);
            }
        }

        if (paxosPromisesReceivedFrom.Count == GetQorumSize())
        {
            if (paxosProposedValue.Count > 0)
            {
                // Broadcast accept! to ALL lease managers, even itself
                foreach (var lm in leaseManagers)
                {
                    // TODO: use async or sync?
                    var accept = new PaxosAccept
                    {
                        ProposalId = paxosProposal.ProposalId,
                    };
                    accept.AcceptedValue.AddRange(paxosProposedValue);
                    lm.service!.ReceiveAcceptAsync(accept);
                }
            }
        }

        return Task.FromResult(new Empty());
    }

    public override Task<Empty> ReceiveAccept(PaxosAccept accept, ServerCallContext context)
    {
        if (accept.ProposalId >= paxosPromisedId)
        {
            paxosPromisedId = accept.ProposalId;
            paxosAcceptedId = accept.ProposalId;

            paxosAcceptedValue.Clear();
            paxosAcceptedValue.AddRange(accept.AcceptedValue);

            // Broadcast accepted to all transaction managers
            foreach (var tm in transactionManagers)
            {
                tm.service!.ReceiveAcceptedAsync(accept);
            }
            
            // Ready to start a new round!
            currentSlotLeaseRequests.Clear();
        }

        return Task.FromResult(new Empty());
    }
}