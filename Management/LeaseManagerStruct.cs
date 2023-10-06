using Dadtkv;
using Grpc.Net.Client;

namespace Management;

public class LeaseManagerStruct
{
    public string name { get; }
    public string url { get; }

    private GrpcChannel? channel;

    public LeaseManagerService.LeaseManagerServiceClient? service { get; private set; }

    public LeaseManagerStruct(string name, string url)
    {
        this.name = name;
        this.url = url;
    }

    public void openChannelService()
    {
        channel = GrpcChannel.ForAddress(url);
        service = new LeaseManagerService.LeaseManagerServiceClient(channel);
    }
}