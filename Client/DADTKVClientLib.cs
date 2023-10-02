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

    public List<DadInt> TxSubmit(string clientId, TransactionRequest request)
    {
        request.ClientId = clientId;

        var res = tm.service!.ExecuteTransaction(request);

        return res.ReadValues.ToList();
    }

    public bool Status()
    {
        return false;
    }
}