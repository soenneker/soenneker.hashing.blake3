using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Soenneker.Hashing.Blake3.Simd;

internal static class VectorCompat
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Load<T>(ref T source) where T : struct
    {
        return Vector128.LoadUnsafe(ref source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Load<T>(ref T source, nuint elementOffset) where T : struct
    {
        return Vector128.LoadUnsafe(ref source, elementOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store<T>(Vector128<T> vector, ref T destination) where T : struct
    {
        vector.StoreUnsafe(ref destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store<T>(Vector128<T> vector, ref T destination, nuint elementOffset) where T : struct
    {
        vector.StoreUnsafe(ref destination, elementOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store<T>(Vector256<T> vector, ref T destination) where T : struct
    {
        vector.StoreUnsafe(ref destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store<T>(Vector256<T> vector, ref T destination, nuint elementOffset) where T : struct
    {
        vector.StoreUnsafe(ref destination, elementOffset);
    }
}