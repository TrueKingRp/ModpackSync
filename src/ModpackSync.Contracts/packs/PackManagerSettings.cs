namespace ModpackSync.Contracts.Packs;

public sealed record PackManagerSettings
{
    public List<ModpackRegistration> Packs { get; init; } = [];
}