using System;
using System.IO;

class Program
{
    static void Main()
    {
        var cs = File.ReadAllText("cs_output.txt");
        Console.WriteLine(cs);
    }
}
