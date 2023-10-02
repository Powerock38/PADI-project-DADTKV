using Dadtkv;

namespace Management;

public class DadIntUtils
{
    public static string DadIntsToString(IEnumerable<DadInt> dadInts)
    {
        return string.Join(", ",
            dadInts.Select(dadInt => dadInt.Key + " = " + dadInt.Value));
    }

    public static string DadIntsKeysToString(IEnumerable<string> keys)
    {
        return string.Join(", ", keys);
    }
}