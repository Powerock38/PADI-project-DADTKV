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

        var res = tm.service!.ExecuteTransaction(request);

        return res.ReadValues;
    }

    public bool Status()
    {
        return false;
    }
}