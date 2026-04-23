using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace GameWorld.ECS
{
    /// <summary>
    /// 定义压缩范围与精度
    /// </summary>
    public interface IAlchemicalRecipe
    {
        float Offset { get; }
        float Scale { get; }
    }

    /// <summary>
    /// 默认位置配方：范围 [-500, 500], 精度 ~0.001 (20位)
    /// </summary>
    public struct DefaultPositionRecipe : IAlchemicalRecipe
    {
        public readonly float Offset => 500f;
        public readonly float Scale => 1048.575f;
    }

    /// <summary>
    /// 压缩转换器 (Alchemical Converter)
    /// 采用泛型配方模式，确保 Burst 编译器能够进行常量内联。
    /// </summary>
    public static class AlchemicalConverter
    {
        // --- 核心单值转换 ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToRaw<T>(float value) where T : struct, IAlchemicalRecipe
        {
            T recipe = default;
            if (value < -recipe.Offset || value > recipe.Offset) return 0;
            return (uint)((value + recipe.Offset) * recipe.Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat<T>(uint rawValue) where T : struct, IAlchemicalRecipe
        {
            T recipe = default;
            return (float)rawValue * (1.0f / recipe.Scale) - recipe.Offset;
        }

        // --- 多维向量转换 ---
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToFloat3<T>(uint3 rawValue) where T : struct, IAlchemicalRecipe
        {
            return new float3(ToFloat<T>(rawValue.x), ToFloat<T>(rawValue.y), ToFloat<T>(rawValue.z));
        }

        /// <summary>
        /// 将 3D 坐标打包为 60 位原始位串 (20位/轴)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PackPosition<T>(float3 pos) where T : struct, IAlchemicalRecipe
        {
            uint rx = ToRaw<T>(pos.x);
            uint ry = ToRaw<T>(pos.y);
            uint rz = ToRaw<T>(pos.z);
            return (ulong)rx | ((ulong)ry << 20) | ((ulong)rz << 40);
        }

        /// <summary>
        /// 将 60 位原始位串解包并还原为 3D 浮点坐标
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 UnpackPosition<T>(ulong rawValue) where T : struct, IAlchemicalRecipe
        {
            uint3 raw3 = new uint3(
                (uint)(rawValue & 0xFFFFF),
                (uint)((rawValue >> 20) & 0xFFFFF),
                (uint)((rawValue >> 40) & 0xFFFFF)
            );
            return ToFloat3<T>(raw3);
        }

    }
}
