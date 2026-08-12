using System;

namespace SilkEngine.Render;

/// <summary>
/// GPU 材质状态
/// <br/>uniform + 着色器绑定
/// <br/>由后端从 Material 数据创建
/// </summary>
public interface IMaterial : IDisposable
{
    /// <summary>
    /// 应用着色器
    /// <br/>绑定着色器并设置 uniform 值，准备绘制
    /// </summary>
    void Apply();
}
