namespace Forma.Locali.Tests.Support.Flow;

using Forma.Locali.Configuration;
using Forma.Locali.Flow;
using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

/// <summary>Finds a compilation's lookups and follows each to its types, as a run of the linter does once every project is loaded.</summary>
public static class LocaleFlowTestFactory
{
    /// <summary>The file a hint built here is written in.</summary>
    public const string HintPath = ".locali.json";

    /// <summary>A hint relating a member's locale to the type a member of the same instance returns, written in <see cref="HintPath"/>.</summary>
    /// <param name="assembly">The assembly declaring the member.</param>
    /// <param name="type">The full metadata name of the type declaring the member.</param>
    /// <param name="member">The property returning the locale.</param>
    /// <param name="typeMember">The property returning the type.</param>
    /// <param name="line">The line the hint is written on.</param>
    public static LocaleMemberHint Hint(string assembly, string type, string member = "L", string typeMember = "LocalizationSourceType", int line = 1)
        => new(assembly, type, member, typeMember, HintPath, line, 1);

    /// <summary>Every lookup the compilation makes, resolved over a run holding it and the other compilations given, each a project of its own.</summary>
    /// <param name="compilation">The compilation whose lookups are resolved.</param>
    /// <param name="others">The other compilations of the run.</param>
    public static IReadOnlyList<LocalizerLookup> Resolve(Compilation compilation, params IReadOnlyList<Compilation> others)
        => Run([], compilation, others).Lookups;

    /// <summary>Every lookup the compilation makes, resolved with hints over a run holding it and the other compilations given, each a project of its own.</summary>
    /// <param name="hints">The hints the run's configuration names.</param>
    /// <param name="compilation">The compilation whose lookups are resolved.</param>
    /// <param name="others">The other compilations of the run.</param>
    public static IReadOnlyList<LocalizerLookup> Resolve(IReadOnlyList<LocaleMemberHint> hints, Compilation compilation, params IReadOnlyList<Compilation> others)
        => Run(hints, compilation, others).Lookups;

    /// <summary>Every lookup a project's compilation makes, resolved over the projects of a run.</summary>
    /// <param name="project">The project whose lookups are resolved, one of the run's.</param>
    /// <param name="projects">Every project of the run.</param>
    /// <param name="hints">The hints the run's configuration names, none unless given.</param>
    public static IReadOnlyList<LocalizerLookup> Resolve(LoadedProject project, IReadOnlyList<LoadedProject> projects, IReadOnlyList<LocaleMemberHint>? hints = null)
        => Run(project, projects, hints ?? []).Lookups;

    /// <summary>Every lookup the compilation makes, resolved with hints over a run holding it and the other compilations given, with what the flow found wrong in the hints.</summary>
    /// <param name="hints">The hints the run's configuration names.</param>
    /// <param name="compilation">The compilation whose lookups are resolved.</param>
    /// <param name="others">The other compilations of the run.</param>
    public static (IReadOnlyList<LocalizerLookup> Lookups, IReadOnlyList<LintingDiagnostic> Diagnostics) Run(IReadOnlyList<LocaleMemberHint> hints, Compilation compilation, params IReadOnlyList<Compilation> others)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(others);

        var project = new LoadedProject("Scratch.csproj", compilation, false, false);

        return Run(project, [project, .. others.Select((other, index) => new LoadedProject($"Other{index}.csproj", other, false, false))], hints);
    }

    /// <summary>Every lookup a project's compilation makes, resolved with hints over the projects of a run, with what the flow found wrong in the hints.</summary>
    /// <param name="project">The project whose lookups are resolved, one of the run's.</param>
    /// <param name="projects">Every project of the run.</param>
    /// <param name="hints">The hints the run's configuration names.</param>
    public static (IReadOnlyList<LocalizerLookup> Lookups, IReadOnlyList<LintingDiagnostic> Diagnostics) Run(LoadedProject project, IReadOnlyList<LoadedProject> projects, IReadOnlyList<LocaleMemberHint> hints)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(hints);

        var compilation = project.Compilation ?? throw new ArgumentException("The project has no compilation.", nameof(project));
        LocalizerLookup[] lookups = [.. compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups)];
        var flow = new LocaleFlow(projects, hints);
        var resolved = flow.Resolve(project, lookups);

        return (resolved, flow.GetDiagnostics());
    }

    /// <summary>The lookups the keys a compilation's callsites take from parameters make at the calls writing them, over a run holding it and the other compilations given, each a project of its own.</summary>
    /// <param name="compilation">The compilation whose callsites are read.</param>
    /// <param name="others">The other compilations of the run.</param>
    public static ForwardedLookups Forward(Compilation compilation, params IReadOnlyList<Compilation> others)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(others);

        var project = new LoadedProject("Scratch.csproj", compilation, false, false);
        LoadedProject[] projects = [project, .. others.Select((other, index) => new LoadedProject($"Other{index}.csproj", other, false, false))];
        (LoadedProject, ForwardedKey)[] keys = [.. projects.SelectMany(each => each.Compilation!.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(each.Compilation!, tree).ForwardedKeys).Select(key => (each, key)))];

        return new LocaleFlow(projects, []).Forward(keys);
    }
}
