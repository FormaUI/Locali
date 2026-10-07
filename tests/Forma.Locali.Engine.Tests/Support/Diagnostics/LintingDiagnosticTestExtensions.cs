namespace Forma.Locali.Tests.Support.Diagnostics;

using Forma.Locali.Diagnostics;

/// <summary>Reads what a test asserts on out of a run's diagnostics.</summary>
public static class LintingDiagnosticTestExtensions
{
    /// <summary>The code of every diagnostic, in the order they were reported.</summary>
    /// <param name="diagnostics">The diagnostics to read.</param>
    public static LintingCode[] Codes(this IEnumerable<LintingDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return [.. diagnostics.Select(diagnostic => diagnostic.Code)];
    }

    /// <summary>The file of every diagnostic, in the order they were reported.</summary>
    /// <param name="diagnostics">The diagnostics to read.</param>
    public static string[] Paths(this IEnumerable<LintingDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return [.. diagnostics.Select(diagnostic => diagnostic.Path)];
    }

    /// <summary>The severity of the one diagnostic there is.</summary>
    /// <param name="diagnostics">The diagnostics to read.</param>
    public static LintingSeverity SoleSeverity(this IEnumerable<LintingDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics.Single().Severity;
    }
}
