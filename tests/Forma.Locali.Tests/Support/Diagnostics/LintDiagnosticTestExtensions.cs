namespace Forma.Locali.Tests.Support.Diagnostics;

using Forma.Locali.Diagnostics;

/// <summary>Reads what a test asserts on out of a run's diagnostics.</summary>
public static class LintDiagnosticTestExtensions
{
    /// <summary>The code of every diagnostic, in the order they were reported.</summary>
    /// <param name="diagnostics">The diagnostics to read.</param>
    public static LintCode[] Codes(this IEnumerable<LintDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return [.. diagnostics.Select(diagnostic => diagnostic.Code)];
    }

    /// <summary>The file of every diagnostic, in the order they were reported.</summary>
    /// <param name="diagnostics">The diagnostics to read.</param>
    public static string[] Paths(this IEnumerable<LintDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return [.. diagnostics.Select(diagnostic => diagnostic.Path)];
    }

    /// <summary>The severity of the one diagnostic there is.</summary>
    /// <param name="diagnostics">The diagnostics to read.</param>
    public static LintSeverity SoleSeverity(this IEnumerable<LintDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics.Single().Severity;
    }
}
