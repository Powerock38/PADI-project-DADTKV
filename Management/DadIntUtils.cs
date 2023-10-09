using Dadtkv;

namespace Management;

public static class DadIntUtils
{
    private static string FormatIfEmpty(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? "<empty>" : s;
    }
    
    public static string DadIntsToString(IEnumerable<DadInt> dadInts)
    {
        return FormatIfEmpty(string.Join(", ",
            dadInts.Select(dadInt => dadInt.Key + " = " + dadInt.Value)));
    }

    public static string DadIntsKeysToString(IEnumerable<string> keys)
    {
        return FormatIfEmpty(string.Join(", ", keys));
    }

    public static string DadIntsDictionnaryToString(Dictionary<string, int> dadInts)
    {
        return "{" + string.Join(", ",
            dadInts.Select(dadInt => dadInt.Key + " = " + dadInt.Value)) + "}";
    }
}