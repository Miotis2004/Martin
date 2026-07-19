using Martin.CommandLine;

namespace Martin.Cli;

internal static class Program
{
    static Task<int> Main(string[] args)
    {
        using var cancellation = new CommandCancellationSource();
        var app = MartinCliApplication.CreateDefault(SystemMartinConsole.Instance);
        return app.RunAsync(args, cancellation.Token);
    }
}
