using System.Text.RegularExpressions;
using Dadtkv;

namespace client;

public partial class ClientScript
{
    [GeneratedRegex("(\"([^\"]*)\",(\\d+))")]
    private static partial Regex writeKeyValuePattern();

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
                string readKeysString = args[1].Trim('(', ')');
                List<string> readKeys = readKeysString.Split(",")
                    .Select(key => key.Trim('"'))
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .ToList();

                string writeKeysString = args[2].Trim('(', ')');
                if (writeKeysString != "")
                {
                    writeKeysString = writeKeysString.Substring(1, writeKeysString.Length - 2);
                }

                List<DadInt> writeKeys = writeKeysString.Split(">,<")
                    .Where(input => !string.IsNullOrWhiteSpace(input))
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

                Console.WriteLine("Sending transaction: " + line);

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