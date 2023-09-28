using Grpc.Net.Client;

namespace Management;

public class TransactionManagerStruct
{
    public string name { get; }
    public string url { get; }

    public GrpcChannel? channel { get; private set; }
    
    public TransactionManagerStruct(string name, string url)
    {
        this.name = name;
        this.url = url;
    }
    
    public void openChannel()
    {
        channel = GrpcChannel.ForAddress(url);
    }
}