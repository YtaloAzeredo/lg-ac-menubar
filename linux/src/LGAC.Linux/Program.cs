using System.Reflection;
using Avalonia;

namespace LGAC.Linux;

internal static class Program
{
    private static readonly TimeSpan SmokeLimit = TimeSpan.FromSeconds(20);

    [STAThread]
    public static int Main(string[] args)
    {
        switch (ProgramArguments.Parse(args))
        {
            case LaunchMode.Version:
                Console.Out.WriteLine(ProgramArguments.VersionText(InformationalVersion()));
                return 0;
            case LaunchMode.Invalid:
                Console.Error.WriteLine("Uso: lg-ac [--version | --smoke]");
                return 64;
            case LaunchMode.Smoke:
                App.RealLaunch = LaunchMode.Smoke;
                StartWatchdog();
                return Start(args, "lg-ac --smoke");
            default:
                App.RealLaunch = LaunchMode.Run;
                return Start(args, "lg-ac");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static int Start(string[] args, string name)
    {
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(name + ": falha (" + error.GetType().Name + ")");
            return 1;
        }
    }

    private static string? InformationalVersion()
    {
        return typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }

    private static void StartWatchdog()
    {
        var watchdog = new Thread(() =>
        {
            Thread.Sleep(SmokeLimit);
            Console.Error.WriteLine("lg-ac --smoke: tempo esgotado");
            Environment.Exit(2);
        })
        {
            IsBackground = true,
        };
        watchdog.Start();
    }
}
