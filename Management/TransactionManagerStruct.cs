using Dadtkv;
using Grpc.Net.Client;

namespace Management;

public class TransactionManagerStruct
{
    public string name { get; }
    public string url { get; }

    private GrpcChannel? channel;

    public TransactionManagerService.TransactionManagerServiceClient? service { get; private set; }


    public TransactionManagerStruct(string name, string url)
    {
        this.name = name;
        this.url = url;
    }

    public void openChannelService()
    {
        channel = GrpcChannel.ForAddress(url);
        service = new TransactionManagerService.TransactionManagerServiceClient(channel);
    }
}