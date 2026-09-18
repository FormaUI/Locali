namespace Forma.Locali.Projects;

using Microsoft.Build.Exceptions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

using ProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;

/// <summary>Loads projects through MSBuild, with the Razor generator's C# in each compilation.</summary>
public sealed class MSBuildProjectLoader : IProjectLoader
{
    private const string RazorSuffix = "_razor.g.cs";
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<LoadedProject>> LoadAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var requested = paths.ToArray();
        if (requested.Length == 0)
        {
            return [];
        }

        using var workspace = MSBuildWorkspace.Create();
        var opened = new List<ProjectId>();

        foreach (var path in requested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            opened.AddRange(await OpenAsync(workspace, path, cancellationToken).ConfigureAwait(false));
        }

        var loaded = new List<LoadedProject>();
        foreach (var id in opened.Distinct())
        {
            if (workspace.CurrentSolution.GetProject(id) is { } project)
            {
                loaded.Add(await ReadAsync(project, cancellationToken).ConfigureAwait(false));
            }
        }

        return loaded;
    }

    private static async ValueTask<IReadOnlyList<ProjectId>> OpenAsync(MSBuildWorkspace workspace, string path, CancellationToken cancellationToken)
    {
        if (Path.GetExtension(path).StartsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var solution = await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);

            return [.. solution.ProjectIds];
        }

        // Opening a project pulls the ones it references in with it, and opening those again throws.
        if (Find(workspace.CurrentSolution, path) is { } already)
        {
            return [already];
        }

        var project = await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);

        return [project.Id];
    }

    private static ProjectId? Find(Solution solution, string path)
    {
        var full = Path.GetFullPath(path);

        return solution.Projects
            .FirstOrDefault(project => project.FilePath is { } file
                && string.Equals(Path.GetFullPath(file), full, StringComparison.OrdinalIgnoreCase))?
            .Id;
    }

    private static async ValueTask<LoadedProject> ReadAsync(Project project, CancellationToken cancellationToken)
    {
        var file = project.FilePath ?? project.Name;
        var folder = project.FilePath is null ? null : Path.GetDirectoryName(project.FilePath);
        var markup = folder is not null && Directory.EnumerateFiles(folder, "*.razor", SearchOption.AllDirectories).Any();
        var root = string.IsNullOrEmpty(project.DefaultNamespace) ? null : project.DefaultNamespace;
        var embedded = ReadEmbeddedFiles(project.FilePath);

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return new LoadedProject(file, null, HasGeneratedSources: false, markup, root, embedded);
        }

        // The generator runs in the workspace when Roslyn is the SDK's own, which needs nothing on disk.
        var generated = await project.GetSourceGeneratedDocumentsAsync(cancellationToken).ConfigureAwait(false);
        if (generated.Any(document => document.HintName.EndsWith(RazorSuffix, StringComparison.OrdinalIgnoreCase)))
        {
            return new LoadedProject(file, compilation, HasGeneratedSources: true, markup, root, embedded);
        }

        var emitted = folder is null ? [] : Emitted(folder);
        if (emitted.Count == 0)
        {
            return new LoadedProject(file, compilation, HasGeneratedSources: false, markup, root, embedded);
        }

        var options = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        var trees = await ParseAsync(emitted, options, cancellationToken).ConfigureAwait(false);

        return new LoadedProject(file, compilation.AddSyntaxTrees(trees), HasGeneratedSources: true, markup, root, embedded);
    }

    // Whether a file is embedded or copied beside the application is the project's items to say; no folder's name can.
    private static HashSet<string>? ReadEmbeddedFiles(string? projectPath)
    {
        if (projectPath is null)
        {
            return null;
        }

        using var collection = new ProjectCollection();

        try
        {
            return collection.LoadProject(projectPath)
                .GetItems("EmbeddedResource")
                .Select(item => Path.GetFullPath(item.GetMetadataValue("FullPath")))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (InvalidProjectFileException)
        {
            return null;
        }
    }

    private static async ValueTask<IReadOnlyList<SyntaxTree>> ParseAsync(
        IReadOnlyList<string> files,
        CSharpParseOptions? options,
        CancellationToken cancellationToken)
    {
        var sources = await Task.WhenAll(files.Select(async file =>
        {
            var bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);

            return (file, text: SourceText.From(new MemoryStream(bytes, writable: false)));
        })).ConfigureAwait(false);

        // Parsing is processor-bound and each file is independent, so the trees are built across cores.
        return [.. sources
            .AsParallel()
            .WithCancellation(cancellationToken)
            .Select(source => CSharpSyntaxTree.ParseText(source.text, options, source.file))];
    }

    // Read from where a build emitted it when the SDK ships no Roslyn new enough to run the generator here.
    private static IReadOnlyList<string> Emitted(string folder)
    {
        var generated = Path.Combine(folder, "obj");

        return Directory.Exists(generated)
            ? [.. Directory.EnumerateFiles(generated, "*.g.cs", SearchOption.AllDirectories)
                .Where(static file => file.Contains($"{Path.DirectorySeparatorChar}generated{Path.DirectorySeparatorChar}", StringComparison.Ordinal))]
            : [];
    }
}
