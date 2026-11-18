namespace Cassandra.Domain.Models;

/// <summary>Translation direction, ISO 639-1 codes ("en", "ru").</summary>
public readonly record struct LanguagePair(string Source, string Target)
{
    public static LanguagePair EnRu => new("en", "ru");

    public LanguagePair Reversed() => new(Target, Source);

    public override string ToString() => $"{Source}→{Target}";
}
