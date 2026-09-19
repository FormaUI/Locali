namespace Forma.Locali.Reporting;

using Forma.Locali.Diagnostics;

/// <summary>Writes every diagnostic, then how many there were.</summary>
/// <param name="writer">Where the lines go.</param>
public sealed class ConsoleDiagnosticReporter(TextWriter writer) : IDiagnosticReporter
{
    private readonly TextWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    /// <inheritdoc />
    public async ValueTask ReportAsync(LintingResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var diagnostic in result.Diagnostics)
        {
            await _writer.WriteLineAsync(diagnostic.ToString().AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        var summary = result.Diagnostics.Count == 0
            ? $"Linted {result.FileCount} file(s); no diagnostics."
            : $"Linted {result.FileCount} file(s); {result.Diagnostics.Count} diagnostic(s).";

        await _writer.WriteLineAsync(summary.AsMemory(), cancellationToken).ConfigureAwait(false);

        if (result.Suppressed.Count == 0)
        {
            return;
        }

        await _writer.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken).ConfigureAwait(false);
        await _writer
            .WriteLineAsync($"{result.SuppressedCount} diagnostic(s) not shown:".AsMemory(), cancellationToken)
            .ConfigureAwait(false);

        foreach (var family in result.Suppressed.GroupBy(Family))
        {
            var codes = string.Join(", ", family.Select(rule => $"{rule.Code.Id} ({rule.Count})"));

            await _writer.WriteLineAsync($"  - {codes}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        // Raising the severity only ever reveals a warning; an ignored diagnostic needs the linting configuration read instead.
        var hint = result.IgnoredCount == 0
            ? "Pass --severity warning to read them."
            : result.IgnoredCount == result.SuppressedCount
                ? "The linting configuration accounts for all of them."
                : "Pass --severity warning to read the rest; the linting configuration accounts for the others.";

        await _writer.WriteLineAsync(hint.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    // The linting configuration reports under 100, a file rule in the 100s, and a callsite rule in the
    // 200s, so the range gives the brief its lines.
    private static string Family(SuppressedRule rule)
        => rule.Code.Id[..^2];
}
