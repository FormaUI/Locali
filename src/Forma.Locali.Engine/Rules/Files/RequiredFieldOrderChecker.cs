namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;

/// <summary>Checks that the metadata declares the required fields in the order the schema defines them.</summary>
public sealed class RequiredFieldOrderChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.RequiredFieldOutOfOrder;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var required = LocalizationSchema.RequiredFields;
        var names = context.FieldNames();

        // Only the required fields are ordered, so the optional ones are stripped out before the order is judged.
        var written = names.Where(name => required.Contains(name, StringComparer.Ordinal)).ToArray();
        var wanted = required.Where(name => written.Contains(name, StringComparer.Ordinal)).ToArray();

        if (written.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            return;
        }

        context.Report(
            Code,
            $"'{LocalizationSchema.Marker}' declares {string.Join(", then ", written)}; the schema orders them {string.Join(", then ", wanted)}.");
    }
}
