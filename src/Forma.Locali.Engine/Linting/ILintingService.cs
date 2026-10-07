namespace Forma.Locali.Linting;

using Forma.Locali.Diagnostics;

/// <summary>Runs the linter over everything the options name.</summary>
public interface ILintingService
{
    /// <summary>Lints every file found, ordering what it finds.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    ValueTask<LintingResult> RunAsync(CancellationToken cancellationToken = default);
}
