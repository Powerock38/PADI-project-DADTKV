namespace Management;

public struct ClientStruct
{
    public string name { get; }
    public string script { get; }
    
    public ClientStruct(string name, string script)
    {
        this.name = name;
        this.script = script;
    }
}