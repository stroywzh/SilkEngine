using System;

namespace SilkEngine.Scene.Serialization;

/// <summary>字段排除序列化（默认全字段序列化）。</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NoSerializeFieldAttribute : Attribute { }

/// <summary>引擎内部序列化组件标记：仅限 SilkEngine 程序集使用（生成器 SENG001 强制）。</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SerializableInternalAttribute : Attribute { }
