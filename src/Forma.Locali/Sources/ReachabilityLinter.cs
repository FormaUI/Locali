namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>
/// Checks that a translation file's own location still names a type. `JsonStringLocaleFactory` resolves a
/// locale by the owning type's full name, not by where the file sits on disk, so a type renamed or moved
/// away leaves its translations unreachable while every other check stays green.
/// </summary>
public sealed class ReachabilityLinter : IReachabilityLinter
{
    /// <inheritdoc />
    public IReadOnlyList<LintDiagnostic> Lint(IReadOnlyList<TranslationFile> localizationFiles)
    {
        ArgumentNullException.ThrowIfNull(localizationFiles);

        return
        [
            .. localizationFiles
                .Where(file => file.Type is null)
                .Select(file => new LintDiagnostic(
                    file.Path,
                    1,
                    1,
                    LintCodes.UnreachableTranslation.DefaultSeverity,
                    LintCodes.UnreachableTranslation,
                    $"No type named '{file.ExpectedName}' exists. JsonStringLocaleFactory resolves this file by that full name, so its translations are unreachable.")),
        ];
    }
}
