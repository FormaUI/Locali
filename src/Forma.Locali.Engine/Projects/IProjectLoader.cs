namespace Forma.Locali.Projects;

/// <summary>Loads projects and solutions into compilations.</summary>
public interface IProjectLoader
{
    /// <summary>Loads every project the paths name, handing each one over as soon as it is loaded.</summary>
    /// <param name="paths">Project or solution files.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    IAsyncEnumerable<LoadedProject> LoadAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);
}
