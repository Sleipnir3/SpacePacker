using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


namespace GameWorld.ECS
{
    public enum Axis : byte
    {
        X = 0, Y = 1, Z = 2,
    }

    // --- 1. ECS 组件定义  ---
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EcsAlchemicalData : IComponentData
    {
        private const uint mask = 0xFFFFF;

        // StateSignature 覆盖 Position (0-7) 与 PackedData (8-11)
        [FieldOffset(0)]
        public uint4 StateSignature;
        [FieldOffset(0)]
        private ulong Position;
        [FieldOffset(8)]
        public uint _padding01;
        [FieldOffset(12)]
        private uint _padding02;
        [FieldOffset(16)]
        private uint4 _padding03;

        /// <summary>
        /// 获取原始 64 位坐标镜像
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetRawPositionBits() => Position;

        // --- 底层：位运算逻辑 (Raw Bitwise) ---
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetRawPos(Axis axis)
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
