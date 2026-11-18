using System.Text;

namespace LGAC.Linux.Platform;

public sealed class AutostartEntry(string desktopFilePath, string executablePath)
{
    private const string ExecPrefix = "Exec=";

    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly string? fullPath = ResolvePath(desktopFilePath);

    public bool IsEnabled
    {
        get => ReadCurrent() is { Enabled: true };
        set
        {
            if (value)
            {
                Write();
            }
            else
            {
                if (fullPath is not null)
                {
                    DeleteQuietly(fullPath);
                }
            }
        }
    }

    public void RepairPath()
    {
        if (ReadCurrent() is not { } current)
        {
            return;
        }

        if (!current.Enabled)
        {
            return;
        }

        if (!string.Equals(current.Path, executablePath, StringComparison.Ordinal))
        {
            Write();
        }
    }

    internal static string EncodeExec(string path)
    {
        var argument = new StringBuilder("\"");
        foreach (var character in path)
        {
            if (character is '"' or '`' or '$' or '\\')
            {
                argument.Append('\\');
            }

            argument.Append(character);
        }

        argument.Append('"');
        return argument.ToString().Replace("%", "%%", StringComparison.Ordinal)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    internal static string DecodeExec(string value)
    {
        var unescaped = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '\\' && index + 1 < value.Length)
            {
                var replacement = value[index + 1] switch
                {
                    '\\' => '\\',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    's' => ' ',
                    _ => '\0',
                };
                if (replacement != '\0')
                {
                    index++;
                    character = replacement;
                }
            }

            unescaped.Append(character);
        }

        var text = unescaped.ToString().Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1];
        }

        var result = new StringBuilder();
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '\\' && index + 1 < text.Length && text[index + 1] is '"' or '`' or '$' or '\\')
            {
                index++;
                character = text[index];
            }
            else if (character == '%' && index + 1 < text.Length && text[index + 1] == '%')
            {
                index++;
            }

            result.Append(character);
        }

        return result.ToString();
    }

    private (bool Enabled, string Path)? ReadCurrent()
    {
        if (fullPath is null)
        {
            return null;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(fullPath, Utf8);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return null;
        }

        string? exec = null;
        var hidden = false;
        var inMainGroup = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('['))
            {
                inMainGroup = trimmed == "[Desktop Entry]";
                continue;
            }

            if (!inMainGroup)
            {
                continue;
            }

            if (trimmed.StartsWith(ExecPrefix, StringComparison.Ordinal))
            {
                exec = trimmed[ExecPrefix.Length..];
            }
            else if (trimmed is "Hidden=true" or "X-GNOME-Autostart-enabled=false")
            {
                hidden = true;
            }
        }

        return exec is null ? null : (!hidden, DecodeExec(exec));
    }

    private string Content() =>
        "[Desktop Entry]\n" +
        "Type=Application\n" +
        "Name=LG AC\n" +
        $"{ExecPrefix}{EncodeExec(executablePath)}\n" +
        "Icon=lg-ac\n" +
        "Terminal=false\n" +
        "X-GNOME-Autostart-enabled=true\n";

    private void Write()
    {
        if (fullPath is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(fullPath)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, Content(), Utf8);
            File.Move(temporary, fullPath, overwrite: true);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
        }
        finally
        {
            DeleteQuietly(temporary);
        }
    }

    private static void DeleteQuietly(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
        }
    }

    private static bool IsIoFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException;

    private static string? ResolvePath(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return null;
        }
    }
}
