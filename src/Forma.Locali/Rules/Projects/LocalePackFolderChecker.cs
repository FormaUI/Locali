namespace Forma.Locali.Rules.Projects;

using Forma.Locali.Diagnostics;

/// <summary>Checks that every top-level folder of a locale pack names an assembly the solution builds or references.</summary>
public sealed class LocalePackFolderChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnknownLocalePackAssembly;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only a solution lists every project a folder could name, and a project that did not compile keeps its assembly's name to itself.
        if (context.Pack is not { IsEveryAssemblyKnown: true } pack)
        {
            return;
        }

        foreach (var folder in pack.Folders)
        {
            if (folder.Target is null && !folder.IsReferenced)
            {
                context.Report(
                    Code,
                    context.Project.Path,
                    $"Folder '{folder.AssemblyName}' names no assembly the solution builds or references, so the runtime reads nothing from it and the types it was written for render in the source language.");
            }
        }
    }
}
