namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Locali.Documents;
using Forma.Locali.Projects;

/// <summary>Resolves a project's translation files to the types the runtime finds them by.</summary>
public static class TranslationFiles
{
    /// <summary>Every culture-named file the project owns, each resolved once against the compilation.</summary>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The translation files this project owns, which the target attributed.</param>
    public static IReadOnlyList<TranslationFile> Resolve(LoadedProject project, IReadOnlyCollection<string> localizationFiles)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);

        if (project.Compilation is not { AssemblyName.Length: > 0 } compilation)
        {
            return [];
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project.Path))!;

        // The SDK embeds under RootNamespace, which a project may declare apart from its assembly name.
        var root = project.RootNamespace ?? compilation.AssemblyName!;
        var resolved = new List<TranslationFile>();

        foreach (var file in localizationFiles)
        {
            if (LocalizationFileName.CultureOf(file) is not { } culture)
            {
                continue;
            }

            var fullPath = Path.GetFullPath(file);
            var candidates = ResourceBaseNames.Candidates(root, projectDirectory, fullPath);

            resolved.Add(new TranslationFile(
                fullPath,
                CultureInfo.GetCultureInfo(culture),
                candidates.Select(compilation.GetTypeByMetadataName).FirstOrDefault(type => type is not null),
                candidates[0]));
        }

        return resolved;
    }
}
