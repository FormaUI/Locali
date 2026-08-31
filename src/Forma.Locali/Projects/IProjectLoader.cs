namespace Forma.Locali.Projects;

/// <summary>Loads projects and solutions into compilations.</summary>
public interface IProjectLoader
{
    /// <summary>Loads every project the paths name, one entry per project.</summary>
    /// <param name="paths">Project or solution files.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    ValueTask<IReadOnlyList<LoadedProject>> LoadAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);
}
