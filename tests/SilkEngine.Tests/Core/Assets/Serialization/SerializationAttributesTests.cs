using System;
using System.Linq;
using SilkEngine.Scene.Serialization;
using Xunit;

namespace SilkEngine.Tests.Core.Assets.Serialization;

public class SerializationAttributesTests
{
    [Fact]
    public void NoSerializeField_TargetsFieldsOnly()
    {
        var usage = typeof(NoSerializeFieldAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>().Single();
        Assert.Equal(AttributeTargets.Field, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void SerializableInternal_TargetsClassesOnly()
    {
        var usage = typeof(SerializableInternalAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>().Single();
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
