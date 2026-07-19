namespace Martin.CommandLine;

public sealed class CommandCancellationSource : IDisposable
{
    readonly CancellationTokenSource source = new();

    public CommandCancellationSource()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public CancellationToken Token => source.Token;

    void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        source.Cancel();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        source.Dispose();
    }
}
