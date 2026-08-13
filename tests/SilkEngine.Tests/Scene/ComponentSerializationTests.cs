using System.Text.Json.Nodes;
using SilkEngine;
using SilkEngine.Core.Assets.Serialization;
using Xunit;

namespace SilkEngine.Tests.Scene;

[Collection("Serialization")]
public class ComponentSerializationTests
{
    private class PlainComponent : Component { }

    [Fact]
    public void BaseVirtuals_DefaultNoOp_NoThrow()
    {
        var c = new PlainComponent();
        var node = new SerializedNode(new JsonObject());
        c.WriteTo(node);
        c.ReadFrom(node);
        Assert.Equal("{}", node.AsJson());   // 基类空默认：不写任何键
    }
}
