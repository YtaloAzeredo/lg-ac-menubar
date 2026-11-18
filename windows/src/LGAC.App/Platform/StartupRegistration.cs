using Microsoft.Win32;

namespace LGAC.App.Platform;

public interface IRunKey
{
    string? Get(string name);

    void Set(string name, string value);

    void Remove(string name);
}

public sealed class RegistryRunKey(string subKeyPath = RegistryRunKey.DefaultSubKeyPath) : IRunKey
{
    public const string DefaultSubKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Get(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValue(name) as string;
    }

    public void Set(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void Remove(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public sealed class StartupRegistration(IRunKey key, string executablePath)
{
    public const string ValueName = "LG AC";

    private string QuotedPath => $"\"{executablePath}\"";

    public bool IsEnabled
    {
        get => key.Get(ValueName) is not null;
        set
        {
            if (value)
            {
                key.Set(ValueName, QuotedPath);
            }
            else
            {
                key.Remove(ValueName);
            }
        }
    }

    public void RepairPath()
    {
        var current = key.Get(ValueName);
        if (current is null)
        {
            return;
        }

        if (!string.Equals(current, QuotedPath, StringComparison.OrdinalIgnoreCase))
        {
            key.Set(ValueName, QuotedPath);
        }
    }
}
