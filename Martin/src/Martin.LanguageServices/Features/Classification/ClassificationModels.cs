using Martin.Compiler.Text;

namespace Martin.LanguageServices;

public enum ClassificationKind
{
    Keyword, Comment, DocumentationComment, NumberLiteral, StringLiteral, Operator, Punctuation,
    Type, Struct, Class, Enum, EnumCase, Protocol, Function, Method, Initializer, Property,
    Parameter, Local, Global, MutableVariable, ImmutableVariable, BuiltIn, TypeParameter,
    PatternVariable, ProtocolRequirement, ProtocolWitness, ProtocolConformance, UnresolvedIdentifier
}

[Flags]
public enum ClassificationModifiers
{
    None = 0, Declaration = 1, Definition = 2, ReadOnly = 4, Static = 8,
    DefaultLibrary = 16, Documentation = 32, Unresolved = 64
}

public sealed record ClassifiedSpan(
    TextSpan Span,
    ClassificationKind Kind,
    ClassificationModifiers Modifiers = ClassificationModifiers.None);

public sealed record ClassificationRequest : LanguageRequest;
