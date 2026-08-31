namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Json;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

using Microsoft.CodeAnalysis;

/// <summary>
/// Checks that a key written in source is answered by every translation file its type already owns. A new key
/// added to a component whose translations exist otherwise passes every check while rendering in the source
/// language, because falling back to the key is what an absent entry is supposed to do.
/// </summary>
public sealed class TranslationCoverageLinter : ITranslationCoverageLinter
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<LintDiagnostic>> LintAsync(
        IReadOnlyList<TranslationFile> localizationFiles,
        IReadOnlyList<LocalizerCallSite> callSites,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localizationFiles);
        ArgumentNullException.ThrowIfNull(callSites);

        var translated = new HashSet<ISymbol>(
            callSites.Select(site => site.ResourceType).OfType<ISymbol>(),
            SymbolEqualityComparer.Default);

        if (translated.Count == 0)
        {
            return [];
        }

        var files = await ReadAsync(localizationFiles, translated, cancellationToken).ConfigureAwait(false);

        return files.Count == 0 ? [] : Check(callSites, files);
    }

    // JsonStringLocale walks the culture up to its parents, so a regional file need only carry what differs.
    private static bool Answered(List<TranslationKeys> owned, TranslationKeys file, string key)
    {
        for (var culture = file.Culture; culture.Name.Length != 0; culture = culture.Parent)
        {
            if (owned.Any(candidate => candidate.Culture.Name == culture.Name && candidate.Keys.Contains(key)))
            {
                return true;
            }
        }

        return false;
    }

    private static List<LintDiagnostic> Check(
        IReadOnlyList<LocalizerCallSite> callSites,
        Dictionary<ISymbol, List<TranslationKeys>> files)
    {
        var diagnostics = new List<LintDiagnostic>();

        // A key written at several sites is reported once per file it is missing from, at the first site.
        var seen = new HashSet<(string Key, string Path)>();

        foreach (var site in Ordered(callSites))
        {
            if (site.ResourceType is not { } resource || !files.TryGetValue(resource, out var owned))
            {
                continue;
            }

            foreach (var file in owned)
            {
                if (Answered(owned, file, site.Key) || !seen.Add((site.Key, file.Path)))
                {
                    continue;
                }

                diagnostics.Add(new LintDiagnostic(
                    site.Location.Path,
                    site.Location.StartLinePosition.Line + 1,
                    site.Location.StartLinePosition.Character + 1 + site.KeyOffset,
                    LintCodes.MissingTranslation.DefaultSeverity,
                    LintCodes.MissingTranslation,
                    $"'{site.Key}' has no entry in '{Path.GetFileName(file.Path)}', so it renders in the source language there."));
            }
        }

        return diagnostics;
    }

    // The walk that found the sites ran in parallel, so which site a key is reported at is settled here.
    private static IEnumerable<LocalizerCallSite> Ordered(IReadOnlyList<LocalizerCallSite> callSites)
        => callSites
            .OrderBy(site => site.Location.Path, StringComparer.Ordinal)
            .ThenBy(site => site.Location.StartLinePosition.Line)
            .ThenBy(site => site.Location.StartLinePosition.Character);

    private static async ValueTask<Dictionary<ISymbol, List<TranslationKeys>>> ReadAsync(
        IReadOnlyList<TranslationFile> localizationFiles,
        HashSet<ISymbol> translated,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<ISymbol, List<TranslationKeys>>(SymbolEqualityComparer.Default);

        foreach (var file in localizationFiles)
        {
            // A file no type answers to is already the unreachability rule's business.
            if (file.Type is not { } type || !translated.Contains(type))
            {
                continue;
            }

            if (await KeysAsync(file.Path, cancellationToken).ConfigureAwait(false) is not { } keys)
            {
                continue;
            }

            if (!files.TryGetValue(type, out var owned))
            {
                files[type] = owned = [];
            }

            owned.Add(new TranslationKeys(file.Path, file.Culture, keys));
        }

        return files;
    }

    // A file that will not parse is the JSON rule's business, and it answers for no key here.
    private static async ValueTask<HashSet<string>?> KeysAsync(string file, CancellationToken cancellationToken)
    {
        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (!LocalizationDocument.TryRead(file, content, out var document, out _))
        {
            return null;
        }

        // A key written with a JSON null is answered: that is how a file declares one it leaves in the source language.
        return document!.Entries
            .Where(static entry => entry.Name != LocalizationSchema.Marker)
            .Select(static entry => entry.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
