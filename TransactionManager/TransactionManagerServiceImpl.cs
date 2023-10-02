using Dadtkv;
using Grpc.Core;
using Management;

namespace TransactionManager;

public class TransactionManagerServiceImpl : TransactionManagerService.TransactionManagerServiceBase
{
    public TransactionManagerServiceImpl(string name)
    {
        this.name = name;
    }

    private readonly string name;

    private List<Lease> leases = new();

    private List<string> getMyLeases()
    {
        return leases.Where(l => l.TransactionManagerId == name).SelectMany(l => l.Dadints).ToList();
    }

    public override async Task<TransactionResponse> ExecuteTransaction(TransactionRequest request,
        ServerCallContext context)
    {
        Console.WriteLine("Received transcation request");

        // For each dadints asked (read or write), we check if TM has a lease for it. If not, TM asks for a lease.
        List<string> dadIntKeysToCheckLeases =
            request.ReadDadints.Concat(request.WriteDadints.Select(d => d.Key)).ToList();

        List<string> myLeases = getMyLeases();

        List<string> dadIntKeysToRequestLeases = dadIntKeysToCheckLeases.Where(key => !myLeases.Contains(key)).ToList();

        if (dadIntKeysToRequestLeases.Count > 0)
        {
            LeaseRequest leaseRequest = new LeaseRequest();
            leaseRequest.RequestedDadints.AddRange(dadIntKeysToRequestLeases);

            Console.WriteLine("Requesting lease for: " + DadIntUtils.DadIntsKeysToString(dadIntKeysToRequestLeases));

            await RequestLeases(leaseRequest);
        }

        TransactionResponse response = new TransactionResponse();

        //TODO: read Dadints
        response.ReadValues.Add(
            new DadInt
            {
                Key = "not implemented",
                Value = 0
            });

        //TODO: write Dadints

        return response;
    }

    private Task RequestLeases(LeaseRequest request)
    {
        return Task.CompletedTask;
    }
}