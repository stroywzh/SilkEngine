using Microsoft.CodeAnalysis;
using Xunit;

namespace SilkEngine.SourceGen.Tests;

public class GeneratorSnapshotTests
{
    private const string WhitelistSnippet = """
        using System;
        using SilkEngine;
        using SilkEngine.Math;

        public partial class SampleWhitelist : MonoBehaviour
        {
            public float Speed = 1f;
            public bool Lit = true;
            public string? Label;
            public Guid Id;
            public Vector3 Offset;
            public Quaternion Rotation;
        }
        """;

    private const string RecursionSnippet = """
        using SilkEngine;
        using SilkEngine.Scene.Serialization;

        public partial class SampleStats : MonoBehaviour
        {
            public float HP = 100f;
            [NoSerializeField] public float Hidden = 7f;
        }

        public partial class SampleOuter : MonoBehaviour
        {
            public float Power = 2f;
            public SampleStats Stats = new();
        }
        """;

    private const string StjSnippet = """
        using System;
        using SilkEngine;

        public partial class SampleStj : MonoBehaviour
        {
            public string[] Tags = Array.Empty<string>();
            public DateTime Stamp;
        }
        """;

    private const string AssetSnippet = """
        using SilkEngine;
        using SilkEngine.Render;

        public partial class SampleAssets : Component
        {
            private Shader? _shader;
            public Shader? Shader { get => _shader; set => _shader = value; }
        }
        """;

    private static readonly string SourceSnapshotsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Snapshots"));

    private static void AssertSnapshot(string name, string actual)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", name);
        actual = GeneratorHarness.Normalize(actual);
        if (Environment.GetEnvironmentVariable("GEN_REGEN") == "1")
        {
            File.WriteAllText(Path.Combine(SourceSnapshotsDir, name), actual + Environment.NewLine);
            return;
        }
        Assert.Equal(GeneratorHarness.Normalize(File.ReadAllText(path)), actual);
    }

    [Fact]
    public void WhitelistComponent_GeneratedCode_MatchesSnapshot()
    {
        var (generated, diags) = GeneratorHarness.Run(WhitelistSnippet);
        Assert.Empty(diags.Where(d => d.Severity == DiagnosticSeverity.Error));
        AssertSnapshot("SampleWhitelist.g.cs", Assert.Single(generated));
    }

    [Fact]
    public void RecursiveExpansion_FlattenedKeys_MatchesSnapshot()
    {
        var (generated, _) = GeneratorHarness.Run(RecursionSnippet);
        AssertSnapshot("SampleOuter.g.cs",
            Assert.Single(generated, g => g.Contains("partial class SampleOuter")));
    }

    [Fact]
    public void StjFallback_ExternalTypes_MatchesSnapshot()
    {
        var (generated, _) = GeneratorHarness.Run(StjSnippet);
        AssertSnapshot("SampleStj.g.cs", Assert.Single(generated));
    }

    [Fact]
    public void AssetField_PropertyAware_MatchesSnapshot()
    {
        var (generated, _) = GeneratorHarness.Run(AssetSnippet);
        AssertSnapshot("SampleAssets.g.cs", Assert.Single(generated));
    }

    [Fact]
    public void GeneratedCode_Recompiles_WithoutErrors()
    {
        Assert.Empty(GeneratorHarness.RunCompileCheck(WhitelistSnippet));
        Assert.Empty(GeneratorHarness.RunCompileCheck(RecursionSnippet));
        Assert.Empty(GeneratorHarness.RunCompileCheck(StjSnippet));
    }
}
