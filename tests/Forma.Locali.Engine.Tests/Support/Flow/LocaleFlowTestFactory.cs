namespace Forma.Locali.Tests.Support.Flow;

using Forma.Locali.Flow;
using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

/// <summary>Finds a compilation's lookups and follows each to its types, as a run of the linter does once every project is loaded.</summary>
public static class LocaleFlowTestFactory
{
    /// <summary>Every lookup the compilation makes, resolved over a run holding it and the other compilations given, each a project of its own.</summary>
    /// <param name="compilation">The compilation whose lookups are resolved.</param>
    /// <param name="others">The other compilations of the run.</param>
    public static IReadOnlyList<LocalizerLookup> Resolve(Compilation compilation, params IReadOnlyList<Compilation> others)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(others);

        var project = new LoadedProject("Scratch.csproj", compilation, false, false);

        return Resolve(project, [project, .. others.Select((other, index) => new LoadedProject($"Other{index}.csproj", other, false, false))]);
    }

    /// <summary>Every lookup a project's compilation makes, resolved over the projects of a run.</summary>
    /// <param name="project">The project whose lookups are resolved, one of the run's.</param>
    /// <param name="projects">Every project of the run.</param>
    public static IReadOnlyList<LocalizerLookup> Resolve(LoadedProject project, IReadOnlyList<LoadedProject> projects)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projects);

        var compilation = project.Compilation ?? throw new ArgumentException("The project has no compilation.", nameof(project));
        LocalizerLookup[] lookups = [.. compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups)];

        return new LocaleFlow(projects, LocaleFlow.ReadLocaleSourceAttribute).Resolve(project, lookups);
    }
}
