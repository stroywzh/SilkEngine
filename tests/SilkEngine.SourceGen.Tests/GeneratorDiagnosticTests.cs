using Microsoft.CodeAnalysis;
using Xunit;

namespace SilkEngine.SourceGen.Tests;

public class GeneratorDiagnosticTests
{
    [Fact]
    public void ExternalAssembly_SerializableInternal_ErrorsSeng001()
    {
        var (_, diags) = GeneratorHarness.Run("""
            using SilkEngine.Scene;
            using SilkEngine.Scene.Serialization;

            [SerializableInternal]
            public class Bad : Component { }
            """, assemblyName: "UserApp");
        Assert.Contains(diags, d => d.Id == "SENG001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SerializableInternal_NonWhitelistField_ErrorsSeng002()
    {
        var (_, diags) = GeneratorHarness.Run("""
            using SilkEngine.Scene;
            using SilkEngine.Scene.Serialization;

            [SerializableInternal]
            public class Bad : Component
            {
                public float Good = 1f;
                public System.Numerics.Vector2 Pos;
            }
            """, assemblyName: "SilkEngine");
        Assert.Contains(diags, d => d.Id == "SENG002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SerializableInternal_ExcludedField_NoSeng002()
    {
        var (_, diags) = GeneratorHarness.Run("""
            using SilkEngine.Scene;
            using SilkEngine.Scene.Serialization;

            [SerializableInternal]
            public class Ok : Component
            {
                public float Good = 1f;
                [NoSerializeField] public System.Numerics.Vector2 Skip;
            }
            """, assemblyName: "SilkEngine");
        Assert.DoesNotContain(diags, d => d.Id == "SENG002");
    }

    [Fact]
    public void SerializableInternal_Unregistered_ErrorsSeng003()
    {
        var (_, diags) = GeneratorHarness.Run("""
            using SilkEngine.Scene;
            using SilkEngine.Scene.Serialization;

            [SerializableInternal]
            public class Bad : Component { }
            """, assemblyName: "SilkEngine");
        Assert.Contains(diags, d => d.Id == "SENG003" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SerializableInternal_Registered_NoSeng003()
    {
        var (_, diags) = GeneratorHarness.Run("""
            using SilkEngine.Scene;
            using SilkEngine.Scene.Serialization;

            namespace SilkEngine.Scene.Serialization
            {
                // 本地同名前缀类型承载 Register<T>（CS0436 遮蔽警告可忽略），保持片段自包含
                public static class ComponentTypeRegistry
                {
                    public static void Register<T>() where T : class, new() { }
                }
            }

            [SerializableInternal]
            public class Ok : Component { }

            public static class Reg
            {
                static Reg() => ComponentTypeRegistry.Register<Ok>();
            }
            """, assemblyName: "SilkEngine");
        Assert.DoesNotContain(diags, d => d.Id == "SENG003");
    }

    [Fact]
    public void SerializableInternal_OnNonComponent_ErrorsSeng004()
    {
        var (_, diags) = GeneratorHarness.Run("""
            using SilkEngine.Core;
            using SilkEngine.Scene.Serialization;

            [SerializableInternal]
            public class NotComp : Object { }
            """, assemblyName: "SilkEngine");
        Assert.Contains(diags, d => d.Id == "SENG004" && d.Severity == DiagnosticSeverity.Error);
    }
}
