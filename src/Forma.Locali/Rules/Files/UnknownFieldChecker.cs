namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;

/// <summary>Checks that the metadata declares no field beyond the ones the schema defines.</summary>
public sealed class UnknownFieldChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnknownMetadataField;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var name in context.FieldNames())
        {
            if (LocalizationSchema.RequiredFields.Contains(name, StringComparer.Ordinal)
                || LocalizationSchema.OptionalFields.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            // The runtime ignores a name it does not know, for forward compatibility with a newer minor schema.
            context.Report(Code, $"'{LocalizationSchema.Marker}' declares '{name}', which the schema does not define.");
        }
    }
}
