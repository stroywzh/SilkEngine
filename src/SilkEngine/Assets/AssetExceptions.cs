namespace SilkEngine.Assets;

/// <summary>资产管线上下文包装异常：仅在需要携带资产管线上下文时使用；错误语义优先采用 BCL 异常</summary>
public sealed class AssetException : Exception
{
    /// <summary>创建资产异常</summary>
    /// <param name="message">错误描述</param>
    public AssetException(string message) : base(message) { }

    /// <summary>创建资产异常并携带内部异常</summary>
    /// <param name="message">错误描述</param>
    /// <param name="innerException">内部异常</param>
    public AssetException(string message, Exception innerException) : base(message, innerException) { }
}
