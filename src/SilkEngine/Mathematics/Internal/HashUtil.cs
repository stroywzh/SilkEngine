namespace SilkEngine.Mathematics.Internal;

/// <summary>
/// Shared hash code combination for math value types. Uses the classic "h * 397 ^ component" sequence,
/// so all structs stay hash-consistent with each other. Values are hashed as their raw bits, which
/// keeps <see cref="object.GetHashCode"/> consistent with the exact <c>operator ==</c> of each type.
/// </summary>
internal static class HashUtil
{
    public static int Of(float a) => a.GetHashCode();

    public static int Of(float a, float b)
    {
        unchecked
        {
            return a.GetHashCode() * 397 ^ b.GetHashCode();
        }
    }

    public static int Of(float a, float b, float c)
    {
        unchecked
        {
            int h = a.GetHashCode();
            h = h * 397 ^ b.GetHashCode();
            h = h * 397 ^ c.GetHashCode();
            return h;
        }
    }

    public static int Of(float a, float b, float c, float d)
    {
        unchecked
        {
            int h = a.GetHashCode();
            h = h * 397 ^ b.GetHashCode();
            h = h * 397 ^ c.GetHashCode();
            h = h * 397 ^ d.GetHashCode();
            return h;
        }
    }
}
