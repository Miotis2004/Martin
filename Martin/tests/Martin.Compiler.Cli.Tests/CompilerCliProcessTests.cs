using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace Martin.Compiler.Cli.Tests;

public sealed class CompilerCliProcessTests
{
    [Fact] public void CompilerCliAssemblyLoads()=>Assert.NotNull(Assembly.Load("Martin.Compiler.Cli"));
    [Fact] public async Task MissingSourceFileFails(){var r=await RunAsync(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".martin"));Assert.NotEqual(0,r.ExitCode);Assert.Contains("MRT4503",r.StandardError);}
    [Fact] public async Task UnknownOptionFailsDuringParsing(){var r=await RunAsync("--unknown-option");Assert.Equal(2,r.ExitCode);Assert.Contains("MRT4502",r.StandardError);Assert.Contains("Unknown option",r.StandardError);}

    [Fact] public async Task MissingOptionValueFailsDuringParsing(){var r=await RunAsync("--output");Assert.Equal(2,r.ExitCode);Assert.Contains("requires a value",r.StandardError);}
    [Fact] public async Task DuplicateSourceFilesFailAfterNormalization(){var root=Temp();try{var file=Path.Combine(root,"main.martin");File.WriteAllText(file,"func main() {}\n");var r=await RunAsync(file,Path.Combine(root,".","main.martin"));Assert.Equal(2,r.ExitCode);Assert.Contains("MRT4509",r.StandardError);}finally{Directory.Delete(root,true);}}
    [Fact] public async Task HelpAndVersionAreStable(){var help=await RunAsync("--help");Assert.Equal(0,help.ExitCode);Assert.Contains("Usage: martinc",help.StandardOutput);var version=await RunAsync("--version");Assert.Equal(0,version.ExitCode);Assert.Contains("martinc",version.StandardOutput);Assert.Contains("Language version:",version.StandardOutput);Assert.Contains("Manifest version:",version.StandardOutput);}
    [Fact] public async Task JsonDiagnosticsIncludeLocation(){var root=Temp();try{var file=Path.Combine(root,"bad.martin");File.WriteAllText(file,"func main( { }\n");var r=await RunAsync(file,"--output",Path.Combine(root,"out"),"--diagnostic-format","json");Assert.NotEqual(0,r.ExitCode);Assert.Contains("\"location\"",r.StandardError);Assert.Contains("\"startLine\"",r.StandardError);Assert.Contains("bad.martin",r.StandardError);}finally{Directory.Delete(root,true);}}
    [Fact] public async Task InvalidExtensionFails(){var root=Temp();try{var file=Path.Combine(root,"main.txt");File.WriteAllText(file,"func main() {}");var r=await RunAsync(file);Assert.NotEqual(0,r.ExitCode);Assert.Contains("MRT4504",r.StandardError);}finally{Directory.Delete(root,true);}}
    [Fact] public async Task InvalidSourceProducesMartinDiagnostic(){var root=Temp();try{var file=Path.Combine(root,"bad.martin");File.WriteAllText(file,"func main( { }");var r=await RunAsync(file,"--output",Path.Combine(root,"out"),"--diagnostic-format","json");Assert.NotEqual(0,r.ExitCode);Assert.Contains("MRT",r.StandardError);Assert.Contains("code",r.StandardError);}finally{Directory.Delete(root,true);}}
    [Fact(Timeout=60000)] public async Task ValidSourceCompilesToRequestedOutputPath(){var root=Temp();try{var file=Path.Combine(root,"main.martin");var outDir=Path.Combine(root,"out");File.WriteAllText(file,"func main() { print(\"hello\") }\n");var r=await RunAsync(file,"--output",outDir,"--name","Hello","--quiet");Assert.Equal(0,r.ExitCode);Assert.True(Directory.Exists(outDir));}finally{Directory.Delete(root,true);}}
    static string Temp(){var p=Path.Combine(Path.GetTempPath(),"MartinCompilerCliTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    static async Task<Result> RunAsync(params string[] args){var psi=new ProcessStartInfo("dotnet"){RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=RepoRoot()};psi.ArgumentList.Add("run");psi.ArgumentList.Add("--project");psi.ArgumentList.Add(Path.Combine("Martin","src","Martin.Compiler.Cli","Martin.Compiler.Cli.csproj"));psi.ArgumentList.Add("--");foreach(var a in args)psi.ArgumentList.Add(a);using var p=Process.Start(psi)!;var so=p.StandardOutput.ReadToEndAsync();var se=p.StandardError.ReadToEndAsync();using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(150));await p.WaitForExitAsync(cts.Token);return new(p.ExitCode,await so,await se);}
    static string RepoRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"README.md")))d=d.Parent;return d?.FullName??throw new InvalidOperationException("Repository root not found.");}
    sealed record Result(int ExitCode,string StandardOutput,string StandardError);
}
