namespace SharedLib;

public class TransactionManagerStruct
{
    private string name { get; }
    private string url { get; }
    
    public TransactionManagerStruct(string name, string url)
    {
        this.name = name;
        this.url = url;
    }
}