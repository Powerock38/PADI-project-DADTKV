using System.Diagnostics;
using Management;

Console.WriteLine("MAIN> Management console started");

string configAbsolutePath = Path.GetFullPath(args.Length > 0 ? args[0] : "../../../../configuration_sample.txt");

ConfigReader config = new ConfigReader(configAbsolutePath);

List<Process> processes = new();

foreach (TransactionManagerStruct tm in config.transactionManagers)
{
    Console.WriteLine($"MAIN> Launching TM {tm.name} {tm.url}");

    StartProcess("dotnet",
        $"run --no-build --project ../../../../TransactionManager/TransactionManager.csproj {configAbsolutePath} {tm.name}",
        tm.name);
}

foreach (LeaseManagerStruct lm in config.leaseManagers)
{
    Console.WriteLine($"MAIN> Launching LM {lm.name} {lm.url}");

    StartProcess("dotnet",
        $"run --no-build --project ../../../../LeaseManager/LeaseManager.csproj {configAbsolutePath} {lm.name}",
        lm.name);
}

foreach (ClientStruct cl in config.clients)
{
    Console.WriteLine($"MAIN> Launching Client {cl.name} {cl.script}");
    StartProcess("dotnet", $"run --no-build --project ../../../../Client/Client.csproj {configAbsolutePath} {cl.name}",
        cl.name);
}

while (true)
{
    var input = Console.ReadLine();

    if (input is "exit" or "quit" or "q" or "stop")
    {
        Console.WriteLine("MAIN> Stopping processes...");

        foreach (Process p in processes)
        {
            p.Kill();
        }

        break;
    }
}

return;

void StartProcess(string path, string arguments, string nodeName)
{
    Process process = new();
    process.EnableRaisingEvents = true;
    process.OutputDataReceived += (s, e) => ProcessOutputDataReceived(e, nodeName);
    process.ErrorDataReceived += (s, e) => ProcessErrorDataReceived(e, nodeName);
    process.Exited += (s, e) => ProcessExited(process, nodeName);

    process.StartInfo.FileName = path;
    process.StartInfo.Arguments = arguments;
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.RedirectStandardError = true;
    process.StartInfo.RedirectStandardOutput = true;

    process.Start();
    process.BeginErrorReadLine();
    process.BeginOutputReadLine();

    processes.Add(process);
}

void ProcessExited(Process process, string nodeName)
{
    Console.WriteLine($"{nodeName} EXITED> process exited with code {process.ExitCode.ToString()}");
}

void ProcessErrorDataReceived(DataReceivedEventArgs e, string nodeName)
{
    if (!string.IsNullOrWhiteSpace(e.Data))
    {
        Console.WriteLine($"{nodeName} ERROR> {e.Data}");

    }
}

void ProcessOutputDataReceived(DataReceivedEventArgs e, string nodeName)
{
    if (!string.IsNullOrWhiteSpace(e.Data))
    {
        Console.WriteLine($"{nodeName}> {e.Data}");
    }
}
