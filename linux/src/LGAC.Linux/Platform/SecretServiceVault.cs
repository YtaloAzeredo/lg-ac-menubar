using LGAC.Core;

namespace LGAC.Linux.Platform;

public interface ISecretService
{
    bool IsAvailable();

    string? Lookup(IReadOnlyDictionary<string, string> attributes);

    void Store(string label, IReadOnlyDictionary<string, string> attributes, string secret);

    void Clear(IReadOnlyDictionary<string, string> attributes);
}

public sealed class SecretServiceException(string message, Exception? inner = null, bool noVault = false) : Exception(message, inner)
{
    public bool NoVault { get; } = noVault;
}

public sealed class SecretServiceVault(ISecretService service) : ITokenVault
{
    private const string Label = "LG AC";

    private static readonly IReadOnlyDictionary<string, string> Attributes = new Dictionary<string, string>
    {
        ["application"] = "lg-ac",
        ["kind"] = "thinq-token",
    };

    public string? Read() => service.Lookup(Attributes);

    public void Save(string token) => service.Store(Label, Attributes, token);

    public void Delete() => service.Clear(Attributes);
}
