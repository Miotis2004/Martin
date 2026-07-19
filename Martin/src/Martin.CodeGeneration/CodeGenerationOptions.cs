using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.CodeGeneration;

public enum OutputKind
{
    ConsoleApplication,
    Library
}
