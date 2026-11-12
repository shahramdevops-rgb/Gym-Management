namespace Gym.Application.Common.Files;

/// <summary>
/// Files kept outside the database (ADR 0010: photos on a Docker volume, their metadata in
/// Postgres). Keys are relative paths made by the app (<c>shop/…-large.webp</c>), never typed by a
/// user, so they cannot reach outside the store.
/// </summary>
/// <remarks>
/// The database row and the file cannot be written in one transaction, so the order is fixed: a
/// new file is written before its row is saved (and deleted if the save fails), and a row is
/// deleted before its file. Whatever goes wrong in between leaves at worst an unused file, never a
/// row without its file.
/// </remarks>
public interface IFileStore
{
    /// <summary>Writes the file whole, replacing one with the same key.</summary>
    Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Opens the file for reading, or null when there is none under that key.</summary>
    Stream? OpenRead(string key);

    /// <summary>Deletes the file; a missing one is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
