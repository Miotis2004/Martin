using Martin.Build.Artifacts;
using Xunit;

namespace Martin.Build.Tests;

public sealed class RuntimeCompatibilityTests
{
    [Fact]
    public void Exact_runtime_compatibility_is_accepted()
    {
        var diagnostic = RuntimeCompatibility.Validate(Descriptor(minor: RuntimeCompatibility.CompilerMinimumCompatibilityMinor));

        Assert.Null(diagnostic);
    }

    [Fact]
    public void Compatible_newer_minor_runtime_is_accepted()
    {
        var diagnostic = RuntimeCompatibility.Validate(Descriptor(minor: RuntimeCompatibility.CompilerMaximumCompatibilityMinor));

        Assert.Null(diagnostic);
    }

    [Fact]
    public void Too_old_runtime_reports_stable_diagnostic()
    {
        var diagnostic = RuntimeCompatibility.Validate(Descriptor(minor: RuntimeCompatibility.CompilerMinimumCompatibilityMinor - 1));

        Assert.NotNull(diagnostic);
        Assert.Equal("MRT3223", diagnostic.Code);
        Assert.Contains(RuntimeCompatibility.CompilerVersion, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Too_new_runtime_reports_stable_diagnostic()
    {
        var diagnostic = RuntimeCompatibility.Validate(Descriptor(minor: RuntimeCompatibility.CompilerMaximumCompatibilityMinor + 1));

        Assert.NotNull(diagnostic);
        Assert.Equal("MRT3224", diagnostic.Code);
    }

    [Fact]
    public void Major_mismatch_reports_stable_diagnostic()
    {
        var diagnostic = RuntimeCompatibility.Validate(Descriptor(major: RuntimeCompatibility.CompilerCompatibilityMajor + 1));

        Assert.NotNull(diagnostic);
        Assert.Equal("MRT3225", diagnostic.Code);
    }

    static RuntimeDescriptor Descriptor(int? major = null, int? minor = null) => new() {
        AssemblyPath = Path.GetFullPath("Martin.Runtime.dll"),
        AssemblyVersion = new Version(0, 1, 0, 0),
        InformationalVersion = "0.1.0-alpha",
        CompatibilityMajor = major ?? RuntimeCompatibility.CompilerCompatibilityMajor,
        CompatibilityMinor = minor ?? RuntimeCompatibility.CompilerMinimumCompatibilityMinor,
        StagingRoot = Path.GetTempPath(),
        ModuleVersionId = Guid.Empty,
        Sha256 = string.Empty
    };
}
