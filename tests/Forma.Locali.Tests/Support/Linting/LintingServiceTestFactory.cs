namespace Forma.Locali.Tests.Support.Linting;

using Forma.Locali.Cli;
using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
using Forma.Locali.Linting;
using Forma.Locali.Projects;
using Forma.Locali.Sources;

using Microsoft.Extensions.Options;

/// <summary>Builds a lint service over the real file system.</summary>
public static class LintingServiceTestFactory
{
    /// <summary>A service that lints the file or directory named, loading no project unless handed a loader.</summary>
    /// <param name="path">What to lint.</param>
    /// <param name="minimumSeverity">The lowest severity the run reports.</param>
    /// <param name="projectLoader">Loads the projects the run finds, or null to load none.</param>
    public static LintingService Create(string path, LintingSeverity minimumSeverity = LintingSeverity.Error, IProjectLoader? projectLoader = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        // One instance so a glob's usage tracked while excluding files is the same tracking read back while ignoring.
        var lintingConfiguration = new LintingConfiguration(path);

        return new LintingService(
            Options.Create(new LinterOptions { Path = path, MinimumSeverity = minimumSeverity }),
            new LintingTargetResolver(lintingConfiguration),
            projectLoader ?? new StubProjectLoader(),
            new LocalizationFileLinter(SchemaLinterTestFactory.Create(), LocaleFormatTestFactory.Json),
            new SourceLinter(SourceLinterTestFactory.Finders(), SourceLinterTestFactory.Checkers()),
            new ProjectLinter(ProjectLinterTestFactory.Checkers(lintingConfiguration)),
            lintingConfiguration);
    }
}
