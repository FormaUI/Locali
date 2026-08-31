namespace Forma.Locali.Linting;

using Forma.Locali.Diagnostics;

/// <summary>Runs the linter over everything the options name.</summary>
public interface ILintService
{
    /// <summary>Lints every file found, ordering what it finds.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    ValueTask<LintResult> RunAsync(CancellationToken cancellationToken = default);
}
