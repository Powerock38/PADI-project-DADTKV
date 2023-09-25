namespace SharedLib;

public struct ClientStruct
{
    private string name { get; }
    private string script { get; }
    
    public ClientStruct(string name, string script)
    {
        this.name = name;
        this.script = script;
    }
}