namespace SharedLib;

public class LeaseManagerStruct
{
    private string name { get; }
    private string url { get; }
    
    public LeaseManagerStruct(string name, string url)
    {
        this.name = name;
        this.url = url;
    }
}