using LGAC.Core;

namespace LGAC.Linux.Platform;

public sealed class FileTokenVault(string path) : ITokenVault
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string fullPath = Path.GetFullPath(path);

    public string? Read()
    {
        try
        {
            var text = File.ReadAllText(fullPath).Trim();
            return text.Length == 0 ? null : text;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string token)
    {
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            CreateDirectory(directory);
            Write(temporary, token);
            File.Move(temporary, fullPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException("Não foi possível gravar o arquivo do token.", exception);
        }
        finally
        {
            DeleteQuietly(temporary);
        }
    }

    public void Delete() => DeleteQuietly(fullPath);

    private static void CreateDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        Directory.CreateDirectory(directory, DirectoryMode);
        try
        {
            File.SetUnixFileMode(directory, DirectoryMode);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void Write(string temporary, string token)
    {
        var options = new FileStreamOptions
        {
            Mode = System.IO.FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerReadWrite;
        }

        using var stream = new FileStream(temporary, options);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        writer.Write(token);
        writer.Flush();
        stream.Flush(flushToDisk: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, OwnerReadWrite);
        }
    }

    private static void DeleteQuietly(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
