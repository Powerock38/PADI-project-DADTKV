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
    }
    else
    {
        uri = new Uri(tm.url);
    }
}

foreach (LeaseManagerStruct lm in config.leaseManagers)
{
    lm.openChannelService();
}

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

Action<uint>? loopEverySlot = null;
loopEverySlot = (_) =>
{
    if (config.IsCrashed(tmName))
    {
        Console.WriteLine("CRASHED!");
        Environment.Exit(0);
    }

    config.ScheduleForNextSlot(loopEverySlot!);
};

config.ScheduleForNextSlot(loopEverySlot);

while (true)
{
}