using System.IO;
using System.Text;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Core.AssetSystem;
using SilkEngine.Core.IO;
using SilkEngine.Serialization;

namespace SilkEngine.Asset;

/// <summary>Catalog adapter for texture sources: reads the schema v2 sidecar and its content-addressed artifact.</summary>
internal sealed class TextureSidecarAdapter : IAssetCatalogAdapter
{
    /// <inheritdoc />
    public string SourceExtension => PngAssetImporter.PngExtension;

    /// <inheritdoc />
    public bool TryCreateEntry(
        ContentLayout layout,
        string sourcePath,
        out AssetCatalogEntry entry
    )
    {
        entry = null!;

        string sidecarPath = SidecarPath.ForSource(sourcePath);
        if (!File.Exists(sidecarPath))
        {
            Log.Debug($"Skipping source '{sourcePath}': no sidecar found.");
            return false;
        }

        TextureSidecar sidecar;
        try
        {
            sidecar = TextureSidecar.Reader.Read(sidecarPath);
        }
        catch (Exception error) when (error is InvalidDataException or IOException)
        {
            Log.Warning($"Skipping sidecar '{sidecarPath}': {error.Message}");
            return false;
        }

        if (sidecar.Artifact is not { } artifact)
        {
            Log.Warning(
                $"Skipping sidecar '{sidecarPath}': no artifact. Reimport the asset and rebuild the catalog "
                    + "(the runtime loader no longer decodes sources)."
            );
            return false;
        }

        if (!AssetGuid.TryParse(sidecar.Guid, out AssetGuid guid) || !guid.IsValid)
        {
            Log.Warning($"Skipping sidecar '{sidecarPath}': missing or invalid guid.");
            return false;
        }

        entry = new AssetCatalogEntry(
            guid,
            sidecar.AssetType,
            Path.GetRelativePath(layout.ProjectRoot, sourcePath),
            artifact.Hash,
            artifact.Length,
            Encoding.UTF8.GetString(JsonCodec.Encode(sidecar.ToTexture(), humanReadable: false)),
            sidecar.SourceHash,
            sidecar.ImportSettingsHash,
            sidecar.ImporterVersion
        );
        return true;
    }
}
