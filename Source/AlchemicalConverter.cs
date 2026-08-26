using System.Runtime.CompilerServices;

namespace SpacePacker
{
    /// <summary>
    /// 定义压缩配方：精度（步长）与符号模式
    /// </summary>
    public interface IAlchemicalRecipe
    {
        /// <summary>
        /// 十进制步长（精度主参数），如 0.001f
        /// </summary>
        float Step { get; }

        /// <summary>
        /// true: (1+19) 符号-幅值模式（bit19=符号, bit0~18=幅值, 零点精确）；
        /// false: 20 位无符号幅值模式（量程 [0, 2^20 × Step)）
        /// </summary>
        bool EnableSymbol { get; }
    }

    /// <summary>
    /// 默认位置配方：符号-幅值模式，步长 0.001，量程 ±524.287，零点精确
    /// </summary>
    public struct DefaultPositionRecipe : IAlchemicalRecipe
    {
        public readonly bool EnableSymbol => true;
        public readonly float Step => 0.001f;
    }

    /// <summary>
    /// 压缩转换器 (Alchemical Converter)
    /// 采用泛型配方模式，确保编译器（JIT/AOT）能够进行常量内联，实现零开销转换。
    /// </summary>
    public static class AlchemicalConverter
    {
        // 20 位字段掩码
        private const uint FieldMask = 0xFFFFF;
        // 符号模式：幅值掩码 (bit0~18) 与符号位 (bit19)
        private const uint MagnitudeMask = 0x7FFFF;
        private const uint SignBit = 1u << 19;

        /// <summary>
        /// 配方派生缓存：每个 T 仅初始化一次（CLR 保证线程安全）。
        /// 由十进制步长 Step 派生出乘法用的 Scale，避免每次转换做除法。
        /// </summary>
        private static class RecipeCache<T> where T : struct, IAlchemicalRecipe
        {
            /// <summary>
            /// 刻度密度（每 1.0 单位的刻度数）= 1 / Step
            /// </summary>
            public static readonly float Scale = Initialize();

            private static float Initialize()
            {
                T recipe = default;
                float scale = 1f / recipe.Step;
                float maxRange = recipe.EnableSymbol
                    ? MagnitudeMask * recipe.Step
                    : FieldMask * recipe.Step;
                string rangeDesc = recipe.EnableSymbol
                    ? $"±{maxRange}"
                    : $"0 ~ {maxRange}";
                System.Diagnostics.Debug.WriteLine(
                    $"[SpacePacker] Recipe<{typeof(T).Name}> 初始化: " +
                    $"EnableSymbol={recipe.EnableSymbol}, Step={recipe.Step}, " +
                    $"Scale={scale}, Range={rangeDesc}");
                return scale;
            }
        }

        // --- 核心单值转换 ---

        /// <summary>
        /// 将浮点坐标量化为 20 位原始值。越界时返回 0（塌缩语义）。
        /// 量化采用四舍五入（+0.5 后截断），最大量化误差为半个步长。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToRaw<T>(float value) where T : struct, IAlchemicalRecipe
        {
            T recipe = default;
            float scale = RecipeCache<T>.Scale;

            if (recipe.EnableSymbol)
            {
                // (1+19) 符号-幅值模式
                float abs = value < 0f ? -value : value;
                uint mag = (uint)(abs * scale + 0.5f);
                if (mag > MagnitudeMask) return 0; // 越界塌缩
                uint sign = value < 0f ? SignBit : 0u; // -0f 归一为 +0
                return mag | sign;
            }
            else
            {
                // 20 位无符号幅值模式
                if (value < 0f) return 0;
                uint raw = (uint)(value * scale + 0.5f);
                if (raw > FieldMask) return 0; // 越界塌缩
                return raw;
            }
        }

        /// <summary>
        /// 将 20 位原始值还原为浮点坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat<T>(uint rawValue) where T : struct, IAlchemicalRecipe
        {
            T recipe = default;

            if (recipe.EnableSymbol)
            {
                // (1+19) 符号-幅值模式：解码直接乘 Step，往返精度最佳
                float v = (rawValue & MagnitudeMask) * recipe.Step;
                return (rawValue & SignBit) != 0 ? -v : v;
            }
            else
            {
                return rawValue * recipe.Step;
            }
        }

        /// <summary>
        /// 将 3D 坐标打包为 60 位原始位串 (20位/轴，符号位随字段位于 bit 19/39/59)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PackPosition<T>(float x, float y, float z) where T : struct, IAlchemicalRecipe
        {
            uint rx = ToRaw<T>(x);
            uint ry = ToRaw<T>(y);
            uint rz = ToRaw<T>(z);
            return (ulong)rx | ((ulong)ry << 20) | ((ulong)rz << 40);
        }

        /// <summary>
        /// 将 60 位原始位串解包并还原为 3D 浮点坐标
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnpackPosition<T>(ulong rawValue, out float x, out float y, out float z) where T : struct, IAlchemicalRecipe
        {
            x = ToFloat<T>((uint)(rawValue & FieldMask));
            y = ToFloat<T>((uint)((rawValue >> 20) & FieldMask));
            z = ToFloat<T>((uint)((rawValue >> 40) & FieldMask));
        }
    }
}
