namespace LGAC.Linux;

internal enum LaunchMode
{
    Run,
    Version,
    Smoke,
    Invalid,
}

internal static class ProgramArguments
{
    public static LaunchMode Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return LaunchMode.Run;
        }

        if (args.Length > 1)
        {
            return LaunchMode.Invalid;
        }

        return args[0] switch
        {
            "--version" => LaunchMode.Version,
            "--smoke" => LaunchMode.Smoke,
            _ => LaunchMode.Invalid,
        };
    }

    public static string VersionText(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "lg-ac desconhecida";
        }

        var plus = informationalVersion.IndexOf('+');
        var version = (plus >= 0 ? informationalVersion[..plus] : informationalVersion).Trim();
        return version.Length == 0 ? "lg-ac desconhecida" : "lg-ac " + version;
    }
}
