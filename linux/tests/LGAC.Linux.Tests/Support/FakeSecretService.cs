using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests.Support;

internal sealed class FakeSecretService : ISecretService
{
    private readonly List<(Dictionary<string, string> Attributes, string Label, string Secret)> items = [];

    public bool Available { get; set; } = true;

    public bool FailLookup { get; set; }

    public bool FailStore { get; set; }

    public bool FailClear { get; set; }

    public bool FailStoreWithoutVault { get; set; }

    public bool FailAvailability { get; set; }

    public int ItemCount => items.Count;

    public string? LastLabel => items.Count == 0 ? null : items[^1].Label;

    public IReadOnlyDictionary<string, string>? LastAttributes => items.Count == 0 ? null : items[^1].Attributes;

    public bool IsAvailable() => FailAvailability ? throw new InvalidOperationException("falha") : Available;

    public string? Lookup(IReadOnlyDictionary<string, string> attributes)
    {
        if (FailLookup)
        {
            throw new SecretServiceException("falha");
        }

        var index = Find(attributes);
        return index < 0 ? null : items[index].Secret;
    }

    public void Store(string label, IReadOnlyDictionary<string, string> attributes, string secret)
    {
        if (FailStoreWithoutVault)
        {
            throw new SecretServiceException("sem cofre", noVault: true);
        }

        if (FailStore)
        {
            throw new SecretServiceException("recusado");
        }

        var index = Find(attributes);
        if (index >= 0)
        {
            items.RemoveAt(index);
        }

        items.Add((new Dictionary<string, string>(attributes), label, secret));
    }

    public void Clear(IReadOnlyDictionary<string, string> attributes)
    {
        if (FailClear)
        {
            throw new SecretServiceException("falha");
        }

        items.RemoveAll(item => Matches(item.Attributes, attributes));
    }

    private int Find(IReadOnlyDictionary<string, string> attributes) =>
        items.FindIndex(item => Matches(item.Attributes, attributes));

    private static bool Matches(Dictionary<string, string> stored, IReadOnlyDictionary<string, string> wanted) =>
        wanted.All(pair => stored.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
