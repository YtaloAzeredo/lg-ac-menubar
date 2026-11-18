namespace LGAC.Core.Tests.Support;

public sealed class InMemoryTokenVault : ITokenVault
{
    private string? stored;

    public int Accesses { get; private set; }

    public string? Read()
    {
        Accesses++;
        return stored;
    }

    public void Save(string token)
    {
        Accesses++;
        stored = token;
    }

    public void Delete()
    {
        Accesses++;
        stored = null;
    }
}
