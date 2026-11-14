namespace Forma.Locali.Rules.Projects;

using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that no section of an application's file is read both for the application's own types and for another assembly's, as a name fitting two readings is where a type of each reaches the section.</summary>
public sealed class AmbiguousNameChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.AmbiguousName;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only an application reads its files for other assemblies as well as for its own types.
        if (context.Localization is not { IsApplication: true } localization)
        {
            return;
        }

        // A section is known by the file it sits in and the part of a type's name it completes there.
        var ownReached = new HashSet<(string Path, string Section)>();
        var otherReached = new Dictionary<(string Path, string Section), SortedSet<string>>();

        foreach (var culture in localization.Cultures)
        {
            var entries = localization.Source.ReadEntries(localization.AssemblyName, culture.Name);
            foreach (var section in localization.TypeNames.SelectMany(entries.GetSections))
            {
                if (localization.GetFile(section.FileName) is { } file)
                {
                    ownReached.Add((file.Path, section.TypeName));
                }
            }

            // The application's reading by its own name reads for its own types too.
            foreach (var (assemblyName, section) in localization.GetApplicationSections(culture))
            {
                if (localization.GetFile(section.FileName) is not { } file)
                {
                    continue;
                }

                if (assemblyName == localization.AssemblyName)
                {
                    ownReached.Add((file.Path, section.TypeName));
                }
                else
                {
                    if (!otherReached.TryGetValue((file.Path, section.TypeName), out var assemblyNames))
                    {
                        otherReached[(file.Path, section.TypeName)] = assemblyNames = [with(StringComparer.Ordinal)];
                    }

                    assemblyNames.Add(assemblyName);
                }
            }
        }

        foreach (var file in localization.EmbeddedFiles)
        {
            var assemblyNames = otherReached
                .Where(read => read.Key.Path == file.Path && ownReached.Contains(read.Key))
                .SelectMany(read => read.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            if (assemblyNames.Count == 0)
            {
                continue;
            }

            var ownAnchors = localization.GetAnchorNames(file);
            var otherAnchors = localization.GetApplicationAnchorNames(file).Except(ownAnchors, StringComparer.Ordinal).ToList();

            context.Report(
                Code,
                file.Path,
                $"A section of this file is read both for a type of the application and for one of {FormatNames(assemblyNames)}, since its name reads both as {FormatNames(ownAnchors)} and as {FormatNames(otherAnchors)}, so its entries answer both.");
        }
    }

    private static string FormatNames(IReadOnlyList<string> names)
        => string.Join(" and ", names.Select(name => $"'{name}'"));
}
