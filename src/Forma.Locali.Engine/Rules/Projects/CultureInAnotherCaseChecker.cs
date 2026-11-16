namespace Forma.Locali.Rules.Projects;

using System.Collections.Concurrent;
using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that no part of the name a project's locale file is embedded under is a culture written in another case than .NET names it, where the runtime then misreads the file.</summary>
public sealed class CultureInAnotherCaseChecker : IProjectRuleChecker
{
    private static readonly ConcurrentDictionary<string, string?> Cultures = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.CultureInAnotherCase;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The types a part could name are known only where the project compiled, and the names it embeds only where its items were read.
        if (context.Localization is not { } localization || context.Project.EmbeddedResourceNames is not { } resourceNames)
        {
            return;
        }

        var localesDir = localization.LocalesDir;

        foreach (var file in context.LocalizationFiles)
        {
            var fullPath = Path.GetFullPath(file);
            if (!resourceNames.TryGetValue(fullPath, out var name))
            {
                continue;
            }

            var hasCulture = LocalizationFileName.Read(name, localization.Format.Extension, localesDir) is not null;
            var readFile = localization.EmbeddedFiles.FirstOrDefault(embedded => embedded.Path == fullPath);

            // A part read into a name may name a type of an assembly whose types are out of reach.
            if (hasCulture && (readFile is null || localization.IsReadOutOfReach(readFile)))
            {
                continue;
            }

            // The root namespace's own parts are LOC0027's.
            var ownName = localization.GetOwnName(name);

            // A file of no culture is read for no reader whatever its parts name, and a part naming nothing is blamed before one naming
            // something; a part read into a name misleads only where it names nothing.
            var (part, culture) = GetParts(ownName, localesDir)
                .Select(candidate => (Part: candidate, Culture: FindCulture(candidate)))
                .Where(candidate => candidate.Culture is not null && !(hasCulture && localization.IsNameSegment(candidate.Part)))
                .OrderBy(candidate => localization.IsNameSegment(candidate.Part))
                .FirstOrDefault();

            if (culture is null)
            {
                continue;
            }

            var reading = hasCulture
                ? "which names no type, so the runtime reads it as part of the name the file stands for"
                : "so the runtime finds no culture in the file's name and reads nothing from it";

            context.Report(Code, fullPath, $"'{part}' in the name this file is embedded under is the culture '{culture}' written in another case, {reading}.");
        }
    }

    // The parts of the name between its folders and dots, the extension and the folders of the folder of locale files left out.
    private static IEnumerable<string> GetParts(string name, string localesDir)
    {
        var localesParts = localesDir.Split('.', '/');

        return name[..^Path.GetExtension(name).Length]
            .Split('.', '/')
            .Where(part => part.Length > 0 && !localesParts.Contains(part, StringComparer.Ordinal));
    }

    // A culture in another case is one .NET finds whatever the case, under a name that differs from the part in case alone.
    private static string? FindCulture(string part)
        => Cultures.GetOrAdd(part, static candidate =>
        {
            var hyphen = candidate.IndexOf('-', StringComparison.Ordinal);
            var language = hyphen >= 0 ? candidate[..hyphen] : candidate;
            if (language.Length is not (2 or 3) || !language.All(char.IsAsciiLetter) || LocaleFileNames.IsCulture(candidate))
            {
                return null;
            }

            try
            {
                var culture = CultureInfo.GetCultureInfo(candidate, predefinedOnly: true).Name;

                return culture.Length > 0 && string.Equals(culture, candidate, StringComparison.OrdinalIgnoreCase) ? culture : null;
            }
            catch (CultureNotFoundException)
            {
                return null;
            }
        });
}
