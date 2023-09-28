namespace Management;

public class LeaseManagerStruct
{
    public string name { get; }
    public string url { get; }
    
    public LeaseManagerStruct(string name, string url)
    {
        this.name = name;
        this.url = url;
    }
}