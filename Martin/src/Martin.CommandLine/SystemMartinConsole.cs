namespace Martin.CommandLine;

public sealed class SystemMartinConsole : IMartinConsole
{
    public static SystemMartinConsole Instance { get; } = new();

    public TextWriter Out => Console.Out;

    public TextWriter Error => Console.Error;

    public bool IsOutputRedirected => Console.IsOutputRedirected;

    public bool IsErrorRedirected => Console.IsErrorRedirected;

    public bool SupportsColor => !Console.IsOutputRedirected && !Console.IsErrorRedirected && !Console.IsInputRedirected;
}
