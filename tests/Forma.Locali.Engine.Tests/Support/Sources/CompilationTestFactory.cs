namespace Forma.Locali.Tests.Support.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.VisualBasic;

/// <summary>Builds compilations a test can walk.</summary>
public static class CompilationTestFactory
{
    private static readonly MetadataReference[] Platform =
    [
        .. ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    /// <summary>A library's compilation carrying no references, so no symbol in it binds to anything.</summary>
    /// <param name="sources">The C# sources to parse.</param>
    public static Compilation Unreferenced(params string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return CSharpCompilation.Create(
            "Scratch",
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>A Visual Basic library's compilation carrying no references, so no symbol in it binds to anything.</summary>
    /// <param name="sources">The Visual Basic sources to parse.</param>
    public static Compilation UnreferencedVisualBasic(params string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return VisualBasicCompilation.Create(
            "Scratch",
            sources.Select(source => VisualBasicSyntaxTree.ParseText(source)),
            options: new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>A library's compilation carrying no references, building the assembly named, so the projects of a run can be told apart by name.</summary>
    /// <param name="assemblyName">The name of the assembly the compilation builds.</param>
    /// <param name="sources">The C# sources to parse.</param>
    public static Compilation Building(string assemblyName, params string[] sources)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        ArgumentNullException.ThrowIfNull(sources);

        return CSharpCompilation.Create(
            assemblyName,
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
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

    /// <summary>A compilation carrying everything the test run loaded, building the assembly named from the C# files at the paths, so a run naming those files reports on them.</summary>
    /// <param name="assemblyName">The name of the assembly the compilation builds.</param>
    /// <param name="paths">The C# files to parse.</param>
    public static Compilation ReferencedFiles(string assemblyName, params string[] paths)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        ArgumentNullException.ThrowIfNull(paths);

        return CSharpCompilation.Create(
            assemblyName,
            paths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)),
            Platform,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>The reference assembly a compilation builds, holding its metadata and no member's body, as the build hands one project another's.</summary>
    /// <param name="compilation">The compilation building the assembly.</param>
    public static MetadataReference MetadataOf(Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        using var image = new MemoryStream();
        Emit(compilation, image, metadataOnly: true);

        return MetadataReference.CreateFromImage(image.ToArray());
    }

    /// <summary>A reference to the assembly a compilation builds, written to a file as the build writes it, holding no member's body where it is a reference assembly.</summary>
    /// <param name="compilation">The compilation building the assembly.</param>
    /// <param name="path">The file to write the assembly to.</param>
    /// <param name="metadataOnly">Whether the assembly is a reference assembly, holding its metadata alone.</param>
    public static MetadataReference WrittenTo(Compilation compilation, string path, bool metadataOnly = false)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrEmpty(path);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var file = File.Create(path))
        {
            Emit(compilation, file, metadataOnly);
        }

        return MetadataReference.CreateFromFile(path);
    }

    private static void Emit(Compilation compilation, Stream destination, bool metadataOnly)
    {
        var emitted = compilation.Emit(destination, options: new EmitOptions(metadataOnly: metadataOnly));
        if (!emitted.Success)
        {
            throw new InvalidOperationException($"'{compilation.AssemblyName}' does not compile: {string.Join(' ', emitted.Diagnostics)}");
        }
    }

    /// <summary>A Visual Basic compilation carrying everything the test run loaded, so the localizer contract binds.</summary>
    /// <param name="sources">The Visual Basic sources to parse.</param>
    public static Compilation ReferencedVisualBasic(params string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return VisualBasicCompilation.Create(
            "Scratch",
            sources.Select(source => VisualBasicSyntaxTree.ParseText(source, path: $"{Guid.NewGuid():N}.vb")),
            Platform,
            new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
