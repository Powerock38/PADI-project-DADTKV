using client;
using Management;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run <config_path> <client_name>");
    return;
}

string configPath = args[0];
string clientName = args[1];

ConfigReader config = new ConfigReader(configPath);

string scriptName = config.clients.Find(c => c.name == clientName).script;

foreach (TransactionManagerStruct tm in config.transactionManagers)
{
    tm.openChannel();
    Console.WriteLine($"Connected to {tm.name} {tm.url}");
}

Console.WriteLine("Executing script: " + scriptName + "...");

ClientScript cl = new ClientScript($"../../../../Client/scripts/{scriptName}");