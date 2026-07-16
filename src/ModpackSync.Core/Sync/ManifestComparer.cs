using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Sync;

namespace ModpackSync.Core.Sync;

public sealed class ManifestComparer
{
    public ManifestComparison Compare(
        ModpackManifest oldManifest,
        ModpackManifest newManifest)
    {
        ArgumentNullException.ThrowIfNull(oldManifest);
        ArgumentNullException.ThrowIfNull(newManifest);

        Dictionary<string, ManifestFile> oldFiles =
            oldManifest.Files.ToDictionary(
                file => file.RelativePath,
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ManifestFile> newFiles =
            newManifest.Files.ToDictionary(
                file => file.RelativePath,
                StringComparer.OrdinalIgnoreCase);

        var addedFiles = new List<ManifestFile>();
        var modifiedFiles = new List<ManifestFile>();
        var deletedFiles = new List<ManifestFile>();
        var unchangedFiles = new List<ManifestFile>();

        foreach (ManifestFile newFile in newFiles.Values)
        {
            if (!oldFiles.TryGetValue(
                    newFile.RelativePath,
                    out ManifestFile? oldFile))
            {
                addedFiles.Add(newFile);
                continue;
            }

            bool fileChanged =
                oldFile.Size != newFile.Size ||
                !oldFile.Sha256.Equals(
                    newFile.Sha256,
                    StringComparison.OrdinalIgnoreCase);

            if (fileChanged)
            {
                modifiedFiles.Add(newFile);
            }
            else
            {
                unchangedFiles.Add(newFile);
            }
        }

        foreach (ManifestFile oldFile in oldFiles.Values)
        {
            if (!newFiles.ContainsKey(oldFile.RelativePath))
            {
                deletedFiles.Add(oldFile);
            }
        }

        return new ManifestComparison
        {
            AddedFiles = SortByPath(addedFiles),
            ModifiedFiles = SortByPath(modifiedFiles),
            DeletedFiles = SortByPath(deletedFiles),
            UnchangedFiles = SortByPath(unchangedFiles)
        };
    }

    private static IReadOnlyList<ManifestFile> SortByPath(
        IEnumerable<ManifestFile> files)
    {
        return files
            .OrderBy(
                file => file.RelativePath,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}