using System.Text;
using Dadtkv;

namespace Management;

/*
 * LeaseDB is a database of leases.
 * Each Key is a TransactionManager name.
 * Each Value is a set of DadInts.
 * The list is guaranteed to be sorted, as well as the sets.
 * A dadint key can only be present once in the whole database: the rule is last writer wins.
 */
public class LeaseDB
{
    private SortedDictionary<string, SortedSet<string>> leases = new();

    private void Add(string tmName, IEnumerable<string> dadIntsKeys)
    {
        // Necessary list clone to be able to iterate over the dadIntsKeys twice
        List<string> dadIntsKeysList = dadIntsKeys.ToList();

        // Add to set tmName
        if (leases.TryGetValue(tmName, out SortedSet<string>? dadInts))
        {
            dadInts.UnionWith(dadIntsKeysList);
        }
        else
        {
            leases.Add(tmName, new SortedSet<string>(dadIntsKeysList));
        }

        // Remove duplicates in every other set
        leases.Where(pair => pair.Key != tmName).ToList().ForEach(pair => pair.Value.ExceptWith(dadIntsKeysList));

        // remove empty sets
        leases = new SortedDictionary<string, SortedSet<string>>(leases.Where(pair => pair.Value.Count > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    public void Add(Lease lease)
    {
        Add(lease.TransactionManagerId, lease.Dadints);
    }

    public void AddRange(LeaseDB leaseDB)
    {
        foreach ((string key, SortedSet<string> value) in leaseDB.leases)
        {
            Add(key, value);
        }
    }

    public void Clear()
    {
        leases.Clear();
    }

    public bool IsEmpty()
    {
        return leases.Count == 0;
    }

    public LeaseDB_GRPC IntoGRPC()
    {
        LeaseDB_GRPC grpc = new();

        foreach ((string key, SortedSet<string> value) in leases)
        {
            if (grpc.LeaseDb.TryGetValue(key, out LeaseDB_GRPC.Types.LeaseDBEntry? grpcValue))
            {
                grpcValue.Dadints.AddRange(value);
            }
            else
            {
                grpc.LeaseDb.Add(key, new LeaseDB_GRPC.Types.LeaseDBEntry
                {
                    Dadints = { value }
                });
            }
        }

        return grpc;
    }

    public static LeaseDB FromGRPC(LeaseDB_GRPC grpc)
    {
        var leaseDB = new LeaseDB();

        foreach ((string key, LeaseDB_GRPC.Types.LeaseDBEntry value) in grpc.LeaseDb)
        {
            leaseDB.Add(key, value.Dadints);
        }

        return leaseDB;
    }

    public SortedSet<string> Get(string key)
    {
        return leases.TryGetValue(key, out SortedSet<string>? dadInts) ? dadInts : new SortedSet<string>();
    }

    public override string ToString()
    {
        StringBuilder sb = new();

        foreach ((string key, SortedSet<string> value) in leases)
        {
            sb.Append($"{key}: {string.Join(", ", value)} | ");
        }

        string res = sb.ToString();

        return string.IsNullOrWhiteSpace(res) ? "<empty>" : res;
    }
}