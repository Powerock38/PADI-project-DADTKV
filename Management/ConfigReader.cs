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

    private readonly uint systemDuration; // in time slots
    private DateTime tStart;
    private readonly uint durationSlot; // in milliseconds

    // Slot -> (TM/LM name -> isCrashed)
    private Dictionary<uint, Dictionary<string, bool>> failureDetection = new();

    // Slot -> (TM/LM name -> (suspecting TM/LM name -> suspected TM/LM name))
    private Dictionary<uint, Dictionary<string, List<string>>> failureSuspicions = new();

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
                    uint slot = uint.Parse(args[1]);

                    // Failure detection
                    Dictionary<string, bool> failureDetectionThisSlot = new();
                    string[] tmStates = args[2..(2 + transactionManagers.Count)];
                    string[] lmStates =
                        args[(2 + transactionManagers.Count)..(2 + transactionManagers.Count + leaseManagers.Count)];

                    // TMs should be declared first in the config file
                    var i = 0;
                    foreach (string state in tmStates)
                    {
                        string tm = transactionManagers[i].name;
                        failureDetectionThisSlot.Add(tm, state == "C");
                        i++;
                    }

                    // LMs should be declared after TMs in the config file
                    i = 0;
                    foreach (string state in lmStates)
                    {
                        string lm = leaseManagers[i].name;
                        failureDetectionThisSlot.Add(lm, state == "C");
                        i++;
                    }

                    failureDetection.Add(slot, failureDetectionThisSlot);

                    // Failure suspicions
                    Dictionary<string, List<string>> failureSuspicionsThisSlot = new();

                    string[] suspicions = args[(2 + transactionManagers.Count + leaseManagers.Count)..];

                    foreach (string suspicion in suspicions)
                    {
                        string[] suspectingSuspected = suspicion[1..^1].Split(',');
                        string suspecting = suspectingSuspected[0];
                        string suspected = suspectingSuspected[1];

                        if (!failureSuspicionsThisSlot.ContainsKey(suspecting))
                        {
                            failureSuspicionsThisSlot[suspecting] = new List<string>();
                        }

                        failureSuspicionsThisSlot[suspecting].Add(suspected);
                    }

                    failureSuspicions.Add(slot, failureSuspicionsThisSlot);
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

    private uint GetCurrentSlot()
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

    public bool IsCrashed(string processName)
    {
        // Missing time slots in the sequence of F commands correspond to time slots
        // where there is no change to the normal/crashed state of the previous slot

        uint currentSlot = GetCurrentSlot();

        for (uint i = currentSlot; i > 0; i--)
        {
            if (failureDetection.TryGetValue(i, out Dictionary<string, bool>? fCommand))
            {
                return fCommand[processName];
            }
        }

        return false;
    }

    public List<string> GetWhoISuspect(string processName)
    {
        uint currentSlot = GetCurrentSlot();

        for (uint i = currentSlot; i > 0; i--)
        {
            if (failureSuspicions.TryGetValue(i, out Dictionary<string, List<string>>? failureSuspicionsThisSlot))
            {
                if (failureSuspicionsThisSlot.TryGetValue(processName, out List<string>? whoISuspect))
                {
                    return whoISuspect;
                }
            }
        }

        return new List<string>();
    }
}