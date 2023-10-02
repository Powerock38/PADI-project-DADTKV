using System.Text.RegularExpressions;
using Dadtkv;
using Management;

namespace client;

public partial class ClientScript
{
    [GeneratedRegex("(\"([^\"]*)\",(\\d+))")]
    private static partial Regex writeKeyValuePattern();

    [GeneratedRegex("\\((\"([^\"]+)\"(?:,\"([^\"]+)\")*)?\\)")]
    private static partial Regex readKeysPattern();

    private enum ClientScriptCommands
    {
        Comment = '#',
        Transaction = 'T',
        Wait = 'W',
    }
    
    private readonly List<string> lines = new();

    private int currentLineIndex = 0;

    public ClientScript(string scriptPath)
    {
        lines.AddRange(File.ReadLines(scriptPath));
    }

    public TransactionRequest? runOneLine()
    {
        string line = lines[currentLineIndex];

        string[] args = line.Split(' ');

        ClientScriptCommands command = (ClientScriptCommands)line[0];

        TransactionRequest? request = null;

        switch (command)
        {
            case ClientScriptCommands.Comment:
                break;

            case ClientScriptCommands.Transaction:
                string readKeysString = args[1];

                List<string> readKeys = readKeysPattern().Matches(readKeysString)
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
                        Match match = writeKeyValuePattern().Matches(input)[0];
                        var dadInt = new DadInt
                        {
                            Key = match.Groups[2].Value,
                            Value = int.Parse(match.Groups[3].Value)
                        };
                        return dadInt;
                    }).ToList();

                Console.WriteLine("Read keys: " + DadIntUtils.DadIntsKeysToString(readKeys));
                Console.WriteLine("Write keys: " + DadIntUtils.DadIntsToString(writeKeys));

                request = new TransactionRequest();
                request.ReadDadints.AddRange(readKeys);
                request.WriteDadints.AddRange(writeKeys);
                break;

            case ClientScriptCommands.Wait:
                int waitTime = int.Parse(args[1]);
                Thread.Sleep(waitTime);
                break;

            default:
                throw new Exception("Invalid script line: " + line);
        }

        currentLineIndex = (currentLineIndex + 1) % lines.Count;

        return request;
    }
}