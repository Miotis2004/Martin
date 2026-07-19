using System.Text;

namespace Martin.CodeGeneration;

public sealed class TemporaryAllocator
{
    int _nextId;

    public string Allocate(string purpose)
    {
        var normalized = NormalizePurpose(purpose);
        return $"__tmp_{normalized}_{_nextId++}";
    }

    static string NormalizePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
            return "value";

        var builder = new StringBuilder();
        foreach (var ch in purpose)
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');

        var result = builder.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "value" : result;
    }
}
