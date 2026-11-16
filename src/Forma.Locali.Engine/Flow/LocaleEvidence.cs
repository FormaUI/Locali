namespace Forma.Locali.Flow;

/// <summary>What established a type a lookup reads.</summary>
public enum LocaleEvidence
{
    /// <summary>The analysis followed the locale to where the runtime decided its type.</summary>
    Analysis,

    /// <summary>A hint supplied the relationship, and the analysis established the same type without it.</summary>
    VerifiedHint,

    /// <summary>A hint supplied the relationship, and nothing the analysis could follow confirms or contradicts it.</summary>
    Hint,
}
