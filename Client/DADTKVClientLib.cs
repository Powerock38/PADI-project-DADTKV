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

        TransactionResponse? res = null;
        try
        {
            res = tm.GetService().ExecuteTransaction(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.Error.WriteLine("No response from TM, it likely crashed");
        }

        return res != null ? res.ReadValues : new List<DadInt>();
    }
}