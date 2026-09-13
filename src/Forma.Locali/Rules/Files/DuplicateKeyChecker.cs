namespace Forma.Locali.Rules.Files;

using Forma.Locali.Diagnostics;

/// <summary>Checks that a file declares each key once within a section, since the last declaration is the one the runtime keeps.</summary>
public sealed class DuplicateKeyChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.DuplicateKey;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var seen = new Dictionary<(string Section, string Name), int>();

        foreach (var entry in context.Document.Entries)
        {
            if (seen.TryGetValue((entry.Section, entry.Name), out var line))
            {
                context.Report(
                    Code,
                    entry.NameOffset,
                    $"The file declares '{entry.Name}' again, having already declared it on line {line}; only the last translation survives.");

                continue;
            }

            seen[(entry.Section, entry.Name)] = context.Document.Map.Locate(entry.NameOffset).Line;
        }
    }
}
