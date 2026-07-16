using ModpackSync.Contracts.Sync;

namespace ModpackSync.Core.Sync;

public static class FileCategoryClassifier
{
    public static FileCategory GetCategory(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return FileCategory.Other;
        }

        string normalisedPath = relativePath
            .Replace('\\', '/')
            .TrimStart('/');

        if (IsInsideFolder(
                normalisedPath,
                "config/blockbuster/models"))
        {
            return FileCategory.BlockbusterModel;
        }

        if (IsInsideFolder(
                normalisedPath,
                "config/chameleon/models"))
        {
            return FileCategory.ChameleonModel;
        }

        if (IsInsideFolder(
                normalisedPath,
                "mods"))
        {
            return FileCategory.Mod;
        }

        return FileCategory.Other;
    }

    private static bool IsInsideFolder(
        string relativePath,
        string folderPath)
    {
        return relativePath.StartsWith(
            folderPath + "/",
            StringComparison.OrdinalIgnoreCase);
    }
}