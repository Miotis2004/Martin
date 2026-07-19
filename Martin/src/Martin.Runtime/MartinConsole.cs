using System.Globalization;

namespace Martin.Runtime;

public static class MartinConsole
{
    static string[] _arguments = [];

    public static void SetArguments(string[] arguments) => _arguments = arguments ?? [];
    public static long ArgumentCount() => _arguments.LongLength;
    public static string Argument(long index) => index >= 0 && index < _arguments.LongLength ? _arguments[checked((int)index)] : string.Empty;
    public static string ReadLine() => Console.ReadLine() ?? string.Empty;
    public static void WriteError(string value) => Console.Error.WriteLine(value);
    public static void Exit(long exitCode) => Environment.Exit(checked((int)exitCode));
    public static void Print(string value) => Console.WriteLine(value);
    public static void Print(long value) => Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
    public static void Print(double value) => Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
    public static void Print(bool value) => Console.WriteLine(value ? "true" : "false");
}
