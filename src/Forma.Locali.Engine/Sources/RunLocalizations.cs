namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;
using Forma.Locali.Projects;

/// <summary>The locale files of every project of a run, each resolved against its project's types once, found by project and by the assembly the project builds.</summary>
public sealed class RunLocalizations
{
    private readonly Dictionary<string, ProjectLocalization?> _byProject = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Dictionary<string, ProjectLocalization> _byAssembly = [with(StringComparer.Ordinal)];

    private RunLocalizations()
    {
    }

    /// <summary>Resolves every project's locale files, each against the projects of the run and the packs naming its assembly.</summary>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="projects">Every project the run loaded.</param>
    /// <param name="localizationFilesOf">The locale files a project owns, by the project's path.</param>
    /// <param name="packs">Every locale pack of the run.</param>
    /// <param name="builtAssemblies">Every assembly a project of the run builds.</param>
    public static RunLocalizations Resolve(
        ILocaleFileFormat format,
        IReadOnlyList<LoadedProject> projects,
        Func<string, IReadOnlyList<string>> localizationFilesOf,
        IReadOnlyList<LocalePack> packs,
        IReadOnlyList<BuiltAssembly> builtAssemblies)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(localizationFilesOf);
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(builtAssemblies);

        var run = new RunLocalizations();

        // Projects building one assembly, one per platform, are told apart by path, and the first by path speaks for the assembly.
        foreach (var project in projects.OrderBy(project => project.Path, StringComparer.Ordinal))
        {
            var localization = ProjectLocaleFiles.Resolve(format, project, localizationFilesOf(project.Path), packs, builtAssemblies);
            run._byProject[project.Path] = localization;

            if (localization is not null)
            {
                run._byAssembly.TryAdd(localization.AssemblyName, localization);
            }
        }

        return run;
    }

    /// <summary>The project's locale files resolved against its types, or null when the project did not compile or is a locale pack.</summary>
    /// <param name="project">The project, one of the run's.</param>
    public ProjectLocalization? ForProject(LoadedProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return _byProject.GetValueOrDefault(project.Path);
    }

    /// <summary>The locale files of the project of the run building an assembly, which the runtime reads a type of that assembly from, or null when no project of the run builds it.</summary>
    /// <param name="assemblyName">The assembly's name.</param>
    public ProjectLocalization? ForAssembly(string assemblyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);

        return _byAssembly.GetValueOrDefault(assemblyName);
    }
}
