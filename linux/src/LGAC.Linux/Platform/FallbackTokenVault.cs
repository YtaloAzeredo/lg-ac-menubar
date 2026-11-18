using LGAC.Core;

namespace LGAC.Linux.Platform;

public sealed class FallbackTokenVault(ISecretService service, ITokenVault file) : ITokenVault
{
    private readonly SecretServiceVault secrets = new(service);

    public string? Read()
    {
        try
        {
            if (ServiceAvailable() && secrets.Read() is { } token)
            {
                return token;
            }
        }
        catch (Exception)
        {
        }

        try
        {
            return file.Read();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(string token)
    {
        if (!ServiceAvailable())
        {
            file.Save(token);
            return;
        }

        try
        {
            secrets.Save(token);
        }
        catch (SecretServiceException exception) when (exception.NoVault)
        {
            file.Save(token);
            return;
        }

        try
        {
            file.Delete();
        }
        catch (Exception)
        {
        }
    }

    public void Delete()
    {
        try
        {
            secrets.Delete();
        }
        catch (Exception)
        {
        }

        try
        {
            file.Delete();
        }
        catch (Exception)
        {
        }
    }

    private bool ServiceAvailable()
    {
        try
        {
            return service.IsAvailable();
        }
        catch (Exception)
        {
            return false;
        }
    }
}
