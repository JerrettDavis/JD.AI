// Licensed under the MIT License.

namespace JD.AI.Tests.Fixtures;

/// <summary>
/// Provides a temporary directory for tests, with automatic cleanup.
/// Replaces 80+ copy-pasted IDisposable temp dir patterns.
/// </summary>
public sealed class TempDirectoryFixture : IDisposable
{
    /// <summary>Gets the path to the temporary directory.</summary>
    public string DirectoryPath { get; }

    /// <summary>Creates a new temporary directory with a unique name.</summary>
    public TempDirectoryFixture()
    {
        DirectoryPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"jdai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>Creates a file in the temp directory with the specified content.</summary>
    /// <param name="relativePath">Relative path within the temp dir.</param>
    /// <param name="content">File content.</param>
    /// <returns>Full path to the created file.</returns>
    public string CreateFile(string relativePath, string content = "")
    {
        var fullPath = System.IO.Path.Combine(DirectoryPath, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    /// <summary>Creates a subdirectory within the temp directory.</summary>
    /// <param name="relativePath">Relative path of the subdirectory.</param>
    /// <returns>Full path to the created subdirectory.</returns>
    public string CreateSubdirectory(string relativePath)
    {
        var fullPath = System.IO.Path.Combine(DirectoryPath, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>Gets the full path for a relative path within the temp dir.</summary>
    public string GetPath(string relativePath) =>
        System.IO.Path.Combine(DirectoryPath, relativePath);

    /// <summary>Removes the temporary directory and all contents.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; temp dirs will eventually be cleared
        }
    }
}
