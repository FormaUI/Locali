namespace Forma.Locali.Rules.Files;

using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that no key or section is shaped like a reserved entry the runtime does not define, allowing for the doubled dollar sign that escapes the shape.</summary>
public sealed class ReservedKeyShapeChecker : ILocalizationFileRuleChecker
{
    private const string Explanation = "is shaped like a reserved entry the runtime does not define, so the file does not load; double the '$' to keep it as text.";

    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.ReservedShapedKey;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var reportedSections = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in context.Document.Entries)
        {
            CheckSections(context, entry, reportedSections);

            if (ReservedNames.IsShaped(entry.Name) && !ReservedNames.IsDefined(entry.Name))
            {
                context.Report(Code, entry.NameOffset, $"The key '{entry.Name}' {Explanation}");
            }
        }
    }

    // A section is reported once, at its first entry, under the path that reaches it.
    private void CheckSections(LocalizationFileContext context, LocalizationEntry entry, HashSet<string> reportedSections)
    {
        if (entry.IsTopLevel)
        {
            return;
        }

        var segments = entry.Section.Split('.');

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];

            if (!ReservedNames.IsShaped(segment) || ReservedNames.IsDefined(segment))
            {
                continue;
            }

            if (reportedSections.Add(string.Join('.', segments[..(index + 1)])))
            {
                context.Report(Code, entry.NameOffset, $"Section '{segment}' {Explanation}");
            }
        }
    }
}
