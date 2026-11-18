namespace LGAC.Core;

public abstract record ACPhase
{
    private ACPhase()
    {
    }

    public sealed record NeedsSetup(string? Message) : ACPhase;

    public sealed record Loading : ACPhase;

    public sealed record ChoosingDevice(IReadOnlyList<ThinQDevice> Devices) : ACPhase
    {
        public bool Equals(ChoosingDevice? other)
        {
            return other is not null && Devices.SequenceEqual(other.Devices);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var device in Devices)
            {
                hash.Add(device);
            }

            return hash.ToHashCode();
        }
    }

    public sealed record Ready : ACPhase;
}
