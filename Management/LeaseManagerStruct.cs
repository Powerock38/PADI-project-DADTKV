using Dadtkv;
using Grpc.Net.Client;

namespace Management;

public class LeaseManagerStruct
{
    public string name { get; }
    public string url { get; }

    private GrpcChannel? channel;

    private LeaseManagerService.LeaseManagerServiceClient? service;

    public LeaseManagerStruct(string name, string url)
    {
        this.name = name;
        this.url = url;
    }

    public LeaseManagerService.LeaseManagerServiceClient GetService()
    {
        if (service == null)
        {
            channel = GrpcChannel.ForAddress(url);
            service = new LeaseManagerService.LeaseManagerServiceClient(channel);
        }

        return service;
    }
}