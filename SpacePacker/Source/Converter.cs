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
        /// false: 20 位无符号幅值模式（量程 [0, 2^20 × Step)）。
        /// 注意：字段保留用于配方描述与兼容，当前实现强制只支持有符号模式，
        /// 转换路径不再根据该字段分支。
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
    /// 转换器
    /// </summary>
    public static class Converter
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
                // 强制有符号模式：量程固定为符号-幅值模式的 ±(2^19-1) × Step
                float maxRange = MagnitudeMask * recipe.Step;
                System.Diagnostics.Debug.WriteLine(
                    $"[SpacePacker] Recipe<{typeof(T).Name}> 初始化: " +
                    $"EnableSymbol={recipe.EnableSymbol}(保留字段, 强制有符号), Step={recipe.Step}, " +
                    $"Scale={scale}, Range=±{maxRange}");
                return scale;
            }
        }

        // --- 核心单值转换 ---

        /// <summary>
        /// 将浮点坐标量化为 20 位原始值。越界时返回 0（塌缩语义）。
        /// 量化采用四舍五入（+0.5 后截断），最大量化误差为半个步长。
        /// 强制有符号模式：无分支直算 (1+19) 符号-幅值，避免 CPU 分支预测中断。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToRaw<T>(float value) where T : struct, IAlchemicalRecipe
        {
            float scale = RecipeCache<T>.Scale;

            // (1+19) 符号-幅值模式（固定路径，无符号分支已移除）
            float abs = value < 0f ? -value : value;
            uint mag = (uint)(abs * scale + 0.5f);
            if (mag > MagnitudeMask) return 0; // 越界塌缩
            uint sign = value < 0f ? SignBit : 0u; // -0f 归一为 +0
            return mag | sign;
        }

        /// <summary>
        /// 将 20 位原始值还原为浮点坐标。
        /// 强制有符号模式：无分支直算符号-幅值解码。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat<T>(uint rawValue) where T : struct, IAlchemicalRecipe
        {
            T recipe = default;

            // (1+19) 符号-幅值模式（固定路径）：解码直接乘 Step，往返精度最佳
            float v = (rawValue & MagnitudeMask) * recipe.Step;
            return (rawValue & SignBit) != 0 ? -v : v;
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
