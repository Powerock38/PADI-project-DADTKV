using Dadtkv;
using Grpc.Net.Client;

namespace Management;

public class TransactionManagerStruct
{
    public string name { get; }
    public string url { get; }

    private GrpcChannel? channel;

    private TransactionManagerService.TransactionManagerServiceClient? service;

    public TransactionManagerStruct(string name, string url)
    {
        this.name = name;
        this.url = url;
    }

    public TransactionManagerService.TransactionManagerServiceClient GetService()
    {
        if (service == null)
        {
            channel = GrpcChannel.ForAddress(url);
            service = new TransactionManagerService.TransactionManagerServiceClient(channel);
        }

        return service;
    }
}