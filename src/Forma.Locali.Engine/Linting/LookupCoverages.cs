namespace Forma.Locali.Linting;

using Forma.Locali.Diagnostics;
using Forma.Locali.Flow;
using Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>Counts how far a run checked the lookups it found, and lists each one it could not check with what kept it unchecked.</summary>
internal static class LookupCoverages
{
    private const string KeyNotWritten = "keyNotWritten";
    private const string KeyFromParameter = "keyFromParameter";

    /// <summary>The coverage of a run's lookups.</summary>
    /// <param name="lookups">The lookups writing their key, each with the types the flow settled for it.</param>
    /// <param name="unreadKeys">Where a lookup writes its key as an expression.</param>
    /// <param name="unforwarded">The keys taken from a parameter that some call writes otherwise, or that no call of the run writes at all.</param>
    /// <param name="localizations">The entries the run reads for each assembly.</param>
    public static LookupCoverage Of(IReadOnlyList<LocalizerLookup> lookups, IReadOnlyList<FileLinePositionSpan> unreadKeys, IReadOnlyList<ForwardedKey> unforwarded, RunLocalizations localizations)
    {
        int fullyChecked = 0, partiallyChecked = 0, notChecked = 0, byAnalysis = 0, byVerifiedHint = 0, byHint = 0;
        var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var uncheckedLookups = new List<UncheckedLookup>();

        void Count(IEnumerable<string> shortfalls)
        {
            foreach (var reason in shortfalls)
            {
                reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
            }
        }

        // A lookup is checked against each certain type whose entries the run reads, from the project building the type's assembly;
        // what kept it short of that is named for the path that ended, or for the alternatives or the entries it could not use.
        foreach (var lookup in lookups)
        {
            var certain = lookup.Origins.Where(origin => !origin.IsApproximate).ToArray();
            var read = certain.Where(origin => origin.Type.ContainingAssembly?.Name is { Length: > 0 } name && localizations.ForAssembly(name) is not null).ToArray();

            var shortfalls = new SortedSet<string>(lookup.Gaps.Select(gap => Camel(gap.Kind.ToString())), StringComparer.Ordinal);
            if (certain.Length < lookup.Origins.Count)
            {
                shortfalls.Add("approximate");
            }

            if (read.Length < certain.Length)
            {
                shortfalls.Add("unavailableEntries");
            }

            // A lookup found without a bound compilation, or in code no instance runs, was never followed to a type.
            if (read.Length == 0 && shortfalls.Count == 0)
            {
                shortfalls.Add("notFollowed");
            }

            Count(shortfalls);

            if (read.Length == 0)
            {
                notChecked++;
                uncheckedLookups.Add(new UncheckedLookup(
                    lookup.Key.Location.Path,
                    lookup.Key.Location.StartLinePosition.Line + 1,
                    lookup.Key.Location.StartLinePosition.Character + 1 + lookup.Key.TextOffset,
                    [.. shortfalls],
                    Explain(lookup, certain)));
                continue;
            }

            if (shortfalls.Count == 0)
            {
                fullyChecked++;
            }
            else
            {
                partiallyChecked++;
            }

            byAnalysis += read.Any(origin => origin.Evidence == LocaleEvidence.Analysis) ? 1 : 0;
            byVerifiedHint += read.Any(origin => origin.Evidence == LocaleEvidence.VerifiedHint) ? 1 : 0;
            byHint += read.Any(origin => origin.Evidence == LocaleEvidence.Hint) ? 1 : 0;
        }

        foreach (var key in unreadKeys)
        {
            uncheckedLookups.Add(Unread(key, [KeyNotWritten], "The lookup's key is an expression rather than text, so no rule could check what it reads."));
        }

        foreach (var key in unforwarded)
        {
            var explanation = $"The lookup's key is the parameter '{key.Parameter}', which a call writes as an expression, passes from outside the run, or hands a method whose path to the lookup turns on the text in a way the flow cannot tell, so no rule could check what it reads.";
            uncheckedLookups.Add(Unread(
                key.Key,
                [KeyFromParameter],
                key.IsCalledFromOutside
                    ? $"{explanation} The method is a library's public one: a localeKeys hint its body bears out lets its consumers' runs check the calls they make."
                    : explanation));
        }

        var unread = unreadKeys.Count + unforwarded.Count;
        Count(Enumerable.Repeat(KeyNotWritten, unreadKeys.Count));
        Count(Enumerable.Repeat(KeyFromParameter, unforwarded.Count));

        return new LookupCoverage(lookups.Count + unread, fullyChecked, partiallyChecked, notChecked + unread, new LookupEvidence(byAnalysis, byVerifiedHint, byHint), reasons)
        {
            UncheckedLookups =
            [
                .. uncheckedLookups
                    .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Line)
                    .ThenBy(entry => entry.Column),
            ],
        };
    }

    private static UncheckedLookup Unread(FileLinePositionSpan key, IReadOnlyList<string> reasons, string explanation)
        => new(key.Path, key.StartLinePosition.Line + 1, key.StartLinePosition.Character + 1, reasons, explanation);

    private static string Explain(LocalizerLookup lookup, LocaleOrigin[] certain)
    {
        if (lookup.Gaps.FirstOrDefault(gap => gap.Kind == LocaleGapKind.UnreadTypeMember) is { } unread)
        {
            return $"A hint reads the lookup's type from '{unread.Detail}', whose getter returns no typeof or GetType() that could be read, so no rule could check '{lookup.Key.Text}' there.";
        }

        if (lookup.Gaps is [var gap, ..])
        {
            return $"No path from the lookup reaches the type its locale is read for, ending at {Phrase(gap.Kind)}, '{gap.Detail}', so no rule could check whether a culture answers '{lookup.Key.Text}'.";
        }

        if (certain.Length < lookup.Origins.Count)
        {
            return $"No path from the lookup reaches the type its locale is read for, ending at alternatives the flow cannot pair with the lookup, '{string.Join("', '", lookup.Origins.Select(origin => origin.Type.Name).Distinct(StringComparer.Ordinal))}', so no rule could check whether a culture answers '{lookup.Key.Text}'.";
        }

        return certain.Length > 0
            ? $"The lookup reads '{string.Join("', '", certain.Select(origin => origin.Type.Name).Distinct(StringComparer.Ordinal))}', whose assembly carries no entries the run reads, so no rule could check '{lookup.Key.Text}'."
            : $"No path was followed from the lookup, which was found without a bound compilation or in code no instance runs, so no rule could check '{lookup.Key.Text}'.";
    }

    private static string Phrase(LocaleGapKind kind)
        => kind switch
        {
            LocaleGapKind.CompiledCode => "compiled code",
            LocaleGapKind.CustomFactory => "a factory the code builds itself",
            LocaleGapKind.NamedByString => "a locale named by a string",
            LocaleGapKind.Dispatch => "a call no one implementation answers that the flow can tell",
            LocaleGapKind.Reflection => "a value the framework sets",
            LocaleGapKind.ExternalCallers => "callers outside the run",
            LocaleGapKind.Local => "a local assigned more than once",
            LocaleGapKind.Write => "a member written where the flow cannot pair the write with the object read",
            LocaleGapKind.Cycle => "a path returning to itself",
            LocaleGapKind.Depth => "the depth the flow follows a path to",
            _ => "code the flow does not read",
        };

    private static string Camel(string name)
        => string.Concat(char.ToLowerInvariant(name[0]).ToString(), name[1..]);
}
