namespace Forma.Locali.Tests.Support.Sources;

/// <summary>Microsoft's string localizer and Forma's adapter in small, as source, so a compilation binds lookups through them without either package.</summary>
public static class StringLocalizerFakeData
{
    /// <summary>The localizer and its factory, the adapter's registration, and Microsoft's own two, each in its namespace's block, to lead a file whose own declarations sit in blocks too.</summary>
    public const string Declarations = """
        namespace Microsoft.Extensions.Localization
        {
            public sealed class LocalizedString(string name, string value)
            {
                public string Name => name;

                public string Value => value;
            }

            public interface IStringLocalizer
            {
                LocalizedString this[string name] { get; }

                LocalizedString this[string name, params object[] arguments] { get; }
            }

            public interface IStringLocalizer<out T> : IStringLocalizer;

            public interface IStringLocalizerFactory
            {
                IStringLocalizer Create(System.Type resourceSource);
            }
        }

        namespace Forma.Framework.Localization.StringLocalizerAdapter
        {
            public static class ServiceCollectionExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddFormaStringLocalizerAdapter(this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
            }
        }

        namespace Microsoft.Extensions.DependencyInjection
        {
            public static class LocalizationServiceCollectionExtensions
            {
                public static IServiceCollection AddLocalization(this IServiceCollection services) => services;

                public static IServiceCollection AddViewLocalization(this IServiceCollection services) => services;
            }
        }

        """;
}
