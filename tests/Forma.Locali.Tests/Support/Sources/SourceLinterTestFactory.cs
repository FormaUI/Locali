namespace Forma.Locali.Tests.Support.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;
using Forma.Locali.Rules;
using Forma.Locali.Rules.Sources;
using Forma.Locali.Sources;

/// <summary>Builds the callsite rules the linter ships with.</summary>
public static class SourceLinterTestFactory
{
    /// <summary>Every callsite rule the linter ships with.</summary>
    public static IReadOnlyList<ICallsiteRuleChecker> Checkers()
        =>
        [
            new MalformedExpressionChecker(),
            new ValueCountChecker(),
            new PluralArgumentChecker(),
            new SoleArgumentChecker(),
            new PluralOtherFieldChecker(),
            new HoleShapeChecker(),
            new BareHoleKeyChecker(),
            new BareExpressionKeyChecker(),
            new ReservedLookupShapeChecker(),
            new ReformattedLookupChecker(),
        ];

    /// <summary>What the callsite rules report over members of a class that holds a localizer.</summary>
    /// <param name="body">The members to place inside the class.</param>
    public static IReadOnlyList<LintingDiagnostic> Lint(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var compilation = CompilationTestFactory.Referenced($$"""
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Widget(IStringLocale locale)
            {
                {{body}}
            }
            """);

        var linter = new SourceLinter(new SemanticCallsiteFinder(), new SyntacticCallsiteFinder(), Checkers());
        var files = compilation.SyntaxTrees.Select(tree => tree.FilePath).ToArray();

        return linter.Lint(new LoadedProject("Scratch.csproj", compilation, HasGeneratedSources: false, HasRazorMarkup: false), files).Diagnostics;
    }
}
