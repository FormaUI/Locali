namespace Forma.Locali.Reporting;

using Forma.Locali.Diagnostics;

/// <summary>Writes every diagnostic, then how many there were.</summary>
/// <param name="writer">Where the lines go.</param>
public sealed class ConsoleDiagnosticReporter(TextWriter writer) : IDiagnosticReporter
{
    private readonly TextWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    /// <inheritdoc />
    public async ValueTask ReportAsync(LintResult result, CancellationToken cancellationToken = default)
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
            .WriteLineAsync($"{result.SuppressedCount} warning(s) not shown:".AsMemory(), cancellationToken)
            .ConfigureAwait(false);

        foreach (var family in result.Suppressed.GroupBy(Family))
        {
            var codes = string.Join(", ", family.Select(rule => $"{rule.Code.Id} ({rule.Count})"));

            await _writer.WriteLineAsync($"  - {codes}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await _writer
            .WriteLineAsync("Pass --severity warning to read them.".AsMemory(), cancellationToken)
            .ConfigureAwait(false);
    }

    // A file rule reports in the 100s and a call-site rule in the 200s, so the range gives the brief its lines.
    private static string Family(SuppressedRule rule)
        => rule.Code.Id[..^2];
}
