namespace Forma.Locali.Tests.Support.Projects;

using Forma.Locali.Projects;

/// <summary>A loader that hands back the projects it was given, none unless told, so a test can lint files without an MSBuild load.</summary>
/// <param name="projects">The projects every load hands back.</param>
public sealed class StubProjectLoader(params IReadOnlyList<LoadedProject> projects) : IProjectLoader
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LoadedProject>> LoadAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(projects);
}
