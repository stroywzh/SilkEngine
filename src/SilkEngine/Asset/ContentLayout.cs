using System.IO;
using SilkEngine.Core.IO;

namespace SilkEngine.Asset;

/// <summary>Single source of truth for the project's content folders.</summary>
/// <remarks>Catalog, artifact store, scanner and importer all receive this layout instead of deriving a
/// project root independently from a sidecar path.</remarks>
internal sealed class ContentLayout
{
    private ContentLayout(string projectRoot)
    {
        ProjectRoot = projectRoot;
        AssetsRoot = Path.Combine(projectRoot, AssetConstants.AssetsFolderName);
        LibraryRoot = Path.Combine(projectRoot, AssetConstants.LibraryFolderName);
        ArtifactsRoot = Path.Combine(LibraryRoot, AssetConstants.ArtifactsFolderName);
        CatalogPath = Path.Combine(LibraryRoot, AssetConstants.CatalogFileName);
    }

    internal string ProjectRoot { get; }

    internal string AssetsRoot { get; }

    internal string LibraryRoot { get; }

    internal string ArtifactsRoot { get; }

    internal string CatalogPath { get; }

    internal static ContentLayout Create(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return new ContentLayout(Path.GetFullPath(projectRoot));
    }

    /// <summary>Tool/test convenience: derives the layout from a source below its <c>Assets/</c> folder.</summary>
    /// <remarks>Runtime paths pass an explicit layout; this method exists for importers invoked directly
    /// with a source path (tests, development tools).</remarks>
    internal static ContentLayout FromSourcePath(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        string directory =
            Path.GetDirectoryName(Path.GetFullPath(sourcePath))
            ?? throw new InvalidDataException($"Source path '{sourcePath}' has no directory.");

        for (DirectoryInfo? current = new(directory); current is not null; current = current.Parent)
        {
            if (!string.Equals(current.Name, AssetConstants.AssetsFolderName, PathUtil.Comparison))
                continue;

            return Create(
                current.Parent?.FullName
                    ?? throw new InvalidDataException(
                        $"Source path '{sourcePath}' has no project root above its '{AssetConstants.AssetsFolderName}' folder."
                    )
            );
        }

        return Create(Path.GetFullPath(Path.Combine(directory, "..")));
    }
}
