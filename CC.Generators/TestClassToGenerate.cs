using Microsoft.CodeAnalysis;

namespace CC.Generators;

public record TestClassToGenerate(string Name, string Accessibility, ITypeSymbol TargetType)

{
    public readonly string Name = Name;

    public readonly ITypeSymbol TargetType = TargetType;

    public readonly string Accessibility = Accessibility;

    public INamedTypeSymbol? ResultType { get; set; }
    public IEnumerable<string>? Usings { get; set; }
    public string? Namespace { get; set; }
}