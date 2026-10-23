namespace SilkEngine.Runtime;

internal interface IUpdateable
{
    void UpdateInternal();
    void FixedUpdateInternal();
    void LateUpdateInternal();
}

