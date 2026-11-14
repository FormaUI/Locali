namespace Forma.Locali.Rules.Projects;

using Forma.Locali.Diagnostics;

/// <summary>Checks that every culture-named file of a locale pack has a name starting with an assembly the solution builds or references.</summary>
public sealed class LocalePackAssemblyChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnknownLocalePackAssembly;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only a solution lists every project a name could start with, and a project that did not compile keeps its assembly's name to itself.
        if (context.Pack is not { IsEveryAssemblyKnown: true } pack)
        {
            return;
        }

        foreach (var (path, fileName) in pack.FileNames.OrderBy(file => file.Key, StringComparer.Ordinal))
        {
            if (fileName.AssemblyName is not null)
            {
                continue;
            }

            var name = string.Join('.', fileName.FileName.Parts);

            context.Report(
                Code,
                path,
                name.Length == 0
                    ? "This file's name is its culture alone, so it starts with no assembly the solution builds or references and the runtime reads nothing from it."
                    : $"This file stands for '{name}', which starts with no assembly the solution builds or references, so the runtime reads nothing from it and the types it was written for render in the source language.");
        }
    }
}
