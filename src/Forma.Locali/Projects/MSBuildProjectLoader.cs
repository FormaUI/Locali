namespace Forma.Locali.Projects;

using Forma.Locali.Cli;

using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;

using ProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;

/// <summary>Loads projects through MSBuild, with the Razor generator's C# in each compilation.</summary>
/// <param name="options">What the command line asked for, the properties every project is loaded with among it.</param>
public sealed class MSBuildProjectLoader(IOptions<LinterOptions> options) : IProjectLoader
{
    private const string RazorSuffix = "_razor.g.cs";
    private const string PrepareResourceNamesTarget = "PrepareResourceNames";
    private const string ReferenceArgument = "/reference:";

    // The build hands each language's compiler its arguments under an item named for that compiler.
    private static readonly Dictionary<string, ProjectLanguage> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csproj"] = new(LanguageNames.CSharp, "CscCommandLineArgs"),
        [".vbproj"] = new(LanguageNames.VisualBasic, "VbcCommandLineArgs"),
    };

    // The targets run in one build: a second build of the same instance fails, and the compiler's arguments with it.
    private static readonly string[] Targets = [PrepareResourceNamesTarget, "Compile", "CoreCompile"];

    // A design-time build runs the project's own targets up to the compiler and stops short of it, handing back what it would have been given.
    private static readonly Dictionary<string, string> DesignTimeProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DesignTimeBuild"] = "true",
        ["BuildingInsideVisualStudio"] = "true",
        ["BuildProjectReferences"] = "false",
        ["SkipCompilerExecution"] = "true",
        ["ProvideCommandLineArgs"] = "true",
    };

    private readonly IReadOnlyDictionary<string, string> _properties = options?.Value.Properties ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LoadedProject>> LoadAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // One collection serves the whole load, so the SDK's targets are parsed once rather than once per project.
        using var collection = new ProjectCollection(GetBaseProperties());
        var run = new LoadRun(collection);
        var loaded = new List<LoadedProject>();

        foreach (var path in paths.SelectMany(GetProjectPaths).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A project in another language is one the linter cannot read, and stands in the run as one that did not load, so what it builds is not taken for absent.
            if (!Languages.TryGetValue(Path.GetExtension(path), out var language))
            {
                loaded.Add(new LoadedProject(path, null, HasGeneratedSources: false, HasRazorMarkup: false));
                continue;
            }

            if (Evaluate(path, language, run, cancellationToken) is { } project)
            {
                loaded.Add(project.Loaded);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<LoadedProject>>(loaded);
    }

    private static IEnumerable<string> GetProjectPaths(string path)
        => Path.GetExtension(path).StartsWith(".sln", StringComparison.OrdinalIgnoreCase)
            ? SolutionFile.Parse(Path.GetFullPath(path)).ProjectsInOrder
                .Where(project => project.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
                .Select(project => project.AbsolutePath)
            : [path];

    // One evaluation answers for the compilation and the embedded names alike, so the two never describe different builds.
    private EvaluatedProject? Evaluate(string path, ProjectLanguage language, LoadRun run, CancellationToken cancellationToken, string? targetFramework = null)
    {
        if (run.Evaluated.TryGetValue(path, out var known))
        {
            return known;
        }

        // A project met again while it is still being evaluated references itself in a circle, and is left out of it.
        run.Evaluated[path] = null;

        var properties = GetBaseProperties();
        if (targetFramework is not null)
        {
            properties["TargetFramework"] = targetFramework;
        }

        ProjectInstance instance;
        try
        {
            instance = run.Collection.LoadProject(path, properties, toolsVersion: null).CreateProjectInstance();
        }
        catch (InvalidProjectFileException)
        {
            return run.Evaluated[path] = new EvaluatedProject(new LoadedProject(path, null, HasGeneratedSources: false, HasMarkup(path, language)), null, null);
        }

        // A project built for several frameworks compiles once per framework, so the first stands for it.
        if (targetFramework is null
            && instance.GetPropertyValue("TargetFramework").Length == 0
            && instance.GetPropertyValue("TargetFrameworks").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is [var first, ..])
        {
            run.Evaluated.Remove(path);

            return Evaluate(path, language, run, cancellationToken, first);
        }

        // Each target answers for itself, so a project that does not compile may still name what it embeds, and the reverse.
        _ = instance.Build(Targets, loggers: null, out var results);
        var resources = results.TryGetValue(PrepareResourceNamesTarget, out var naming) && naming.ResultCode == TargetResultCode.Success
            ? GetResources(instance)
            : null;

        var arguments = instance.GetItems(language.ArgumentsItem).Select(item => item.EvaluatedInclude).ToArray();

        var rootNamespace = instance.GetPropertyValue("RootNamespace") is { Length: > 0 } declared ? declared : null;
        var isLocalePack = instance.GetPropertyValue("IsLocalePack").Equals("true", StringComparison.OrdinalIgnoreCase);
        var compilation = arguments.Length == 0
            ? null
            : CompilerCommandLine.Compile(arguments, Path.GetDirectoryName(path)!, GetProjectReferences(instance, language, arguments, run, cancellationToken), language.Name, cancellationToken);

        var loaded = Read(path, language, compilation, rootNamespace, resources?.Names, resources?.SatelliteFiles, isLocalePack, cancellationToken);

        return run.Evaluated[path] = new EvaluatedProject(loaded, instance.GetPropertyValue("TargetPath"), instance.GetPropertyValue("TargetRefPath"));
    }

    // A referenced project is evaluated only where the assembly it builds is missing, which is a tree that was never built,
    // and only a compilation in the referencing project's own language can stand in, since Roslyn takes no other's as a reference.
    private Dictionary<string, Compilation> GetProjectReferences(
        ProjectInstance instance,
        ProjectLanguage language,
        IReadOnlyList<string> arguments,
        LoadRun run,
        CancellationToken cancellationToken)
    {
        var references = new Dictionary<string, Compilation>(StringComparer.OrdinalIgnoreCase);

        var isAnyMissing = arguments
            .Where(argument => argument.StartsWith(ReferenceArgument, StringComparison.OrdinalIgnoreCase))
            .Any(argument => !File.Exists(argument[ReferenceArgument.Length..].Trim('"')));

        if (!isAnyMissing)
        {
            return references;
        }

        foreach (var reference in instance.GetItems("ProjectReference"))
        {
            var referencePath = Path.GetFullPath(reference.GetMetadataValue("FullPath"));
            if (!Languages.TryGetValue(Path.GetExtension(referencePath), out var referencedLanguage)
                || referencedLanguage.Name != language.Name
                || Evaluate(referencePath, referencedLanguage, run, cancellationToken) is not { Loaded.Compilation: { } compilation } referenced)
            {
                continue;
            }

            foreach (var assembly in new[] { referenced.TargetPath, referenced.TargetRefPath })
            {
                if (!string.IsNullOrEmpty(assembly))
                {
                    references[assembly] = compilation;
                }
            }
        }

        return references;
    }

    private static LoadedProject Read(
        string path,
        ProjectLanguage language,
        Compilation? compilation,
        string? rootNamespace,
        IReadOnlyDictionary<string, string>? resourceNames,
        IReadOnlySet<string>? satelliteFiles,
        bool isLocalePack,
        CancellationToken cancellationToken)
    {
        var markup = HasMarkup(path, language);
        if (compilation is null)
        {
            return new LoadedProject(path, null, HasGeneratedSources: false, markup, rootNamespace, resourceNames, isLocalePack, satelliteFiles);
        }

        // Razor generates C# alone, so no other language's project has generated sources to look for.
        if (language.Name != LanguageNames.CSharp)
        {
            return new LoadedProject(path, compilation, HasGeneratedSources: false, markup, rootNamespace, resourceNames, isLocalePack, satelliteFiles);
        }

        // The generator ran with the compilation, in the compiler it was built against, which needs nothing on disk.
        if (compilation.SyntaxTrees.Any(tree => tree.FilePath.EndsWith(RazorSuffix, StringComparison.OrdinalIgnoreCase)))
        {
            return new LoadedProject(path, compilation, HasGeneratedSources: true, markup, rootNamespace, resourceNames, isLocalePack, satelliteFiles);
        }

        var emitted = Emitted(Path.GetDirectoryName(path)!);
        if (emitted.Count == 0)
        {
            return new LoadedProject(path, compilation, HasGeneratedSources: false, markup, rootNamespace, resourceNames, isLocalePack, satelliteFiles);
        }

        var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;

        return new LoadedProject(path, compilation.AddSyntaxTrees(Parse(emitted, parseOptions, cancellationToken)), HasGeneratedSources: true, markup, rootNamespace, resourceNames, isLocalePack, satelliteFiles);
    }

    // Razor markup compiles to C#, so under any other language it is not the project's to lint.
    private static bool HasMarkup(string path, ProjectLanguage language)
        => language.Name == LanguageNames.CSharp
            && Path.GetDirectoryName(path) is { } folder
            && Directory.EnumerateFiles(folder, "*.razor", SearchOption.AllDirectories).Any();

    // Whether a file is embedded or copied beside the application is the project's items to say, and the build's own naming targets say what each is embedded as; and where; no folder's name can.
    private static EmbeddedResources GetResources(ProjectInstance instance)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var satelliteFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in instance.GetItems("EmbeddedResource"))
        {
            var fullPath = Path.GetFullPath(item.GetMetadataValue("FullPath"));
            names[fullPath] = GetResourceName(item, out var isSatellite);
            if (isSatellite)
            {
                satelliteFiles.Add(fullPath);
            }
        }

        return new EmbeddedResources(names, satelliteFiles);
    }

    // A file the build moves into a satellite assembly is named there without its culture, which the runtime reads back in just before the extension.
    private static string GetResourceName(ProjectItemInstance item, out bool isSatellite)
    {
        var logicalName = item.GetMetadataValue("LogicalName");
        var name = logicalName.Length > 0 ? logicalName : item.GetMetadataValue("ManifestResourceName");
        var culture = item.GetMetadataValue("Culture");

        isSatellite = culture.Length > 0 && !item.GetMetadataValue("WithCulture").Equals("false", StringComparison.OrdinalIgnoreCase);
        if (!isSatellite)
        {
            return name;
        }

        var extension = Path.GetExtension(name);

        return $"{name[..^extension.Length]}.{culture}{extension}";
    }

    // Parsing is processor-bound and each file is independent, so the trees are built across cores.
    private static ParallelQuery<SyntaxTree> Parse(IReadOnlyList<string> files, CSharpParseOptions? parseOptions, CancellationToken cancellationToken)
        => files
            .AsParallel()
            .WithCancellation(cancellationToken)
            .Select(file =>
            {
                using var stream = File.OpenRead(file);

                return CSharpSyntaxTree.ParseText(SourceText.From(stream), parseOptions, file, cancellationToken);
            });

    // Read from where a build emitted it, for a project whose generator did not run here.
    private static IReadOnlyList<string> Emitted(string folder)
    {
        var generated = Path.Combine(folder, "obj");

        return Directory.Exists(generated)
            ? [.. Directory.EnumerateFiles(generated, "*.g.cs", SearchOption.AllDirectories)
                .Where(static file => file.Contains($"{Path.DirectorySeparatorChar}generated{Path.DirectorySeparatorChar}", StringComparison.Ordinal))]
            : [];
    }

    // The properties every project of the run is evaluated under: the design-time ones, and the command line's over them.
    private Dictionary<string, string> GetBaseProperties()
    {
        var properties = new Dictionary<string, string>(DesignTimeProperties, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in _properties)
        {
            properties[name] = value;
        }

        return properties;
    }

    private sealed record EvaluatedProject(LoadedProject Loaded, string? TargetPath, string? TargetRefPath);

    // The collection every project of one load is evaluated in, and what the load has evaluated so far.
    private sealed record LoadRun(ProjectCollection Collection)
    {
        public Dictionary<string, EvaluatedProject?> Evaluated { get; } = [with(StringComparer.OrdinalIgnoreCase)];
    }

    // The language as Roslyn names it, and the build item its compiler's arguments come back under.
    private sealed record ProjectLanguage(string Name, string ArgumentsItem);

    private sealed record EmbeddedResources(IReadOnlyDictionary<string, string> Names, IReadOnlySet<string> SatelliteFiles);
}
