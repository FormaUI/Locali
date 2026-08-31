namespace Forma.Locali;

using System.Diagnostics.CodeAnalysis;

using Forma.Locali.Cli;
using Forma.Locali.Linting;
using Forma.Locali.Reporting;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Drives one lint run and stops the host when it is done.</summary>
/// <param name="service">Runs the checks.</param>
/// <param name="reporters">Writes what the run found.</param>
/// <param name="lifetime">Stops the application once the run has been reported.</param>
/// <param name="logger">Records a failure the run could not report.</param>
public sealed partial class LintHostedService(
    ILintService service,
    IEnumerable<IDiagnosticReporter> reporters,
    IHostApplicationLifetime lifetime,
    ILogger<LintHostedService> logger) : BackgroundService
{
    private readonly ILintService _service = service ?? throw new ArgumentNullException(nameof(service));
    private readonly IEnumerable<IDiagnosticReporter> _reporters = reporters ?? throw new ArgumentNullException(nameof(reporters));
    private readonly IHostApplicationLifetime _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    private readonly ILogger<LintHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>The code the process should exit with.</summary>
    public ExitCode ExitCode { get; private set; } = ExitCode.Clean;

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The process boundary turns any failure into a non-zero exit code.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var result = await _service.RunAsync(stoppingToken).ConfigureAwait(false);

            foreach (var reporter in _reporters)
            {
                await reporter.ReportAsync(result, stoppingToken).ConfigureAwait(false);
            }

            ExitCode = result.Diagnostics.Count == 0 ? ExitCode.Clean : ExitCode.Diagnostics;
        }
        catch (OperationCanceledException)
        {
            ExitCode = ExitCode.Usage;
        }

        // A run that could not finish must never look like a clean one, whatever stopped it.
        catch (Exception exception)
        {
            LogRunFailure(exception);
            ExitCode = ExitCode.Usage;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The run did not finish.")]
    private partial void LogRunFailure(Exception exception);
}
