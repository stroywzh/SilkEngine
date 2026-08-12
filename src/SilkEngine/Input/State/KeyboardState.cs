using System.Collections.Generic;
using SilkEngine.InputSystem;

namespace SilkEngine;

public class KeyboardState
{
    private HashSet<KeyCode> _prev = new(),
        _curr = new();

    public bool GetKey(KeyCode key) => _curr.Contains(key);

    public bool GetKeyDown(KeyCode key) => !_prev.Contains(key) && _curr.Contains(key);

    public bool GetKeyUp(KeyCode key) => _prev.Contains(key) && !_curr.Contains(key);

    public bool AnyKey => _curr.Count > 0;

    public void SwapBuffers()
    {
        _prev = _curr;
        _curr = new HashSet<KeyCode>();
    }

    public void SetKey(KeyCode key, bool pressed)
    {
        if (pressed)
        {
            _curr.Add(key);
        }
    }
}
