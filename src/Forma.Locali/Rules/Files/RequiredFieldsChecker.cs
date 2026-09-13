namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;

/// <summary>Checks that the metadata declares every field the schema requires.</summary>
public sealed class RequiredFieldsChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MissingRequiredField;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var names = context.FieldNames();

        foreach (var name in LocalizationSchema.RequiredFields)
        {
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                context.Report(Code, $"'{LocalizationSchema.Marker}' does not declare '{name}'.");
            }
        }
    }
}
