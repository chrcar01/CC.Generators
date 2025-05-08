using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.CodeDom.Compiler;
using System.Text;

namespace CC.Generators
{
    [Generator]
    public class CreatorGenerator : IIncrementalGenerator
    {
        public const string Attribute = @"#nullable enable
using System;
namespace CC.Generators
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CreatorAttribute : Attribute
    {        
        public Type? Target;
    }
}";
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx => ctx.AddSource(
                "CreatorAttribute.g.cs", SourceText.From(Attribute, Encoding.UTF8)));

            IncrementalValuesProvider<TestClassToGenerate?> testClassesToGenerate = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => IsSyntaxTargetForGeneration(s),
                    transform: (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(m => m is not null);

            context.RegisterSourceOutput(testClassesToGenerate, (source, testClassToGenerate) =>
            {
                var targetType = testClassToGenerate?.TargetType;
                if (targetType == null)
                {
                    return;
                }

                // this is where we need to generate the partial test class
                using var stringWriter = new StringWriter();
                using var code = new IndentedTextWriter(stringWriter);
                code.Indent = 0;
                code.WriteLine("#nullable enable");
                var usings = (testClassToGenerate?.Usings ?? []).ToList();
                if (!usings.Contains("Moq"))
                {
                    usings.Add("Moq");
                }

                var resultType = testClassToGenerate?.ResultType;
                if (resultType != null)
                {
                    var lastIndexOf = resultType.ToDisplayString().LastIndexOf('.');
                    if (lastIndexOf > 0)
                    {
                        var usingNamespace = resultType.ToDisplayString().Substring(0, lastIndexOf);
                        if (!usings.Contains(usingNamespace))
                        {
                            usings.Add(usingNamespace);
                        }
                    }
                }
                var @params = GetConstructorWithMostParameters(targetType).ToArray();
                foreach (var param in @params)
                {
                    var lastIndexOf = param.ToDisplayString().LastIndexOf('.');
                    if (lastIndexOf > 0)
                    {
                        var usingNamespace = param.ToDisplayString().Substring(0, lastIndexOf);
                        if (!usings.Contains(usingNamespace))
                        {
                            usings.Add(usingNamespace);
                        }
                    }
                }
                foreach (var @using in usings.Distinct())
                {
                    code.WriteLine($"using {@using};");
                }
                
                code.WriteLine();
                code.WriteLine($"namespace {testClassToGenerate?.Namespace}");
                code.WriteLine("{");
                code.Indent = 1;
                var accessibility = "public";
                if (!string.IsNullOrWhiteSpace(testClassToGenerate?.Accessibility))
                {
                    accessibility = testClassToGenerate?.Accessibility;
                }
                code.WriteLine($"{accessibility} partial class {testClassToGenerate?.Name}");
                code.WriteLine("{");
                code.Indent = 2;
                var resultTypeName = testClassToGenerate?.ResultType?.Name ?? string.Empty;
                code.Write($"private static {resultTypeName} Create{targetType.Name}(");
                code.WriteLine("MockBehavior defaultBehavior = MockBehavior.Loose,");
                code.Indent++;
                
                for (var paramIndex=0;paramIndex<@params.Length;paramIndex++)
                {
                    var param = @params[paramIndex];
                    code.Write($"{param.Type.Name}? {param.Name} = null");
                    code.WriteLine(paramIndex < @params.Length - 1 ? "," : "");
                }

                code.Indent--;
                code.WriteLine(")");
                code.WriteLine("{");
                code.Indent++;
                code.WriteLine($"return new {targetType.Name}(");
                code.Indent++;
                for (var paramIndex = 0; paramIndex < @params.Length; paramIndex++)
                {
                    var param = @params[paramIndex];
                    code.Write($"{param.Name}");
                    if (param.Type.IsValueType && param.Type.Name != "String")
                    {
                        code.Write(" ?? default");
                    }
                    else if (param.Type.Name == "String")
                    {
                        code.Write(" ?? string.Empty");
                    }
                    else
                    {
                        code.Write(" ?? Mock.Of<" + param.Type.Name + ">(defaultBehavior)");
                    }

                    code.WriteLine(paramIndex < @params.Length - 1 ? "," : "");
                }
                code.Indent--;
                code.WriteLine(");");
                code.Indent--;
                code.WriteLine("}");
                code.Indent = 1;
                code.WriteLine("}");
                code.Indent = 0;
                code.WriteLine("}");

                source.AddSource($"{testClassToGenerate?.Name}.g.cs", SourceText.From(stringWriter.ToString(), Encoding.UTF8));
            });
        }
        
        private TestClassToGenerate? GetSemanticTargetForGeneration(GeneratorSyntaxContext ctx)
        {
            var classDeclarationSyntax = (ClassDeclarationSyntax)ctx.Node;
            
            foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    // Check if the attribute is CreatorAttribute
                    var symbol = ctx.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol;
                    if (symbol is not IMethodSymbol attributeSymbol)
                    {
                        continue;
                    }

                    INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                    if (attributeContainingTypeSymbol.ToDisplayString() != "CC.Generators.CreatorAttribute")
                    {
                        continue;
                    }

                    // Extract the named argument "Target"
                    if (attributeSyntax.ArgumentList == null)
                    {
                        continue;
                    }

                    foreach (var argument in attributeSyntax.ArgumentList.Arguments)
                    {
                        if (argument.NameEquals?.Name.Identifier.Text == "Target")
                        {
                            // Get the typeof argument
                            if (argument.Expression is TypeOfExpressionSyntax expression)
                            {
                                var typeSymbol = ctx.SemanticModel.GetTypeInfo(expression.Type).Type;
                                if (typeSymbol != null)
                                {
                                    return GetTestClassToGenerate(ctx.SemanticModel, classDeclarationSyntax,
                                        typeSymbol);
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private string? GetNamespace(ClassDeclarationSyntax classDeclarationSyntax)
        {
            // Traverse up the syntax tree to find the enclosing NamespaceDeclarationSyntax
            SyntaxNode? parent = classDeclarationSyntax.Parent;
            while (parent != null && parent is not NamespaceDeclarationSyntax && parent is not FileScopedNamespaceDeclarationSyntax)
            {
                parent = parent.Parent;
            }

            // Return the namespace name if found
            if (parent is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                return namespaceDeclaration.Name.ToString();
            }

            // Return null if no namespace is found (e.g., for top-level statements)
            return null;
        }

        private IEnumerable<IParameterSymbol> GetConstructorWithMostParameters(ITypeSymbol targetType)
        {
            if (targetType is not INamedTypeSymbol namedTypeSymbol)
            {
                return [];
            }

            // Find the constructor with the most parameters
            var constructorWithMostParameters = namedTypeSymbol.Constructors
                .Where(constructor => !constructor.IsStatic) // Exclude static constructors
                .OrderByDescending(constructor => constructor.Parameters.Length) // Sort by parameter count
                .FirstOrDefault(); // Get the constructor with the most parameters

            return constructorWithMostParameters?.Parameters.ToList() ?? [];
        }


        private TestClassToGenerate? GetTestClassToGenerate(SemanticModel semanticModel,
            ClassDeclarationSyntax classDeclarationSyntax, ITypeSymbol targetType)
        {
            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
            {
                return null; // when would this happen???
            }

            var accessibility = classSymbol.DeclaredAccessibility.ToString().ToLower();
            var result = new TestClassToGenerate(classSymbol.Name, accessibility, targetType)
            {
                ResultType = targetType.AllInterfaces.FirstOrDefault(x => x.Name == "I" + targetType.Name) ?? targetType as INamedTypeSymbol,
                Usings = GetUsingNamespaces(classDeclarationSyntax),
                Namespace = GetNamespace(classDeclarationSyntax)
            };

            return result;
        }

        private IEnumerable<string> GetUsingNamespaces(ClassDeclarationSyntax classDeclarationSyntax)
        {
            // Traverse up to the CompilationUnitSyntax
            if (classDeclarationSyntax.SyntaxTree.GetRoot() is not CompilationUnitSyntax compilationUnit)
            {
                return [];
            }

            // Extract namespaces from using directives
            return compilationUnit.Usings
                .Select(usingDirective => usingDirective.Name.ToString());
        }

        private bool IsSyntaxTargetForGeneration(SyntaxNode syntaxNode)
        {
            var result = syntaxNode is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
            return result;
        }
    }

    public record TestClassToGenerate(string Name, string Accessibility, ITypeSymbol TargetType)

    {
        public readonly string Name = Name;

        public readonly ITypeSymbol TargetType = TargetType;

        public readonly string Accessibility = Accessibility;

        public INamedTypeSymbol? ResultType { get; set; }
        public IEnumerable<string>? Usings { get; set; }
        public string? Namespace { get; set; }
    }
}
