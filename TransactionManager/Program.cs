using Dadtkv;
using Grpc.Core;
using Management;
using TransactionManager;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run <config_path> <transaction_manager_name>");
    return;
}

string configPath = args[0];
string tmName = args[1];

ConfigReader config = new ConfigReader(configPath);

Uri? uri = config.transactionManagers.Where(tm => tm.name == tmName)
    .Select(tm => new Uri(tm.url)).FirstOrDefault();

if (uri == null)
{
    Console.WriteLine("didn't found myself in config file");
    return;
}

var tmService = new TransactionManagerServiceImpl(tmName, config);

Server server = new Server
{
    Services =
    {
        TransactionManagerService.BindService(tmService)
    },
    Ports = { new ServerPort(uri.Host, uri.Port, ServerCredentials.Insecure) }
};
server.Start();

config.ReadyWaitForStart();

config.StartSlotTickingWithAction((_) =>
{
    if (config.IsCrashed(tmName))
    {
        Console.WriteLine("CRASHED!");
        Environment.Exit(0);
    }
});

while (true)
{
}