using Dadtkv;
using Google.Protobuf.WellKnownTypes;
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

    private readonly Dictionary<string, int> dadInts = new();

    private readonly string name;

    private readonly List<Lease> leases = new();

    private List<string> getMyLeases()
    {
        return leases.Where(l => l.TransactionManagerId == name).SelectMany(l => l.Dadints).ToList();
    }

    public override Task<TransactionResponse> ExecuteTransaction(TransactionRequest request,
        ServerCallContext context)
    {
        List<string> requestReadDadInts = request.ReadDadints.Where(key => !string.IsNullOrEmpty(key)).ToList();

        Console.WriteLine(
            $"Received transaction request from {request.ClientId}: read {DadIntUtils.DadIntsKeysToString(requestReadDadInts)} | write {DadIntUtils.DadIntsToString(request.WriteDadints)}");

        Console.WriteLine($"MY COLLECTION = {DadIntUtils.DadIntsDictionnaryToString(dadInts)}");

        // For each dadints asked (read or write), we check if TM has a lease for it. If not, TM asks for a lease.
        List<string> dadIntKeysToCheckLeases =
            requestReadDadInts.Concat(request.WriteDadints.Select(d => d.Key)).ToList();

        List<string> myLeases = getMyLeases();

        List<string> dadIntKeysToRequestLeases = dadIntKeysToCheckLeases.Where(key => !myLeases.Contains(key)).ToList();

        // If we need to request leases, we ask for them
        if (dadIntKeysToRequestLeases.Count > 0)
        {
            Lease leaseRequest = new();
            leaseRequest.Dadints.AddRange(dadIntKeysToRequestLeases);

            Console.WriteLine("Requesting lease for: " + DadIntUtils.DadIntsKeysToString(dadIntKeysToRequestLeases));

            foreach (var lm in leaseManagers)
            {
                lm.service!.RequestLeases(leaseRequest);
            }
        }

        // Build the response
        TransactionResponse response = new TransactionResponse();

        foreach (string key in requestReadDadInts)
        {
            if (dadInts.TryGetValue(key, out var val))
            {
                response.ReadValues.Add(
                    new DadInt
                    {
                        Key = key,
                        Value = val
                    }
                );
            }
            else
            {
                Console.WriteLine($"ERROR: {key} not found in dadints");
            }
        }

        // Update local dadints database (from request.WriteDadints)
        List<DadInt> dadIntsToBroadcast = new();
        foreach (DadInt dadInt in request.WriteDadints)
        {
            dadInts[dadInt.Key] = dadInt.Value;
            dadIntsToBroadcast.Add(dadInt);
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

        return Task.FromResult(response);
    }

    public override Task<Empty> ReceiveAccepted(PaxosAccept accept, ServerCallContext context)
    {
        //TODO: we finally got the lease response, respond to client in waiting

        leases.AddRange(accept.AcceptedValue);

        return Task.FromResult(new Empty());
    }

    public override Task<BroadcastDadIntsAck> BroadcastDadInts(BroadcastDadIntsMsg msg, ServerCallContext context)
    {
        foreach (var dadInt in msg.Dadints)
        {
            dadInts[dadInt.Key] = dadInt.Value;
        }

        Console.WriteLine(
            $"Received broadcast dadints: {DadIntUtils.DadIntsToString(msg.Dadints)} now collection is {DadIntUtils.DadIntsDictionnaryToString(dadInts)}");

        return Task.FromResult(new BroadcastDadIntsAck { Ok = true });
    }
}