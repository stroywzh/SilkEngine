using SilkEngine.Core;

namespace SilkEngine.Runtime;

internal static class UpdateDriver
{
    private static readonly List<IUpdateable> _snapShot = new();
    private static readonly HashSet<IUpdateable> _updateables = new();

    public static void Register(IUpdateable updateable)
    {
        if (updateable is not null)
        {
            if (_updateables.Contains(updateable))
            {
                Log.Debug($"重复注册{updateable.GetType().FullName}");
                return;
            }
            _updateables.Add(updateable);
        }
    }

    public static void UnRegister(IUpdateable updateable)
    {
        if (updateable is not null)
        {
            if (!_updateables.Contains(updateable))
            {
                Log.Debug($"无效注销{updateable.GetType().FullName}");
                return;
            }

            _updateables.Remove(updateable);
        }
    }

    internal static void RefreshSnapshot()
    {
        _snapShot.Clear();
        _snapShot.AddRange(_updateables);
    }

    internal static void UpdateInternal()
    {
        foreach (var u in _snapShot)
        {
            u.UpdateInternal();
        }
    }

    internal static void LateUpdateInternal()
    {
        foreach (var u in _snapShot)
        {
            u.LateUpdateInternal();
        }
    }

    internal static void FixedUpdateInternal()
    {
        foreach (var u in _snapShot)
        {
            u.FixedUpdateInternal();
        }
    }
}
