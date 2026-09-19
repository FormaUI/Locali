namespace Forma.Locali.Projects;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

/// <summary>Builds the compilation a compiler command line describes, with its source generators run, as the compiler itself would.</summary>
internal static class CompilerCommandLine
{
    // Every project of a run references the same framework assemblies, which are read from disk once.
    private static readonly ConcurrentDictionary<(string Path, MetadataReferenceProperties Properties), MetadataReference> References = new();

    /// <summary>The compilation the arguments describe, its generators' output included.</summary>
    /// <param name="arguments">The arguments the project's build would hand the compiler.</param>
    /// <param name="projectDirectory">The folder relative paths among the arguments start from.</param>
    /// <param name="projectReferences">The compilation of each referenced project whose built assembly is missing, keyed by that assembly's path.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static Compilation Compile(
        IReadOnlyList<string> arguments,
        string projectDirectory,
        IReadOnlyDictionary<string, Compilation> projectReferences,
        CancellationToken cancellationToken)
    {
        var parsed = CSharpCommandLineParser.Default.Parse(arguments, projectDirectory, sdkDirectory: null);

        // Parsing is processor-bound and each file is independent, so the trees are built across cores.
        var trees = parsed.SourceFiles
            .Where(file => File.Exists(file.Path))
            .AsParallel()
            .AsOrdered()
            .WithCancellation(cancellationToken)
            .Select(file => CSharpSyntaxTree.ParseText(Read(file.Path), parsed.ParseOptions, file.Path, cancellationToken));

        var compilation = CSharpCompilation.Create(
            parsed.CompilationName,
            trees,
            GetReferences(parsed, projectReferences),
            parsed.CompilationOptions);

        var loader = new GeneratorLoader();
        var generators = parsed.AnalyzerReferences
            .Where(reference => File.Exists(reference.FilePath))
            .SelectMany(reference => new AnalyzerFileReference(reference.FilePath, loader).GetGenerators(LanguageNames.CSharp))
            .ToImmutableArray();

        if (generators.IsEmpty)
        {
            return compilation;
        }

        var additionalTexts = parsed.AdditionalFiles
            .Where(file => File.Exists(file.Path))
            .Select(file => (AdditionalText)new FileAdditionalText(file.Path))
            .ToImmutableArray();

        var configs = AnalyzerConfigSet.Create(
            parsed.AnalyzerConfigPaths.Where(File.Exists).Select(path => AnalyzerConfig.Parse(Read(path), path)).ToList());

        CSharpGeneratorDriver
            .Create(generators, additionalTexts, parsed.ParseOptions, new ConfigOptionsProvider(configs))
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _, cancellationToken);

        return generated;
    }

    // A referenced project that was never built has no assembly on disk, so its own compilation stands in for it.
    private static IEnumerable<MetadataReference> GetReferences(CSharpCommandLineArguments parsed, IReadOnlyDictionary<string, Compilation> projectReferences)
    {
        foreach (var reference in parsed.MetadataReferences)
        {
            if (File.Exists(reference.Reference))
            {
                yield return References.GetOrAdd((reference.Reference, reference.Properties), static key => MetadataReference.CreateFromFile(key.Path, key.Properties));
            }
            else if (projectReferences.TryGetValue(reference.Reference, out var referenced))
            {
                yield return referenced.ToMetadataReference(reference.Properties.Aliases, reference.Properties.EmbedInteropTypes);
            }
        }
    }

    private static SourceText Read(string path)
    {
        using var stream = File.OpenRead(path);

        return SourceText.From(stream);
    }

    // A generator's dependencies sit beside it, which loading it from its own path resolves.
    private sealed class GeneratorLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath)
            => Assembly.LoadFrom(fullPath);
    }

    private sealed class FileAdditionalText(string path) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default)
            => Read(Path);
    }

    // The build writes what a generator reads about the project and each file into config files, which the compiler serves back through this.
    private sealed class ConfigOptionsProvider(AnalyzerConfigSet configs) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConfigOptions(configs.GlobalConfigOptions.AnalyzerOptions);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
            => new ConfigOptions(configs.GetOptionsForSourcePath(tree.FilePath).AnalyzerOptions);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => new ConfigOptions(configs.GetOptionsForSourcePath(textFile.Path).AnalyzerOptions);
    }

    private sealed class ConfigOptions(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
            => values.TryGetValue(key, out value!);
    }
}
