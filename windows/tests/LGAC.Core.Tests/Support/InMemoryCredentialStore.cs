namespace LGAC.Core.Tests.Support;

public sealed class InMemoryCredentialStore : ICredentialStore
{
    private string? storedToken;

    public string Country { get; set; } = "BR";

    public string? DeviceId { get; set; }

    public string? DeviceAlias { get; set; }

    public string ClientId => "lgac-test-client";

    public Exception? SaveError { get; set; }

    public Exception? ReadError { get; set; }

    public Exception? DeleteError { get; set; }

    public int VaultAccesses { get; private set; }

    public string? Token
    {
        get
        {
            VaultAccesses++;
            if (ReadError is not null)
            {
                throw ReadError;
            }

            return storedToken;
        }
        set => storedToken = value;
    }

    public void SaveToken(string token)
    {
        VaultAccesses++;
        if (SaveError is not null)
        {
            throw SaveError;
        }

        storedToken = token;
    }

    public void DeleteToken()
    {
        VaultAccesses++;
        if (DeleteError is not null)
        {
            throw DeleteError;
        }

        storedToken = null;
    }

    public static InMemoryCredentialStore Configured()
    {
        return new InMemoryCredentialStore
        {
            Token = "tok",
            DeviceId = "ac-1",
            DeviceAlias = "Ar do Quarto",
        };
    }
}
