using System;
using System.IO;

class Program
{
    static void Main()
    {
        var cs = File.ReadAllText("Martin/tests/Martin.CodeGeneration.Tests/bin/Debug/net8.0/cs_output_iflet.txt");
        Console.WriteLine(cs);
    }
}
