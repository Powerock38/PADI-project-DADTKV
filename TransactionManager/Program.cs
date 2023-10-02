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

Uri? uri = null;

ConfigReader config = new ConfigReader(configPath);

foreach (TransactionManagerStruct tm in config.transactionManagers)
{
    if (tm.name != tmName)
    {
        tm.openChannelService();
        Console.WriteLine("Ready");
    }
    else
    {
        uri = new Uri(tm.url);
    }
}

if (uri == null)
{
    Console.WriteLine("didn't found myself in config file");
    return;
}

Server server = new Server
{
    Services = { TransactionManagerService.BindService(new TransactionManagerServiceImpl(tmName)) },
    Ports = { new ServerPort(uri.Host, uri.Port, ServerCredentials.Insecure) }
};
server.Start();

while(true)
{}
