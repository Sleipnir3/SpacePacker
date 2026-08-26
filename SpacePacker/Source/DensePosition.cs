using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SpacePacker
{
    public enum Axis : byte
    {
        X = 0, Y = 1, Z = 2,
    }

    /// <summary>
    /// 高密度坐标模板
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct DensePosition
    {
        [FieldOffset(0)]
        private ulong Position;

        private const uint mask = 0xFFFFF;
        /// <summary>
        /// 获取原始 64 位坐标镜像
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetRawPositionBits() => Position;

        // --- 底层：位运算逻辑 (Raw Bitwise) ---
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetSingleRawPos(Axis axis)
        {
            int offset = 20 * (int)axis;
            return (uint)((Position >> offset) & mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRawPos(Axis axis, uint rawValue)
        {

            //mask diffusion!
            uint gate = (uint)(((long)rawValue - (long)(mask + 1)) >> 63);

            int shift = 20 * (int)axis;
            ulong effectiveMask = (ulong)mask & gate;

            // 清理目标位置并写入新值
            Position &= ~(effectiveMask << shift);
            Position |= ((ulong)(rawValue & (uint)effectiveMask) << shift);
        }
        /// <summary>
        /// [Dangerious!!]全量写入Position数据，且不做任何校验
        /// </summary>
        /// <param name="positionData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateRawPosition(ulong positionData)
        {
            Position = positionData & ((1UL << 60) - 1);
        }
    }
}
