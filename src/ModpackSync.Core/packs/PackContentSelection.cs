namespace ModpackSync.Core.Packs;

public sealed class PackContentSelection
{
    public List<string> IncludedPaths
    {
        get;
        init;
    } = [];

    public List<string> ExcludedPaths
    {
        get;
        init;
    } = [];
}