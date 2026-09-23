namespace Forma.Locali.Tests.Support.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Builds compilations a test can walk.</summary>
public static class CompilationTestFactory
{
    private static readonly MetadataReference[] Platform =
    [
        .. ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    /// <summary>A compilation carrying no references, so no symbol in it binds to anything.</summary>
    /// <param name="sources">The C# sources to parse.</param>
    public static Compilation Unreferenced(params string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return CSharpCompilation.Create(
            "Scratch",
            sources.Select(source => CSharpSyntaxTree.ParseText(source)));
    }

    /// <summary>A compilation carrying no references, building the assembly named, so the projects of a run can be told apart by name.</summary>
    /// <param name="assemblyName">The name of the assembly the compilation builds.</param>
    /// <param name="sources">The C# sources to parse.</param>
    public static Compilation Building(string assemblyName, params string[] sources)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        ArgumentNullException.ThrowIfNull(sources);

        return CSharpCompilation.Create(
            assemblyName,
            sources.Select(source => CSharpSyntaxTree.ParseText(source)));
    }

    /// <summary>A compilation carrying everything the test run loaded, so the localizer contract binds.</summary>
    /// <param name="sources">The C# sources to parse.</param>
    public static Compilation Referenced(params string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return CSharpCompilation.Create(
            "Scratch",
            sources.Select(source => CSharpSyntaxTree.ParseText(source, path: $"{Guid.NewGuid():N}.cs")),
            Platform,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
