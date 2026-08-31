namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>Finds the places a syntax tree looks a localization key up.</summary>
public interface ILocalizerCallSiteFinder
{
    /// <summary>Whether the finder can judge this tree at all.</summary>
    /// <param name="compilation">The compilation the tree belongs to.</param>
    bool CanRead(Compilation compilation);

    /// <summary>Every lookup the tree contains, split by whether its key could be read.</summary>
    /// <param name="compilation">The compilation the tree belongs to.</param>
    /// <param name="tree">The tree to walk.</param>
    LocalizerLookups Find(Compilation compilation, SyntaxTree tree);
}
