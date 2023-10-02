using Dadtkv;
using Management;

namespace client;

public class DADTKVClientLib
{

    private int currentTmIndex;

    private readonly List<TransactionManagerStruct> tms;

    private TransactionManagerStruct getCurrentTM()
    {
        return tms[currentTmIndex];
    }

    public DADTKVClientLib(List<TransactionManagerStruct> tms)
    {
        this.tms = tms;
        currentTmIndex = new Random().Next(tms.Count);
    }
    
    public List<DadInt> TxSubmit(string clientId, TransactionRequest request)
    {
       var res= getCurrentTM().service!.ExecuteTransaction(request);

        currentTmIndex = (currentTmIndex + 1) % tms.Count;
        
        return res.ReadValues.ToList();
    }

    public bool Status()
    {
        return false;
    }
}