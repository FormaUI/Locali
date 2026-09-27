namespace Forma.Locali.Tests.Support.Projects;

using System.Runtime.CompilerServices;

using Forma.Locali.Projects;

/// <summary>A loader that hands back the projects it was given, none unless told, so a test can lint files without an MSBuild load.</summary>
/// <param name="projects">The projects every load hands back.</param>
public sealed class StubProjectLoader(params IReadOnlyList<LoadedProject> projects) : IProjectLoader
{
    /// <inheritdoc />
    public async IAsyncEnumerable<LoadedProject> LoadAsync(IEnumerable<string> paths, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Each project arrives on a turn of its own, as it does from the real loader's thread.
        foreach (var project in projects)
        {
            await Task.Yield();

            yield return project;
        }
    }
}
