namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that no folder of a project's locale files is named for a type: a folder is a namespace, and a type's own file is named for the type in its namespace's folder.</summary>
public sealed class TypeNamedFolderLinter : ITypeNamedFolderLinter
{
    /// <inheritdoc />
    public IReadOnlyList<LintingDiagnostic> Lint(ProjectLocalization localization)
    {
        ArgumentNullException.ThrowIfNull(localization);

        var typeNames = localization.TypeNames.ToHashSet(StringComparer.Ordinal);
        var diagnostics = new List<LintingDiagnostic>();

        // A file named for its culture alone speaks for its folder, so a folder named for a type lays the type out as a namespace.
        foreach (var file in localization.Files)
        {
            if (!Path.GetFileNameWithoutExtension(file.Path).Equals(file.Culture.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var anchorName in localization.GetAnchorNames(file))
            {
                if (typeNames.Contains(anchorName))
                {
                    var typeName = anchorName[(anchorName.LastIndexOf('.', StringComparison.Ordinal) + 1)..];
                    diagnostics.Add(new LintingDiagnostic(
                        file.Path,
                        1,
                        1,
                        LintingCodes.TypeNamedFolder.DefaultSeverity,
                        LintingCodes.TypeNamedFolder,
                        $"This file's folder is named for the type '{anchorName}', which lays the type out as a namespace; a type's own file is named for the type instead, '{typeName}.{file.Culture.Name}{Path.GetExtension(file.Path)}' among its namespace folder's locale files."));
                    break;
                }
            }
        }

        return diagnostics;
    }
}
