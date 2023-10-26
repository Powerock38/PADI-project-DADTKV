using client;
using Dadtkv;
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

// clients wait 3s before starting, to make sure all servers are up
Thread.Sleep(3000);

ClientScript script = new ClientScript($"../../../../Client/scripts/{scriptName}");

// Pick a random TM
TransactionManagerStruct myChoosenTM = config.transactionManagers[new Random().Next(config.transactionManagers.Count)];
DADTKVClientLib lib = new(myChoosenTM);

config.ReadyWaitForStart();

while (true)
{
    TransactionRequest? request = script.runOneLine();

    if (request != null)
    {
        IEnumerable<DadInt> dadInts = lib.TxSubmit(clientName, request);

        Console.WriteLine($"RESPONSE FROM {myChoosenTM.name}: {DadIntUtils.DadIntsToString(dadInts)}");
    }
}