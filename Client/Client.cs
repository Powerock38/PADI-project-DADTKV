using System.Text.RegularExpressions;

namespace client;

public class Client
{
    private const string readKeysPattern = """\(("([^"]+)"(?:,"([^"]+)")*)?\)""";
    private const string writeKeyValuePattern = """("([^"]*)",(\d+))""";

    private enum ClientScriptCommands
    {
        Comment = '#',
        Transaction = 'T',
        Wait = 'W',
    }

    public Client(string scriptPath)
    {
        IEnumerable<string> lines = File.ReadLines(scriptPath);

        foreach (string line in lines)
        {
            string[] args = line.Split(' ');

            ClientScriptCommands command = (ClientScriptCommands)line[0];

            switch (command)
            {
                case ClientScriptCommands.Comment:
                    continue;

                case ClientScriptCommands.Transaction:
                    string readKeysString = args[1];

                    List<string> readKeys = Regex.Matches(readKeysString, readKeysPattern)
                        .Select(match => match.Groups[1].Value).ToList();

                    string writeKeysString = args[2].Trim('(', ')');
                    if (writeKeysString != "")
                    {
                        writeKeysString = writeKeysString.Substring(1, writeKeysString.Length - 2);
                    }

                    List<DadInt> writeKeys = writeKeysString.Split(">,<")
                        .Where(input => input != "")
                        .Select(input =>
                        {
                            Match match = Regex.Matches(input, writeKeyValuePattern)[0];
                            return new DadInt
                                { key = match.Groups[2].Value, value = int.Parse(match.Groups[3].Value) };
                        }).ToList();

                    Console.WriteLine("Read keys: " + string.Join(", ", readKeys));
                    Console.WriteLine("Write keys: " + string.Join(", ",
                        writeKeys.Select(dadInt => dadInt.key + " = " + dadInt.value)));
                    break;

                case ClientScriptCommands.Wait:
                    int waitTime = int.Parse(args[1]);
                    Thread.Sleep(waitTime);
                    break;

                default:
                    throw new Exception("Invalid script line: " + line);
            }
        }
    }

    public void run()
    {
    }
}