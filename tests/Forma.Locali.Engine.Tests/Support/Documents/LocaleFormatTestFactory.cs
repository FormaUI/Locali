namespace Forma.Locali.Tests.Support.Documents;

using Forma.Locali.Documents;

/// <summary>Hands out the locale format the linter ships with.</summary>
public static class LocaleFormatTestFactory
{
    /// <summary>The JSON format, the one the linter registers.</summary>
    public static ILocaleFormat Json { get; } = new JsonLocaleFormat();
}
