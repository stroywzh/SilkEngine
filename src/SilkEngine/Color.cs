namespace SilkEngine;

public struct Color
{
    //临时占位的Color32，懒得改名字了
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }
    public byte A { get; init; }

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public System.Drawing.Color ToSystemColor() => System.Drawing.Color.FromArgb(A, R, G, B);
}
