namespace Forma.Locali.Rules.Projects;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that the root namespace the runtime counts a type's name from holds a type of the project.</summary>
public sealed class RootNamespaceWithoutTypesChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.RootNamespaceWithoutTypes;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Without the project's items no config file is known to be embedded, so what the runtime counts from cannot be told.
        if (context.Localization is not { TypeNames.Count: > 0 } localization || context.Project.EmbeddedResourceNames is null)
        {
            return;
        }

        // The runtime's own answer: the config's rootNamespace, or the assembly's name where it declares none.
        var rootNamespace = localization.Source.GetRootNamespace(localization.AssemblyName);
        if (localization.TypeNames.Any(typeName => IsUnder(typeName, rootNamespace)))
        {
            return;
        }

        var declared = ReadDeclaredRootNamespace(localization.Source.ConfigPaths, out var configPath, out var isUnreadable);
        if (isUnreadable)
        {
            return;
        }

        // The project's own RootNamespace is named where the types sit under it, since a drift between the two is the usual cause.
        var projectRootNamespace = context.Project.RootNamespace;
        var suggestion = projectRootNamespace is not null
            && projectRootNamespace != rootNamespace
            && localization.TypeNames.Any(typeName => IsUnder(typeName, projectRootNamespace))
                ? $"; the project's types sit under '{projectRootNamespace}', its RootNamespace"
                : string.Empty;

        if (declared is not null)
        {
            context.Report(Code, configPath!, $"'{LocaleConfig.RootNamespaceKey}' names '{rootNamespace}', which none of the project's types is under, so the runtime counts no type's name from it and a host or a pack laying the assembly's files out by it reaches no type{suggestion}.");
        }
        else if (localization.EmbeddedFiles.Count > 0 || IsAnyPackFilePlacedUnderRootAlone(localization, rootNamespace))
        {
            context.Report(Code, context.Project.Path, $"None of the project's types is under '{rootNamespace}', the assembly's name, which the runtime counts a type's name from where no '{LocaleConfig.FileName}' declares '{LocaleConfig.RootNamespaceKey}', so a host or a pack laying the assembly's files out by it reaches no type{suggestion}.");
        }
    }

    // The runtime places a file spelling a full name outside the root there as well, and reads it for a type outside the root; a file placed under the root alone reaches nothing when no type is there.
    private static bool IsAnyPackFilePlacedUnderRootAlone(ProjectLocalization localization, string rootNamespace)
        => localization.Packs
            .SelectMany(pack => pack.Files)
            .Select(localization.GetAnchorNames)
            .Any(anchorNames => anchorNames.Count > 0 && anchorNames.All(anchorName => anchorName == rootNamespace || IsUnder(anchorName, rootNamespace)));

    // A type outside any namespace belongs to the root, as the runtime has it.
    private static bool IsUnder(string typeName, string rootNamespace)
        => !typeName.Contains('.', StringComparison.Ordinal)
            || (typeName.Length > rootNamespace.Length + 1
                && typeName[rootNamespace.Length] == '.'
                && typeName.StartsWith(rootNamespace, StringComparison.Ordinal));

    // What is wrong with the config file itself is LOC0022's business, so one that will not read is left to it.
    private static string? ReadDeclaredRootNamespace(IReadOnlyCollection<string> configPaths, out string? configPath, out bool isUnreadable)
    {
        configPath = configPaths.Count == 1 ? configPaths.First() : null;
        isUnreadable = configPaths.Count > 1;

        if (configPath is null)
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(configPath);

            return LocaleConfig.Read(stream, configPath).RootNamespace;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or LocaleConfigException)
        {
            isUnreadable = true;

            return null;
        }
    }
}
