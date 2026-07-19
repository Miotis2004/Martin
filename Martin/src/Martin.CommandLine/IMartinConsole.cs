namespace Martin.CommandLine;

public interface IMartinConsole
{
    TextWriter Out { get; }

    TextWriter Error { get; }

    bool IsOutputRedirected { get; }

    bool IsErrorRedirected { get; }

    bool SupportsColor { get; }
}
