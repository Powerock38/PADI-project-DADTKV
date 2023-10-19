using Dadtkv;
using Management;

namespace client;

public class DADTKVClientLib
{
    private readonly TransactionManagerStruct tm;

    public DADTKVClientLib(TransactionManagerStruct tm)
    {
        this.tm = tm;
    }

    public IEnumerable<DadInt> TxSubmit(string clientId, TransactionRequest request)
    {
        request.ClientId = clientId;

        // Check if 'abort' is not used as a key
        List<string> dadintsKeys = request.ReadDadints.Concat(request.WriteDadints.Select(d => d.Key)).ToList();
        if (dadintsKeys.Contains("abort") || dadintsKeys.Contains(""))
        {
            throw new ArgumentException("Invalid dadint key");
        }

        var res = tm.service!.ExecuteTransaction(request);

        if (res.ReadValues.Select(d => d.Key).Contains("abort"))
        {
            Console.WriteLine("Transaction was aborted!");
        }

        return res.ReadValues;
    }

    public bool Status()
    {
        //TODO
        return false;
    }
}