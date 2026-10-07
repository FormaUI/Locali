namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>A readable localization key in source.</summary>
/// <param name="Text">The key's text, as the runtime looks it up.</param>
/// <param name="Location">Where the key sits, mapped back to the file a human wrote.</param>
/// <param name="TextOffset">Where the key's text starts inside the literal.</param>
public sealed record LocalizationKeyReference(string Text, FileLinePositionSpan Location, int TextOffset);
