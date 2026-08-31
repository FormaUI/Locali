namespace Forma.Locali.Tests.Support.Linting;

using Forma.Locali.Cli;
using Forma.Locali.Diagnostics;
using Forma.Locali.Linting;
using Forma.Locali.Sources;

using Microsoft.Extensions.Options;

/// <summary>Builds a lint service over the real file system.</summary>
public static class LintServiceTestFactory
{
    /// <summary>A service that lints the file or directory named, without loading any project.</summary>
    /// <param name="path">What to lint.</param>
    /// <param name="minimumSeverity">The lowest severity the run reports.</param>
    public static LintService Create(string path, LintSeverity minimumSeverity = LintSeverity.Error)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return new LintService(
            Options.Create(new LinterOptions { Path = path, MinimumSeverity = minimumSeverity }),
            new LintTargetResolver(),
            new StubProjectLoader(),
            new LocalizationFileLinter(SchemaLinterTestFactory.Create()),
            new SourceLinter(new SemanticCallSiteFinder(), new SyntacticCallSiteFinder(), SourceLinterTestFactory.Checkers()),
            new ReachabilityLinter(),
            new TranslationCoverageLinter());
    }
}
