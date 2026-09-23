namespace Forma.Locali.Rules.Projects;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that a type translated for one culture family the project's files or packs carry is not left out of another.</summary>
public sealed class CultureCoverageChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MissingCultureCoverage;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Localization is not { } localization)
        {
            return;
        }

        // A regional culture answers for its family, so families rather than individual cultures are what is compared.
        var families = localization.Cultures
            .GroupBy(GetFamily, culture => culture, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CultureInfo>)[.. group], StringComparer.Ordinal);

        if (families.Count < 2)
        {
            return;
        }

        foreach (var typeName in localization.TypeNames)
        {
            var answered = GetAnswered(localization, typeName, families);

            // A type with no translations at all is unlocalized rather than uneven, and one every family carries is not missing anywhere.
            if (answered.Count == 0 || answered.Count == families.Count)
            {
                continue;
            }

            var (answeredFamily, entry) = answered.OrderBy(pair => pair.Key, StringComparer.Ordinal).First();
            var filePath = localization.GetFile(entry.FileName) is { } file ? file.Path : entry.FileName;

            foreach (var family in families.Keys.Where(family => !answered.ContainsKey(family)).OrderBy(family => family, StringComparer.Ordinal))
            {
                context.Report(
                    Code,
                    filePath,
                    $"'{typeName}' is translated in '{answeredFamily}' here, and has no entry anywhere for '{family}', so it renders in the source language for every '{family}' reader.");
            }
        }
    }

    // The first culture of a family carrying an entry stands for the whole family, so a regional file alone answers for its neutral culture and the reverse.
    private static Dictionary<string, LocaleSourceEntry> GetAnswered(ProjectLocalization localization, string typeName, Dictionary<string, IReadOnlyList<CultureInfo>> families)
    {
        var answered = new Dictionary<string, LocaleSourceEntry>(StringComparer.Ordinal);

        foreach (var (family, cultures) in families)
        {
            foreach (var culture in cultures)
            {
                // A shared entry answers for every type the file it sits in reaches, so it alone does not mean the type was ever translated.
                if (localization.Read(typeName, culture).Where(entry => !IsShared(entry.TypeName)).ToArray() is [.., var entry])
                {
                    answered[family] = entry;
                    break;
                }
            }
        }

        return answered;
    }

    private static bool IsShared(string entryTypeName)
        => entryTypeName == LocalizationSchema.SharedKey || entryTypeName.EndsWith($".{LocalizationSchema.SharedKey}", StringComparison.Ordinal);

    // Where a culture's hierarchy bottoms out: es-419's is es, es's is itself.
    private static string GetFamily(CultureInfo culture)
    {
        var family = culture;
        while (!family.Parent.Equals(CultureInfo.InvariantCulture))
        {
            family = family.Parent;
        }

        return family.Name;
    }
}
