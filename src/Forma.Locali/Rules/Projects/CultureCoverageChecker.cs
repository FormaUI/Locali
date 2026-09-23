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

    // The first culture of a family carrying an entry of the type's own stands for the whole family, so a regional file alone answers for its neutral culture and the reverse.
    private static Dictionary<string, LocaleSourceEntry> GetAnswered(ProjectLocalization localization, string typeName, Dictionary<string, IReadOnlyList<CultureInfo>> families)
    {
        var answered = new Dictionary<string, LocaleSourceEntry>(StringComparer.Ordinal);

        foreach (var (family, cultures) in families)
        {
            foreach (var culture in cultures)
            {
                // A shared block above the type answers for every type beneath it and says nothing about this one, so only the type's own entries, its own shared block among them, count.
                if (localization.Read(typeName, culture).Where(entry => IsOwn(localization, typeName, entry)).ToArray() is [.., var entry])
                {
                    answered[family] = entry;
                    break;
                }
            }
        }

        return answered;
    }

    // A shared block's owner is the section holding it, spelled from the file's position, or from the assembly's root for a rooted section; the block is the type's own when that owner spells the type.
    private static bool IsOwn(ProjectLocalization localization, string typeName, LocaleSourceEntry entry)
    {
        if (!TryGetSharedOwner(entry.TypeName, out var owner))
        {
            return true;
        }

        if (owner.StartsWith(LocalizationSchema.RootedSectionPrefix, StringComparison.Ordinal))
        {
            var rooted = owner[LocalizationSchema.RootedSectionPrefix.Length..];

            return rooted == typeName || $"{localization.Source.GetRootNamespace(localization.AssemblyName)}.{rooted}" == typeName;
        }

        return localization.GetFile(entry.FileName) is { } file
            && localization.GetAnchorNames(file).Any(anchorName => (owner.Length == 0 ? anchorName : $"{anchorName}.{owner}") == typeName);
    }

    private static bool TryGetSharedOwner(string entryTypeName, out string owner)
    {
        if (entryTypeName == LocalizationSchema.SharedKey)
        {
            owner = string.Empty;

            return true;
        }

        var suffix = $".{LocalizationSchema.SharedKey}";
        if (entryTypeName.EndsWith(suffix, StringComparison.Ordinal))
        {
            owner = entryTypeName[..^suffix.Length];

            return true;
        }

        owner = string.Empty;

        return false;
    }

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
