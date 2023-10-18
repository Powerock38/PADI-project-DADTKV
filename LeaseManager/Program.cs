using Dadtkv;
using Grpc.Core;
using LeaseManager;
using Management;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run <config_path> <lease_manager_name>");
    return;
}

string configPath = args[0];
string lmName = args[1];

Uri? uri = null;

ConfigReader config = new ConfigReader(configPath);

foreach (LeaseManagerStruct lm in config.leaseManagers)
{
    if (lm.name != lmName)
    {
        lm.openChannelService();
    }
    else
    {
        uri = new Uri(lm.url);
    }
}

foreach (TransactionManagerStruct tm in config.transactionManagers)
{
    tm.openChannelService();
}

if (uri == null)
{
    Console.WriteLine("didn't found myself in config file");
    return;
}

var lmService = new LeaseManagerServiceImpl(lmName, config.transactionManagers, config.leaseManagers);

Server server = new Server
{
    Services =
    {
        LeaseManagerService.BindService(lmService)
    },
    Ports = { new ServerPort(uri.Host, uri.Port, ServerCredentials.Insecure) }
};
server.Start();

config.ReadyWaitForStart();

config.ScheduleForNextSlot((slot) =>
{
    lmService.ProcessLeaseRequests(slot);
});

while (true)
{
}