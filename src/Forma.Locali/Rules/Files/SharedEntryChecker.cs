namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that every shared entry is an object holding translations alone, which is the only shape the runtime loads.</summary>
public sealed class SharedEntryChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MalformedShared;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var reportedNests = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in context.Document.Entries)
        {
            // Another tool's entries are its own, and the runtime skips them wherever they sit.
            if (entry.IsInsideToolsSection)
            {
                continue;
            }

            // An entry is a leaf, so a shared entry that reaches here was given something other than an object.
            if (entry.Name == LocalizationSchema.SharedKey)
            {
                context.Report(Code, entry.NameOffset, $"'{LocalizationSchema.SharedKey}' is given a value that is not an object, so the file does not load; a shared section holds translations alone.");

                continue;
            }

            CheckNesting(context, entry, reportedNests);
        }
    }

    // A section inside a shared section is reported once, at its first entry.
    private void CheckNesting(LocalizationFileContext context, LocalizationEntry entry, HashSet<string> reportedNests)
    {
        var segments = entry.Section.Split('.');
        var shared = Array.IndexOf(segments, LocalizationSchema.SharedKey);

        if (shared < 0 || shared + 1 >= segments.Length)
        {
            return;
        }

        if (reportedNests.Add(string.Join('.', segments[..(shared + 2)])))
        {
            context.Report(Code, entry.NameOffset, $"'{LocalizationSchema.SharedKey}' nests '{segments[shared + 1]}', so the file does not load; a shared section holds translations alone.");
        }
    }
}
