namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Json;
using Forma.Locali.Diagnostics;

/// <summary>Checks that the culture the file declares is the one its own name gives it.</summary>
public sealed class CultureMatchesChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.CultureMismatch;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ExpectedCulture is not { } expected
            || context.Field(LocalizationSchema.CultureField) is not { } written
            || string.Equals(written, expected, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Report(Code, $"The file declares '{LocalizationSchema.CultureField}: {written}' but is named for '{expected}'.");
    }
}
