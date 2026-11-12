using Gym.Application.Common.Files;

using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Files;

/// <summary><c>Files:Root</c>: the folder the file store keeps its files in.</summary>
public sealed class FileStoreOptions
{
    public const string SectionName = "Files";

    /// <summary>
    /// In production the files volume (<c>/var/lib/gym/files</c>, docker-compose.prod.yml), which
    /// <c>backup.sh</c> takes with the database. Locally a folder that git ignores.
    /// </summary>
    public string Root { get; set; } = string.Empty;
}

public sealed class FileStoreOptionsValidator : IValidateOptions<FileStoreOptions>
{
    public ValidateOptionsResult Validate(string? name, FileStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.Root)
            ? ValidateOptionsResult.Fail("Files:Root is required: the folder uploaded photos are kept in.")
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// <see cref="IFileStore"/> on the local disk: a key is a relative path under <c>Files:Root</c>.
/// </summary>
/// <remarks>
/// A file is written to a temporary name and then moved into place, so a reader never sees half a
/// file and a crash leaves no broken one under the real name. Keys are made by the app, but a key
/// that would resolve outside the root is refused anyway.
/// </remarks>
public sealed class DiskFileStore(IOptions<FileStoreOptions> options) : IFileStore
{
    private readonly string _root = Path.GetFullPath(options.Value.Root);

    public async Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, content.ToArray(), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public Stream? OpenRead(string key)
    {
        var path = PathOf(key);
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        File.Delete(PathOf(key));
        return Task.CompletedTask;
    }

    private string PathOf(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The file key '{key}' leaves the file store's root.", nameof(key));
        }

        return path;
    }
}
