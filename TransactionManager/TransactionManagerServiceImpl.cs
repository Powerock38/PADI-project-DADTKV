using Dadtkv;
using Grpc.Core;
using Management;

namespace TransactionManager;

public class TransactionManagerServiceImpl : TransactionManagerService.TransactionManagerServiceBase
{
    private readonly List<TransactionManagerStruct> transactionManagers;
    private readonly List<LeaseManagerStruct> leaseManagers;

    public TransactionManagerServiceImpl(string name, List<TransactionManagerStruct> transactionManagers,
        List<LeaseManagerStruct> leaseManagers)
    {
        this.name = name;
        this.transactionManagers = transactionManagers;
        this.leaseManagers = leaseManagers;
    }

    private readonly List<DadInt> dadInts = new();

    private readonly string name;

    private readonly List<Lease> leases = new();

    private List<string> getMyLeases()
    {
        return leases.Where(l => l.TransactionManagerId == name).SelectMany(l => l.Dadints).ToList();
    }

    public override async Task<TransactionResponse> ExecuteTransaction(TransactionRequest request,
        ServerCallContext context)
    {
        Console.WriteLine($"Received transaction request from {request.ClientId}: read {DadIntUtils.DadIntsKeysToString(request.ReadDadints)} | write {DadIntUtils.DadIntsToString(request.WriteDadints)}");

        // For each dadints asked (read or write), we check if TM has a lease for it. If not, TM asks for a lease.
        List<string> dadIntKeysToCheckLeases =
            request.ReadDadints.Concat(request.WriteDadints.Select(d => d.Key)).ToList();

        List<string> myLeases = getMyLeases();

        List<string> dadIntKeysToRequestLeases = dadIntKeysToCheckLeases.Where(key => !myLeases.Contains(key)).ToList();

        // If we need to request leases, we ask for them
        if (dadIntKeysToRequestLeases.Count > 0)
        {
            LeaseRequest leaseRequest = new LeaseRequest();
            leaseRequest.RequestedDadints.AddRange(dadIntKeysToRequestLeases);

            Console.WriteLine("Requesting lease for: " + DadIntUtils.DadIntsKeysToString(dadIntKeysToRequestLeases));

            await RequestLeases(leaseRequest);
        }

        // Build the response
        TransactionResponse response = new TransactionResponse();
        response.ReadValues.AddRange(dadInts.Where(d => request.ReadDadints.Contains(d.Key)));

        // Update local dadints database (from request.WriteDadints)
        List<DadInt> dadIntsToBroadcast = new();
        foreach (DadInt dadInt in request.WriteDadints)
        {
            var index = dadInts.FindIndex(d => d.Key == dadInt.Key);
            if (index != -1)
            {
                dadInts[index].Value = dadInt.Value;
                dadIntsToBroadcast.Add(dadInts[index]);
            }
            else
            {
                dadInts.Add(dadInt);
                dadIntsToBroadcast.Add(dadInt);
            }
        }

        // Broadcast new or edited dadints (from request.WriteDadints) to all other TMs
        if (dadIntsToBroadcast.Count > 0)
        {
            foreach (var tm in transactionManagers.Where(tm => tm.name != name))
            {
                Console.WriteLine("Broadcasting dadints to " + tm.name);
                tm.service!.BroadcastDadIntsAsync(new BroadcastDadIntsMsg { Dadints = { dadIntsToBroadcast } });
            }
        }

        return response;
    }

    private Task RequestLeases(LeaseRequest request)
    {
        //TODO: implement
        return Task.CompletedTask;
    }

    public override Task<BroadcastDadIntsAck> BroadcastDadInts(BroadcastDadIntsMsg msg, ServerCallContext context)
    {
        Console.WriteLine("Received broadcast dadints: " + DadIntUtils.DadIntsToString(msg.Dadints));

        dadInts.AddRange(msg.Dadints);

        return Task.FromResult(new BroadcastDadIntsAck { Ok = true });
    }
}