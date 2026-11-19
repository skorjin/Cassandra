namespace Cassandra.Application.Pipeline;

/// <summary>Cheap script check: tells whether a message is already written in the target language.</summary>
public static class ScriptDetector
{
    public static bool IsAlreadyIn(string text, string language)
    {
        var cyrillic = 0;
        var latin = 0;
        foreach (var c in text)
        {
            if (c is >= 'Ѐ' and <= 'ӿ')
            {
                cyrillic++;
            }
            else if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'))
            {
                latin++;
            }
        }

        return language switch
        {
            "ru" => cyrillic > latin,
            "en" => latin > cyrillic,
            _ => false,
        };
    }
}
