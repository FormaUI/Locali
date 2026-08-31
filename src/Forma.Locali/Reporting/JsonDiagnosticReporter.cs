namespace Forma.Locali.Reporting;

using System.Text.Json;
using System.Text.Json.Serialization;

using Forma.Locali.Diagnostics;

/// <summary>Writes the diagnostics as JSON, creating the directory when it does not exist.</summary>
/// <param name="path">The file the report is written to.</param>
public sealed class JsonDiagnosticReporter(string path) : IDiagnosticReporter
{
    private static readonly JsonSerializerOptions ReportOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path = !string.IsNullOrWhiteSpace(path)
        ? path
        : throw new ArgumentException("A report needs somewhere to go.", nameof(path));

    /// <inheritdoc />
    public async ValueTask ReportAsync(LintResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var report = new LintReport(
            result.FileCount,
            result.Diagnostics.Count,
            result.SuppressedCount,
            [.. result.Diagnostics.Select(LintReportEntry.From)]);

        var stream = File.Create(_path);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, report, ReportOptions, cancellationToken).ConfigureAwait(false);
        }
    }
}
