using Grpc.Core;
using Management;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run <config_path> <transaction_manager_name>");
    return;
}

string configPath = args[0];
string tmName = args[1];

ConfigReader config = new ConfigReader(configPath);

// TODO: start gRPC server
// Server server = new Server
// {
//     Services = { ChatServerService.BindService(new ServerService()) },
//     Ports = { new ServerPort(hostname, port, ServerCredentials.Insecure) }
// };
// server.Start();

foreach (TransactionManagerStruct tm in config.transactionManagers)
{
    if (tm.name != tmName)
    {
        tm.openChannel();
        Console.WriteLine("Ready");
    }
}