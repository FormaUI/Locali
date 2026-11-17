namespace Forma.Locali.Configuration;

using Forma.Locali.Diagnostics;

/// <summary>A hint that the locale a member returns is read for the type another member of the same instance returns, with where it is written.</summary>
/// <param name="Assembly">The name of the assembly declaring the member.</param>
/// <param name="Type">The full metadata name of the type declaring the member.</param>
/// <param name="Member">The property returning the locale.</param>
/// <param name="TypeMember">The property of the same instance returning the type the locale is read for.</param>
/// <param name="Path">The file the hint is written in.</param>
/// <param name="Line">The line the hint starts on, from 1.</param>
/// <param name="Column">The column the hint starts at, from 1.</param>
public sealed record LocaleMemberHint(string Assembly, string Type, string Member, string TypeMember, string Path, int Line, int Column)
{
    /// <summary>Whether the hint is written in a restored package's file, which the run cannot change, so what is wrong in it is reported as a warning.</summary>
    public bool IsPackaged { get; init; }

    /// <summary>The member the hint is written for, by its type's full name.</summary>
    public string MemberName
        => $"{Type}.{Member}";

    /// <summary>Builds a diagnostic pointing at the hint.</summary>
    /// <param name="code">The rule the diagnostic is reported under.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public LintingDiagnostic Diagnose(LintingCode code, string message)
    {
        ArgumentNullException.ThrowIfNull(code);

        return new LintingDiagnostic(Path, Line, Column, IsPackaged ? LintingSeverity.Warning : code.DefaultSeverity, code, message);
    }
}
