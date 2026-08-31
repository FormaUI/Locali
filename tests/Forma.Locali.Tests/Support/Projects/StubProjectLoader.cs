namespace Forma.Locali.Tests.Support.Projects;

using Forma.Locali.Projects;

/// <summary>A loader that reports no projects, so a test can lint files without an MSBuild load.</summary>
public sealed class StubProjectLoader : IProjectLoader
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LoadedProject>> LoadAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<LoadedProject>>([]);
}
