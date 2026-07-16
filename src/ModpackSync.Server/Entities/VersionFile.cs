namespace ModpackSync.Server.Entities;

public sealed class VersionFile
{
    public Guid VersionId { get; set; }

    public required string RelativePath { get; set; }

    public required string Sha256 { get; set; }

    public long Size { get; set; }

    public ServerPackVersion? Version { get; set; }

    public StoredFile? StoredFile { get; set; }
}