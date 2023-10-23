﻿using Dadtkv;
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