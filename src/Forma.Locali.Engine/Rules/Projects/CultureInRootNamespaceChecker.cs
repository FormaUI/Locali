namespace Forma.Locali.Rules.Projects;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that no part of the root namespace a project's build embeds its locale files under is a culture the runtime reads an embedded file for.</summary>
public sealed class CultureInRootNamespaceChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.CultureInRootNamespace;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A pack embeds its files under their paths alone, and without the project's items no name is known.
        var project = context.Project;
        if (project.IsLocalePack
            || project.EmbeddedResourceNames is not { } resourceNames
            || project.Compilation is not { AssemblyName: { Length: > 0 } assemblyName })
        {
            return;
        }

        var rootNamespace = project.RootNamespace ?? assemblyName;
        if (rootNamespace.Split('.').FirstOrDefault(part => LocaleFileNames.IsCulture(part)) is not { } culture)
        {
            return;
        }

        // The runtime reads the first culture in a file's name as the file's, so a file embedded under the root namespace is read for the root's.
        var misread = context.LocalizationFiles
            .Select(Path.GetFullPath)
            .Where(path => resourceNames.TryGetValue(path, out var name)
                && name.StartsWith(rootNamespace + ".", StringComparison.Ordinal)
                && LocalizationFileName.Read(name, Path.GetExtension(name))?.Culture == culture)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();

        if (misread is null)
        {
            return;
        }

        var file = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(project.Path))!, misread);
        var named = rootNamespace == assemblyName ? "the assembly's name and the root namespace" : "the root namespace";

        context.Report(Code, project.Path, $"'{culture}' in '{rootNamespace}', {named} the build embeds the project's locale files under, is a culture, and the runtime reads the first culture in a file's name as the file's, so it reads '{file}' and every other file embedded under it for '{culture}'.");
    }
}
