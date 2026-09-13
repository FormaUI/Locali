namespace Forma.Locali.Tests.Support.Linting;

using Forma.Locali.Cli;
using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
using Forma.Locali.Linting;
using Forma.Locali.Sources;

using Microsoft.Extensions.Options;

/// <summary>Builds a lint service over the real file system.</summary>
public static class LintingServiceTestFactory
{
    /// <summary>A service that lints the file or directory named, without loading any project.</summary>
    /// <param name="path">What to lint.</param>
    /// <param name="minimumSeverity">The lowest severity the run reports.</param>
    public static LintingService Create(string path, LintingSeverity minimumSeverity = LintingSeverity.Error)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        // One instance so a glob's usage tracked while excluding files is the same tracking read back while ignoring.
        var lintingConfiguration = new LintingConfiguration();

        return new LintingService(
            Options.Create(new LinterOptions { Path = path, MinimumSeverity = minimumSeverity }),
            new LintingTargetResolver(lintingConfiguration),
            new StubProjectLoader(),
            new LocalizationFileLinter(SchemaLinterTestFactory.Create()),
            new SourceLinter(new SemanticCallSiteFinder(), new SyntacticCallSiteFinder(), SourceLinterTestFactory.Checkers()),
            new ReachabilityLinter(),
            new TranslationCoverageLinter(),
            lintingConfiguration);
    }
}
