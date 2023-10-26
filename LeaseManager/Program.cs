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

ConfigReader config = new ConfigReader(configPath);

Uri? uri = config.leaseManagers.Where(lm => lm.name == lmName)
    .Select(lm => new Uri(lm.url)).FirstOrDefault();

if (uri == null)
{
    Console.WriteLine("didn't found myself in config file");
    return;
}

var lmService = new LeaseManagerServiceImpl(lmName, config);

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

Action<uint>? loopEverySlot = null;
loopEverySlot = (slot) =>
{
    lmService.ProcessLeaseRequests(slot);

    if (config.IsCrashed(lmName))
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