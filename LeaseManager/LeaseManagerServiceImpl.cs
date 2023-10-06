using Dadtkv;
using Grpc.Core;
using Management;

namespace LeaseManager;

public class LeaseManagerServiceImpl : LeaseManagerService.LeaseManagerServiceBase
{
    private readonly List<TransactionManagerStruct> transactionManagers;
    private readonly List<LeaseManagerStruct> leaseManagers;
    private readonly string name;
    
    public LeaseManagerServiceImpl(string name, List<TransactionManagerStruct> transactionManagers, List<LeaseManagerStruct> leaseManagers)
    {
        this.name = name;
        this.transactionManagers = transactionManagers;
        this.leaseManagers = leaseManagers;
    }
    
    public override Task<LeaseResponse> RequestLeases(LeaseRequest request, ServerCallContext context)
    {
        Console.WriteLine($"Received lease request from {request.TransactionManagerId}");
        
        //TODO: check if asked dadints are not already leased by another TM
        // paxos
        
        Lease lease = new Lease
        {
            TransactionManagerId = request.TransactionManagerId
        };
        lease.Dadints.AddRange(request.RequestedDadints);
        
        return Task.FromResult(new LeaseResponse
        {
            Lease = { lease }
        });
    }
}