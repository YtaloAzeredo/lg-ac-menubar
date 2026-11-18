using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests.Support;

internal static class DBusTestEnvironment
{
    public const string UnreachableAddress = "unix:path=/nonexistent/lg-ac-test/bus";

    public static string? SessionAddress => Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");

    public static void RequireSessionBus()
    {
        if (!OperatingSystem.IsLinux())
        {
            Unavailable("Requer Linux");
        }

        if (string.IsNullOrEmpty(SessionAddress))
        {
            Unavailable("Requer Linux com D-Bus de sessão");
        }
    }

    public static DBusSecretService RequireSecretService()
    {
        RequireSessionBus();
        var service = new DBusSecretService(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60));
        if (!service.IsAvailable())
        {
            Unavailable("Serviço de segredos indisponível");
        }

        return service;
    }

    private static void Unavailable(string reason)
    {
        if (Environment.GetEnvironmentVariable("LGAC_REQUIRE_DBUS") == "1")
        {
            Assert.Fail($"LGAC_REQUIRE_DBUS=1: {reason}");
        }

        Assert.Inconclusive(reason);
    }
}
