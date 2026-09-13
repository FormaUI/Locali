namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;

/// <summary>Checks that the file declares the schema this build defines.</summary>
public sealed class SchemaVersionChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.SchemaVersionMismatch;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Field(LocalizationSchema.SchemaVersionField) is not { } written
            || string.Equals(written, LocalizationSchema.CurrentVersion, StringComparison.Ordinal))
        {
            return;
        }

        // The runtime rejects only a differing major; a differing minor or patch still loads.
        var severity = Version.TryParse(written, out var schema) && schema.Major == LocalizationSchema.SupportedMajor
            ? LintSeverity.Warning
            : LintSeverity.Error;

        context.Report(
            Code,
            context.Marker.ValueOffset,
            $"The file declares schema {written}; this build defines {LocalizationSchema.CurrentVersion}.",
            severity);
    }
}
