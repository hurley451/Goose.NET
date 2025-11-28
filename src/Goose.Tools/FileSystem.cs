namespace Goose.Tools;

/// <summary>
/// Default implementation of IFileSystem for file operations
/// </summary>
public class FileSystem : IFileSystem
{
    /// <summary>
    /// Reads all text from a file asynchronously
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
