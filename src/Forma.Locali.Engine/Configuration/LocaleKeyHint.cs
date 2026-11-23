namespace Forma.Locali.Configuration;

using Forma.Locali.Diagnostics;

/// <summary>A hint that a method looks up one of its parameters as a key, in the locale a parameter or a member holds or for the type a type parameter names, with where it is written.</summary>
/// <param name="Assembly">The name of the assembly declaring the method.</param>
/// <param name="Type">The full metadata name of the type declaring the method.</param>
/// <param name="Method">The method's name.</param>
/// <param name="Parameter">The parameter the method looks up as a key.</param>
/// <param name="Locale">The parameter of the method, or the member of its type, holding the locale the key is looked up in, or null where a type parameter names the type.</param>
/// <param name="TypeParameter">The type parameter of the method naming the type the key is looked up in, or null where a locale holds it.</param>
/// <param name="Path">The file the hint is written in.</param>
/// <param name="Line">The line the hint starts on, from 1.</param>
/// <param name="Column">The column the hint starts at, from 1.</param>
public sealed record LocaleKeyHint(string Assembly, string Type, string Method, string Parameter, string? Locale, string? TypeParameter, string Path, int Line, int Column)
{
    /// <summary>Whether the hint is written in a restored package's file, which the run cannot change, so what is wrong in it is reported as a warning.</summary>
    public bool IsPackaged { get; init; }

    /// <summary>Whether the run reads every project the hint may be written for, so one naming an assembly no project of the run builds or references applies to nothing.</summary>
    public bool IsHeldToRun { get; init; } = true;

    /// <summary>The method the hint is written for, by its type's full name.</summary>
    public string MethodName
        => $"{Type}.{Method}";

    /// <summary>Builds a diagnostic pointing at the hint.</summary>
    /// <param name="code">The rule the diagnostic is reported under.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public LintingDiagnostic Diagnose(LintingCode code, string message)
    {
        ArgumentNullException.ThrowIfNull(code);

        return new LintingDiagnostic(Path, Line, Column, IsPackaged ? LintingSeverity.Warning : code.DefaultSeverity, code, message);
    }
}
