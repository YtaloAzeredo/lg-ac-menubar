namespace LGAC.Linux.Platform;

public sealed class XdgPaths(Func<string, string?> env, string home, string? tempBase = null)
{
    private const string AppDirectoryName = "lg-ac";

    private readonly string homeDirectory = IsUsable(home) ? home : FallbackBase(tempBase);

    public string ConfigHome => Resolve("XDG_CONFIG_HOME") ?? Path.Combine(homeDirectory, ".config");

    public string DataHome => Resolve("XDG_DATA_HOME") ?? Path.Combine(homeDirectory, ".local", "share");

    public string RuntimeDir
    {
        get
        {
            var configured = Resolve("XDG_RUNTIME_DIR");
            return configured is not null && Directory.Exists(configured) ? configured : FallbackBase(tempBase);
        }
    }

    public string AppConfigDir => Path.Combine(ConfigHome, AppDirectoryName);

    public string SettingsFile => Path.Combine(AppConfigDir, "settings.json");

    public string TokenFile => Path.Combine(AppConfigDir, "token");

    public string AutostartFile => Path.Combine(ConfigHome, "autostart", "lg-ac.desktop");

    public string LockFile => Path.Combine(RuntimeDir, "lg-ac.lock");

    public string SocketFile => Path.Combine(RuntimeDir, "lg-ac.sock");

    public static XdgPaths FromEnvironment() => new(
        Environment.GetEnvironmentVariable,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private static bool IsUsable(string? value) => !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value);

    public bool EnsurePrivateRuntimeDir()
    {
        try
        {
            var directory = RuntimeDir;
            var configured = Resolve("XDG_RUNTIME_DIR");
            if (configured is not null && string.Equals(configured, directory, StringComparison.Ordinal))
            {
                return Directory.Exists(directory);
            }

            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(directory);
                return Directory.Exists(directory);
            }

            const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(directory, ownerOnly);
            if (!Directory.Exists(directory) || new DirectoryInfo(directory).LinkTarget is not null)
            {
                return false;
            }

            File.SetUnixFileMode(directory, ownerOnly);
            return File.GetUnixFileMode(directory) == ownerOnly;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    private static string FallbackBase(string? tempBase)
    {
        var root = Path.GetFullPath(string.IsNullOrWhiteSpace(tempBase) ? Path.GetTempPath() : tempBase);
        return Path.Combine(root, $"{AppDirectoryName}-{UserIdentifier()}");
    }

    private static string UserIdentifier()
    {
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                return NativeMethods.GetEffectiveUserId().ToString();
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
            {
            }
        }

        var user = string.Concat(Environment.UserName.Where(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'));
        return user.Length == 0 ? "user" : user;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "geteuid")]
        internal static extern uint GetEffectiveUserId();
    }

    private string? Resolve(string name)
    {
        var value = env(name);
        return IsUsable(value) ? value : null;
    }
}
