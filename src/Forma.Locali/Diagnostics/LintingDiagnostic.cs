namespace Forma.Locali.Diagnostics;

/// <summary>One thing a rule found wrong, at the place it found it.</summary>
/// <param name="Path">The file the diagnostic belongs to.</param>
/// <param name="Line">The one-based line the diagnostic sits on.</param>
/// <param name="Column">The one-based column the diagnostic starts at.</param>
/// <param name="Severity">How much the diagnostic matters.</param>
/// <param name="Code">The rule the diagnostic is reported under.</param>
/// <param name="Message">What is wrong, in a sentence.</param>
public sealed record LintingDiagnostic(string Path, int Line, int Column, LintingSeverity Severity, LintingCode Code, string Message)
{
    /// <summary>The severity as a build log writes it.</summary>
    public string SeverityLabel
        => Severity == LintingSeverity.Error ? "error" : "warning";

    /// <summary>The diagnostic as an editor-navigable line.</summary>
    public override string ToString()
        => $"{Path}({Line},{Column}): {SeverityLabel} {Code.Id}: {Message}";
}
