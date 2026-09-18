namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;

/// <summary>Checks that a translation writes nothing shaped like a hole the runtime cannot read.</summary>
public sealed class TranslationHoleShapeChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.TranslationHoleShapedText;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entry in context.Document.Entries)
        {
            if (!entry.IsText || entry.Value is not { } translation || entry.Name == LocalizationSchema.Marker)
            {
                continue;
            }

            foreach (var suspect in HoleShapes.Suspect(translation))
            {
                context.Report(
                    Code,
                    entry.ValueOffset,
                    $"The translation of '{entry.Name}' writes '{{{suspect}}}', which is not a name a value can be read by, so it renders as written.");
            }
        }
    }
}
