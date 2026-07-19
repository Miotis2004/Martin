using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14ProtocolConstraintTests
{
    private const string Declarations = """
        protocol Describable { func description() -> String }
        struct Good: Describable { func description() -> String { return "good" } }
        struct Lookalike { func description() -> String { return "lookalike" } }
        func describe<T: Describable>(_ value: T) -> String { return value.description() }
        """;

    [Fact]
    public void ExplicitConformanceSatisfiesCallableConstraint()
    {
        var compilation = Compile(Declarations + " func use(_ value: Good) -> String { return describe(value) }");

        Assert.Empty(compilation.Diagnostics);
        var conformance = Assert.Single(compilation.GetConformances());
        var function = compilation.BindProgram().Functions.Single(f => f.Name == "describe");
        var result = new GenericConstraintValidator(compilation.GetConformances())
            .Validate(Assert.Single(function.TypeParameters), conformance.Type);

        Assert.True(result.Succeeded);
        Assert.Same(conformance, Assert.Single(result.Proofs).Conformance);
    }

    [Fact]
    public void StructuralLookalikeDoesNotSatisfyCallableConstraint()
    {
        var compilation = Compile(Declarations + " func use(_ value: Lookalike) -> String { return describe(value) }");

        var diagnostic = Assert.Single(compilation.Diagnostics.Where(d => d.Code == "MRT2304"));
        Assert.Contains("Lookalike", diagnostic.Message);
        Assert.Contains("Describable", diagnostic.Message);
    }

    [Fact]
    public void OpenParameterMustCarryACompatibleConstraint()
    {
        var valid = Compile(Declarations + " func forward<U: Describable>(_ value: U) -> String { return describe(value) }");
        var invalid = Compile(Declarations + " func forward<U>(_ value: U) -> String { return describe(value) }");

        Assert.DoesNotContain(valid.Diagnostics, d => d.Code == "MRT2304");
        Assert.Contains(invalid.Diagnostics, d => d.Code == "MRT2304");
    }

    [Fact]
    public void ConstrainedMemberLookupRetainsRequirementIdentity()
    {
        var compilation = Compile(Declarations);

        Assert.Empty(compilation.Diagnostics);
        var program = compilation.BindProgram();
        var function = program.Functions.Single(f => f.Name == "describe");
        var body = program.FunctionBodies[function];
        var call = Assert.IsType<BoundProtocolRequirementCallExpression>(
            Assert.IsType<BoundReturnStatement>(Assert.Single(body.Statements)).Expression);

        Assert.Equal("description", call.Requirement.Name);
        Assert.Same(Assert.Single(program.Conformances).Protocol, call.Requirement.ContainingProtocol);
    }

    private static Compilation Compile(string source) =>
        Compilation.Create(SyntaxTree.Parse(source, "constraints.martin"));
}
