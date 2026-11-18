using LGAC.Core;

namespace LGAC.Linux.Platform;

internal sealed class PrimedCredentialStore(ICredentialStore inner) : ICredentialStore
{
    private readonly object gate = new();
    private bool primed;
    private string? primedToken;

    public string? Token
    {
        get
        {
            lock (gate)
            {
                if (primed)
                {
                    primed = false;
                    var value = primedToken;
                    primedToken = null;
                    return value;
                }
            }

            return inner.Token;
        }
    }

    public string Country
    {
        get => inner.Country;
        set => inner.Country = value;
    }

    public string? DeviceId
    {
        get => inner.DeviceId;
        set => inner.DeviceId = value;
    }

    public string? DeviceAlias
    {
        get => inner.DeviceAlias;
        set => inner.DeviceAlias = value;
    }

    public string ClientId => inner.ClientId;

    public void Prime()
    {
        string? value;
        try
        {
            value = inner.Token;
        }
        catch (Exception)
        {
            value = null;
        }

        lock (gate)
        {
            primedToken = value;
            primed = true;
        }
    }

    public void SaveToken(string token)
    {
        Invalidate();
        inner.SaveToken(token);
    }

    public void DeleteToken()
    {
        Invalidate();
        inner.DeleteToken();
    }

    private void Invalidate()
    {
        lock (gate)
        {
            primed = false;
            primedToken = null;
        }
    }
}
