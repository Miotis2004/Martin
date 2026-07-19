using Martin.Build;
using Martin.Execution;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Integration.Tests;

public sealed class Phase3IntegrationTests
{
    [Fact]
    public async Task BuildsAndRunsRepresentativeProgram()
    {
        var source = "func add(_ left: Int, _ right: Int) -> Int { return left + right } func main() { let answer = add(20, 22) if answer == 42 { print(\"Martin works!\") } }";
        var output = Path.Combine(Path.GetTempPath(), "MartinTests", Guid.NewGuid().ToString("N"));
        var build = await new MartinBuildService().BuildAsync(Compilation.Create(SyntaxTree.Parse(source, "main.martin")), new BuildOptions { OutputDirectory = output, AssemblyName = "HelloMartin", UseAppHost = false });
        Assert.True(build.Success, build.StandardOutput + build.StandardError + string.Join("\n", build.Diagnostics.Select(d => d.Message)));
        Assert.Contains(build.Artifacts, a => a.Kind == BuildArtifactKind.ManagedAssembly);
        Assert.Contains(build.Artifacts, a => a.Kind == BuildArtifactKind.RuntimeConfiguration);
        Assert.Contains(build.Artifacts, a => a.Kind == BuildArtifactKind.DependencyManifest);
        Assert.Contains(build.Artifacts, a => a.Kind == BuildArtifactKind.RuntimeLibrary);
        Assert.Equal(build.Artifacts.Select(a => a.Path).OrderBy(Path.GetFileName, StringComparer.Ordinal), build.Artifacts.Select(a => a.Path));
        var run = await new MartinExecutionService().RunAsync(build, []);
        Assert.True(run.Completed, run.StandardError);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal("Martin works!" + Environment.NewLine, run.StandardOutput);
    }
}
