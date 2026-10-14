namespace HomeSpeaker.Shared.Temperature;

#nullable enable

public sealed class UnmatchedDevice
{
    public required string Name { get; init; }
    public required string Sku { get; init; }
}
