namespace Forma.Locali.Tests.Support.Linting;

using Forma.Locali.Linting;
using Forma.Locali.Rules;
using Forma.Locali.Rules.Files;

/// <summary>Builds a schema linter carrying the rules the linter ships with.</summary>
public static class SchemaLinterTestFactory
{
    /// <summary>A linter running every shipped file rule.</summary>
    public static SchemaLinter Create()
        => new(Checkers());

    /// <summary>Every file rule the linter ships with.</summary>
    public static IReadOnlyList<ILocalizationFileRuleChecker> Checkers()
        =>
        [
            new RequiredFieldsChecker(),
            new RequiredFieldOrderChecker(),
            new UnknownFieldChecker(),
            new SchemaVersionChecker(),
            new CultureMatchesChecker(),
            new TranslationHolesChecker(),
            new TranslationHoleShapeChecker(),
            new DuplicateKeyChecker(),
        ];
}
