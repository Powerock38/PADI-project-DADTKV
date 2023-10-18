namespace Management;

public class ConfigReader
{
    private enum ConfigCommands
    {
        Comment = '#',
        Process = 'P',
        SystemDuration = 'S',
        DurationSlot = 'D',
        TStart = 'T',
        FailureDetection = 'F',
    }

    public List<TransactionManagerStruct> transactionManagers { get; }
    public List<LeaseManagerStruct> leaseManagers { get; }
    public List<ClientStruct> clients { get; }

    public uint systemDuration { get; } // in time slots
    private DateTime tStart;
    private uint durationSlot { get; } // in milliseconds

    public ConfigReader(string configFilePath)
    {
        IEnumerable<string> lines = File.ReadLines(configFilePath);

        transactionManagers = new List<TransactionManagerStruct>();
        leaseManagers = new List<LeaseManagerStruct>();
        clients = new List<ClientStruct>();

        foreach (string line in lines)
        {
            string[] args = line.Split(' ');

            ConfigCommands command = (ConfigCommands)line[0];

            switch (command)
            {
                case ConfigCommands.Comment:
                    continue;

                case ConfigCommands.Process:
                    string name = args[1];
                    string type = args[2];

                    switch (type)
                    {
                        case "T":
                            transactionManagers.Add(
                                new TransactionManagerStruct(name, args[3])
                            );
                            break;
                        case "L":
                            leaseManagers.Add(
                                new LeaseManagerStruct(name, args[3])
                            );
                            break;
                        case "C":
                            clients.Add(
                                new ClientStruct(name, args[3])
                            );
                            break;
                        default:
                            throw new Exception("Invalid process type: " + type);
                    }

                    break;

                case ConfigCommands.SystemDuration:
                    systemDuration = uint.Parse(args[1]);
                    break;

                case ConfigCommands.DurationSlot:
                    durationSlot = uint.Parse(args[1]);
                    break;

                case ConfigCommands.TStart:
                    DateTime currentDate = DateTime.Now.Date;

                    string[] timeParts = args[1].Split(':');
                    if (timeParts.Length == 3 &&
                        int.TryParse(timeParts[0], out int hour) &&
                        int.TryParse(timeParts[1], out int minute) &&
                        int.TryParse(timeParts[2], out int second))
                    {
                        tStart = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, hour, minute,
                            second);
                    }
                    else
                    {
                        throw new Exception("Invalid time format: " + args[1]);
                    }

                    break;

                case ConfigCommands.FailureDetection:
                    //TODO
                    break;

                default:
                    throw new Exception("Invalid config line: " + line);
            }
        }
    }

    public void ReadyWaitForStart()
    {
        Console.WriteLine("Ready to start");

        while (true)
        {
            TimeSpan timeToSleep = tStart - DateTime.Now;

            if (timeToSleep.TotalMilliseconds > 0)
            {
                Console.WriteLine(
                    $"Starting at {tStart} - sleeping for {timeToSleep.TotalMilliseconds} milliseconds...");
                Thread.Sleep(timeToSleep);
                Console.WriteLine("Starting");

                // Schedule end of system after systemDuration slots
                TimeSpan finishIn = tStart - DateTime.Now + TimeSpan.FromMilliseconds(systemDuration * durationSlot);

                Console.WriteLine($"System will end in {finishIn}");
                Task.Delay(finishIn).ContinueWith(_ =>
                {
                    Console.WriteLine("System ended");
                    Environment.Exit(0);
                });
            }
            else // Invalid tStart
            {
                DateTime currentDate = DateTime.Now;
                tStart = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, currentDate.Hour,
                    currentDate.Minute + 1, 0);
                Console.WriteLine($"Invalid tStart, trying to start at {tStart}");
                continue;
            }

            break;
        }
    }

    public uint GetCurrentSlot()
    {
        return (uint)Math.Floor((DateTime.Now - tStart) / TimeSpan.FromMilliseconds(durationSlot));
    }

    public void ScheduleForNextSlot(Action<uint> action)
    {
        uint currentSlot = GetCurrentSlot();

        DateTime nextSlotTimestamp = tStart + TimeSpan.FromMilliseconds(durationSlot * (currentSlot + 1));

        TimeSpan timeToSleep = nextSlotTimestamp - DateTime.Now;

        Task.Delay(timeToSleep).ContinueWith(_ => action(currentSlot + 1));

        Console.WriteLine(
            $"Next slot will start at {nextSlotTimestamp} - sleeping for {timeToSleep.TotalMilliseconds} milliseconds...");
    }
}