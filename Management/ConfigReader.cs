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
    public TimeSpan tStart { get; }
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
                    tStart = TimeSpan.Parse(args[1]);
                    break;

                case ConfigCommands.FailureDetection:
                    //TODO
                    break;

                default:
                    throw new Exception("Invalid config line: " + line);
            }
        }
    }
}