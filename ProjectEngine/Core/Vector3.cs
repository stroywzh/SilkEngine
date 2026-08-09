using System.Runtime.CompilerServices;

namespace ProjectEngine.Core;

public struct Vector3
{
    public float x;
    public float y;
    public float z;

    public Vector3()
    {
        x = 0;
        y = 0;
        z = 0;
    }
}
// TODO:未来的扩展
// public struct Vector3<T>
//    where T:struct
// {
//     public T x;
//     public T y;
//     public T z;

//     public Vector3(T x, T y, T z)
//         : this()
//     {
//         this.x = x;
//         this.y = y;
//         this.z = z;
//     }
// }
