using Dadtkv;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Management;

namespace TransactionManager;

public class TransactionManagerServiceImpl : TransactionManagerService.TransactionManagerServiceBase
{
    private const uint BROADCAST_TIMEOUT = 3;

    private readonly ConfigReader config;

    public TransactionManagerServiceImpl(string name, ConfigReader config)
    {
        this.name = name;
        this.config = config;
    }

    private readonly Dictionary<string, int> dadInts = new();

    private readonly string name;

    private LeaseDB leases = new();

    private SortedSet<string> GetMyLeases()
    {
        return leases.Get(name);
    }

    private uint GetQuorumSize()
    {
        // TODO minus 1 because we don't count ourselves?
        int count = config.transactionManagers.Count - config.GetWhoISuspect(name).Count - 1;
        return (uint)Math.Ceiling(count * 0.5);
    }

    public override async Task<TransactionResponse> ExecuteTransaction(TransactionRequest request,
        ServerCallContext context)
    {
        List<string> requestReadDadInts = request.ReadDadints.Where(key => !string.IsNullOrEmpty(key)).ToList();

        Console.WriteLine(
            $"Received transaction request from {request.ClientId}: read {DadIntUtils.DadIntsKeysToString(requestReadDadInts)} | write {DadIntUtils.DadIntsToString(request.WriteDadints)}");

        Console.WriteLine($"MY COLLECTION = {DadIntUtils.DadIntsDictionnaryToString(dadInts)}");

        // For each dadints asked (read or write), we check if TM has a lease for it. If not, TM asks for a lease.
        List<string> dadIntKeysToCheckLeases =
            requestReadDadInts.Concat(request.WriteDadints.Select(d => d.Key)).ToList();

        SortedSet<string> myLeases = GetMyLeases();

        List<string> dadIntKeysToRequestLeases = dadIntKeysToCheckLeases.Where(key => !myLeases.Contains(key)).ToList();

        // If we need to request leases, we ask for them
        if (dadIntKeysToRequestLeases.Count > 0)
        {
            Lease leaseRequest = new()
            {
                TransactionManagerId = name
            };
            leaseRequest.Dadints.AddRange(dadIntKeysToRequestLeases);

            Console.WriteLine("Requesting lease for: " + DadIntUtils.DadIntsKeysToString(dadIntKeysToRequestLeases));

            foreach (var lm in config.leaseManagers)
            {
                lm.service!.RequestLeases(leaseRequest);
            }

            //TODO: maybe wait for leases to be received instead of aborting the transaction?
            return new TransactionResponse
            {
                ReadValues = { new DadInt { Key = "abort", Value = 0 } }
            };
        }

        // Build the response
        TransactionResponse response = new TransactionResponse();

        // Reads first
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

        // Then Writes: update local dadints database (from request.WriteDadints)
        List<DadInt> dadIntsToBroadcast = new();
        foreach (DadInt dadInt in request.WriteDadints)
        {
            dadInts[dadInt.Key] = dadInt.Value;
            dadIntsToBroadcast.Add(dadInt);
        }

        // Broadcast new or edited dadints (from request.WriteDadints) to all other TMs
        if (dadIntsToBroadcast.Count > 0)
        {
            uint nbAcks = 0;
            uint quorum = GetQuorumSize();

            List<Task<BroadcastDadIntsAck>> tasks = config.transactionManagers.Where(tm => tm.name != name)
                .Select(tm => tm.service!.BroadcastDadIntsAsync(
                    new BroadcastDadIntsMsg { Dadints = { dadIntsToBroadcast } },
                    deadline: DateTime.Now.AddSeconds(BROADCAST_TIMEOUT)
                ).ResponseAsync).ToList();

            // Only wait for <quorum> acks
            while (nbAcks < quorum && tasks.Count > 0)
            {
                Task<BroadcastDadIntsAck> completedTask = await Task.WhenAny(tasks);
                if (completedTask.IsCompletedSuccessfully && completedTask.Result.Ok)
                {
                    nbAcks++;
                }

                tasks.Remove(completedTask);
            }

            if (nbAcks < quorum)
            {
                Console.WriteLine($"ERROR: only {nbAcks} acks received, quorum is {quorum}");
                return new TransactionResponse
                {
                    ReadValues = { new DadInt { Key = "abort", Value = 0 } }
                };
            }
        }

        return response;
    }

    public override Task<Empty> ReceiveAccepted(PaxosAccept accept, ServerCallContext context)
    {
        //TODO: maybe wait for every accept before sending the response to the client? and assert that we have a quorum
        // https://github.com/cocagne/paxos/blob/master/paxos/essential.py#L162

        //TODO: we finally got the lease response, respond to client in waiting?

        leases = LeaseDB.FromGRPC(accept.AcceptedValue);

        Console.WriteLine(leases);

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