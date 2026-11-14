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

        // The root namespace's own parts are LOC0027's.
        var namesStart = localization.Source.GetRootDir(localization.AssemblyName) + ".";

        foreach (var file in context.LocalizationFiles)
        {
            var fullPath = Path.GetFullPath(file);
            if (!resourceNames.TryGetValue(fullPath, out var name))
            {
                continue;
            }

            var hasCulture = LocalizationFileName.Read(name, Path.GetExtension(name)) is not null;
            var readFile = localization.EmbeddedFiles.FirstOrDefault(embedded => embedded.Path == fullPath);

            // A part read into a name may name a type of an assembly whose types are out of reach.
            if (hasCulture && (readFile is null || localization.IsReadOutOfReach(readFile)))
            {
                continue;
            }

            var ownName = name.StartsWith(namesStart, StringComparison.Ordinal) ? name[namesStart.Length..] : name;

            foreach (var part in GetParts(ownName))
            {
                // A file of no culture is read for no reader whatever its parts name, and a part read into a name misleads only where it names nothing.
                if (FindCulture(part) is not { } culture || (hasCulture && localization.IsNameSegment(part)))
                {
                    continue;
                }

                var reading = hasCulture
                    ? "names no type, so the runtime reads it as part of the name the file stands for"
                    : "so the runtime finds no culture in the file's name and reads nothing from it";

                context.Report(Code, fullPath, $"'{part}' in the name this file is embedded under is the culture '{culture}' written in another case, which {reading}.");
                break;
            }
        }
    }

    // The parts of the name between its dots, the extension and folders of locale files left out.
    private static IEnumerable<string> GetParts(string name)
        => Path.GetFileNameWithoutExtension(name)
            .Split('.')
            .Where(part => part.Length > 0 && part != LocaleConfig.DefaultLocalesDir.TrimStart('.'));

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
