namespace SilkEngine.InputSystem;

/// <summary>
/// 按键码枚举（引擎抽象键；SilkInputProvider 映射为 Silk.NET Key）。
/// 仅覆盖引擎常用键，未列出的物理键映射为 Key.Unknown 忽略。
/// </summary>
public enum KeyCode
{
    /// <summary>空值（日志/轮询跳过）</summary>
    None,

    /// <summary>空格键</summary>
    Space,

    /// <summary>回车键</summary>
    Enter,

    /// <summary>Esc 键</summary>
    Escape,

    /// <summary>Tab 键</summary>
    Tab,

    /// <summary>退格键</summary>
    Backspace,

    /// <summary>删除键</summary>
    Delete,

    /// <summary>字母键 A</summary>
    A,

    /// <summary>字母键 B</summary>
    B,

    /// <summary>字母键 C</summary>
    C,

    /// <summary>字母键 D</summary>
    D,

    /// <summary>字母键 E</summary>
    E,

    /// <summary>字母键 F</summary>
    F,

    /// <summary>字母键 G</summary>
    G,

    /// <summary>字母键 H</summary>
    H,

    /// <summary>字母键 I</summary>
    I,

    /// <summary>字母键 J</summary>
    J,

    /// <summary>字母键 K</summary>
    K,

    /// <summary>字母键 L</summary>
    L,

    /// <summary>字母键 M</summary>
    M,

    /// <summary>字母键 N</summary>
    N,

    /// <summary>字母键 O</summary>
    O,

    /// <summary>字母键 P</summary>
    P,

    /// <summary>字母键 Q</summary>
    Q,

    /// <summary>字母键 R</summary>
    R,

    /// <summary>字母键 S</summary>
    S,

    /// <summary>字母键 T</summary>
    T,

    /// <summary>字母键 U</summary>
    U,

    /// <summary>字母键 V</summary>
    V,

    /// <summary>字母键 W</summary>
    W,

    /// <summary>字母键 X</summary>
    X,

    /// <summary>字母键 Y</summary>
    Y,

    /// <summary>字母键 Z</summary>
    Z,

    /// <summary>数字键 0</summary>
    D0,

    /// <summary>数字键 1</summary>
    D1,

    /// <summary>数字键 2</summary>
    D2,

    /// <summary>数字键 3</summary>
    D3,

    /// <summary>数字键 4</summary>
    D4,

    /// <summary>数字键 5</summary>
    D5,

    /// <summary>数字键 6</summary>
    D6,

    /// <summary>数字键 7</summary>
    D7,

    /// <summary>数字键 8</summary>
    D8,

    /// <summary>数字键 9</summary>
    D9,

    /// <summary>左 Shift 键</summary>
    LeftShift,

    /// <summary>右 Shift 键</summary>
    RightShift,

    /// <summary>左 Ctrl 键</summary>
    LeftControl,

    /// <summary>右 Ctrl 键</summary>
    RightControl,

    /// <summary>左 Alt 键</summary>
    LeftAlt,

    /// <summary>右 Alt 键</summary>
    RightAlt,

    /// <summary>左方向键</summary>
    LeftArrow,

    /// <summary>右方向键</summary>
    RightArrow,

    /// <summary>上方向键</summary>
    UpArrow,

    /// <summary>下方向键</summary>
    DownArrow,

    /// <summary>功能键 F1</summary>
    F1,

    /// <summary>功能键 F2</summary>
    F2,

    /// <summary>功能键 F3</summary>
    F3,

    /// <summary>功能键 F4</summary>
    F4,

    /// <summary>功能键 F5</summary>
    F5,

    /// <summary>功能键 F6</summary>
    F6,

    /// <summary>功能键 F7</summary>
    F7,

    /// <summary>功能键 F8</summary>
    F8,

    /// <summary>功能键 F9</summary>
    F9,

    /// <summary>功能键 F10</summary>
    F10,

    /// <summary>功能键 F11</summary>
    F11,

    /// <summary>功能键 F12</summary>
    F12,
}
