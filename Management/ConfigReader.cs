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

    public uint systemDuration { get; }
    private DateTime tStart;
    public uint durationSlot { get; }

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
                        int.TryParse(timeParts[0], out int hours) &&
                        int.TryParse(timeParts[1], out int minutes) &&
                        int.TryParse(timeParts[2], out int seconds))
                    {
                        DateTime dateTime = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, hours,
                            minutes, seconds);
                        tStart = dateTime;
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

    public void waitForStart()
    {
        while (true)
        {
            TimeSpan timeToSleep = tStart - DateTime.Now;

            if (timeToSleep.TotalMilliseconds > 0)
            {
                Console.WriteLine($"Sleeping for {timeToSleep.TotalMilliseconds} milliseconds...");
                Thread.Sleep(timeToSleep);
                Console.WriteLine("Ready");
            }
            else
            {
                Console.WriteLine("invalid tStart, starting in 5 seconds...");
                tStart = DateTime.Now.AddSeconds(5);
                continue;
            }

            break;
        }
    }
}