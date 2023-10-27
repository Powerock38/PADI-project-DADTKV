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

ClientScript script = new ClientScript($"../../../../Client/scripts/{scriptName}");

// Pick a TM, at random or round-robin deterministically
bool random = false;

TransactionManagerStruct myChoosenTM;
if (random)
{
    myChoosenTM = config.transactionManagers[new Random().Next(config.transactionManagers.Count)];
}
else
{
    int i = config.clients.FindIndex(c => c.name == clientName);
    myChoosenTM = config.transactionManagers[i % config.transactionManagers.Count];
}

DADTKVClientLib lib = new(myChoosenTM);

config.ReadyWaitForStart();

while (true)
{
    TransactionRequest? request = script.runOneLine();

    // If the script asks for a transaction, send it to the TM
    if (request != null)
    {
        IEnumerable<DadInt> dadInts = lib.TxSubmit(clientName, request);

        Console.WriteLine($"RESPONSE FROM {myChoosenTM.name}: {DadIntUtils.DadIntsToString(dadInts)}");
    }
}