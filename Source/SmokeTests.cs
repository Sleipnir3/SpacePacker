using System;
using System.Diagnostics;

namespace SpacePacker.Tests
{
    // 无符号模式测试配方
    public struct UnsignedRecipe : IAlchemicalRecipe
    {
        public readonly bool EnableSymbol => false;
        public readonly float Step => 0.001f;
    }

    /// <summary>
    /// 冒烟测试入口（控制台程序）。
    /// 将本文件与 Source 下两个源码文件一起编译为控制台程序即可运行：
    /// 全部通过时输出 ALL PASS 并以退出码 0 结束，否则输出 FAIL 并以退出码 1 结束。
    /// </summary>
    public static class SmokeTests
    {
        public static int Main()
        {
            // 捕获 Debug.WriteLine 输出，验证一次性初始化日志
            var listener = new TextWriterTraceListener(Console.Out);
            Trace.Listeners.Add(listener);

            // === 符号-幅值模式（默认配方） ===

            // 1. 正负坐标往返
            float ox = 12.5f, oy = -3.25f, oz = 100f;
            ulong packed = AlchemicalConverter.PackPosition<DefaultPositionRecipe>(ox, oy, oz);
            AlchemicalConverter.UnpackPosition<DefaultPositionRecipe>(packed, out float x, out float y, out float z);
            Console.WriteLine($"packed=0x{packed:X16}");
            Console.WriteLine($"in : ({ox}, {oy}, {oz})");
            Console.WriteLine($"out: ({x}, {y}, {z})");
            if (Math.Abs(x - ox) > 0.001f || Math.Abs(y - oy) > 0.001f || Math.Abs(z - oz) > 0.001f)
            { Console.WriteLine("FAIL: round-trip precision"); return 1; }

            // 2. 零点精确性（符号-幅值模型的核心收益）
            ulong zeroPacked = AlchemicalConverter.PackPosition<DefaultPositionRecipe>(0f, 0f, 0f);
            if (zeroPacked != 0UL) { Console.WriteLine($"FAIL: zero encodes to 0x{zeroPacked:X}"); return 1; }
            AlchemicalConverter.UnpackPosition<DefaultPositionRecipe>(zeroPacked, out float zx, out float zy, out float zz);
            if (zx != 0f || zy != 0f || zz != 0f) { Console.WriteLine("FAIL: zero decode"); return 1; }
            Console.WriteLine("zero: exact OK");

            // 3. 符号位位置：负数 X 应置 bit19，幅值四舍五入为 1500
            uint negRaw = AlchemicalConverter.ToRaw<DefaultPositionRecipe>(-1.5f);
            if ((negRaw & (1u << 19)) == 0) { Console.WriteLine("FAIL: sign bit not set"); return 1; }
            if ((negRaw & 0x7FFFF) != 1500u) { Console.WriteLine($"FAIL: magnitude = {negRaw & 0x7FFFF}, expect 1500"); return 1; }
            float negBack = AlchemicalConverter.ToFloat<DefaultPositionRecipe>(negRaw);
            if (Math.Abs(negBack - (-1.5f)) > 0.001f) { Console.WriteLine($"FAIL: negative round-trip {negBack}"); return 1; }
            Console.WriteLine($"sign bit OK: -1.5 -> 0x{negRaw:X5} -> {negBack}");

            // 4. -0f 归一化为 +0
            if (AlchemicalConverter.ToRaw<DefaultPositionRecipe>(-0f) != 0u) { Console.WriteLine("FAIL: -0f normalize"); return 1; }

            // 5. 边界：±524.287 可表示，±524.4 越界塌缩为 0
            uint maxRaw = AlchemicalConverter.ToRaw<DefaultPositionRecipe>(524.287f);
            if (maxRaw == 0u) { Console.WriteLine("FAIL: 524.287 should be representable"); return 1; }
            if (AlchemicalConverter.ToRaw<DefaultPositionRecipe>(524.4f) != 0u) { Console.WriteLine("FAIL: +overflow should collapse to 0"); return 1; }
            if (AlchemicalConverter.ToRaw<DefaultPositionRecipe>(-524.4f) != 0u) { Console.WriteLine("FAIL: -overflow should collapse to 0"); return 1; }
            float maxBack = AlchemicalConverter.ToFloat<DefaultPositionRecipe>(maxRaw);
            Console.WriteLine($"boundary: 524.287 -> 0x{maxRaw:X5} -> {maxBack}");

            // === 无符号幅值模式 ===

            // 6. 正值往返 + 负值拒绝 + 上限 1048.575
            float ux = 500.123f;
            uint uRaw = AlchemicalConverter.ToRaw<UnsignedRecipe>(ux);
            float uBack = AlchemicalConverter.ToFloat<UnsignedRecipe>(uRaw);
            if (Math.Abs(uBack - ux) > 0.001f) { Console.WriteLine($"FAIL: unsigned round-trip {uBack}"); return 1; }
            if (AlchemicalConverter.ToRaw<UnsignedRecipe>(-1f) != 0u) { Console.WriteLine("FAIL: unsigned should reject negative"); return 1; }
            if (AlchemicalConverter.ToRaw<UnsignedRecipe>(1048.575f) == 0u) { Console.WriteLine("FAIL: unsigned max should be representable"); return 1; }
            if (AlchemicalConverter.ToRaw<UnsignedRecipe>(1048.6f) != 0u) { Console.WriteLine("FAIL: unsigned overflow should collapse"); return 1; }
            Console.WriteLine($"unsigned OK: {ux} -> 0x{uRaw:X5} -> {uBack}");

            // === AlchemicalData 回归 ===

            // 7. 位操作 + 8 字节布局
            var data = new AlchemicalData();
            data.UpdateRawPosition(packed);
            if (data.GetRawPositionBits() != packed) { Console.WriteLine("FAIL: UpdateRawPosition/GetRawPositionBits"); return 1; }
            if (AlchemicalConverter.ToFloat<DefaultPositionRecipe>(data.GetRawPos(Axis.X)) != x) { Console.WriteLine("FAIL: GetRawPos X"); return 1; }
            uint newRawY = AlchemicalConverter.ToRaw<DefaultPositionRecipe>(42f);
            data.SetRawPos(Axis.Y, newRawY);
            AlchemicalConverter.UnpackPosition<DefaultPositionRecipe>(data.GetRawPositionBits(), out float x2, out float y2, out float z2);
            if (x2 != x || z2 != z || Math.Abs(y2 - 42f) > 0.001f) { Console.WriteLine($"FAIL: SetRawPos -> ({x2},{y2},{z2})"); return 1; }
            ulong before = data.GetRawPositionBits();
            data.SetRawPos(Axis.X, 0x100000); // 超出 20 位 -> 无操作
            if (data.GetRawPositionBits() != before) { Console.WriteLine("FAIL: SetRawPos out-of-range guard"); return 1; }
            int size = System.Runtime.InteropServices.Marshal.SizeOf<AlchemicalData>();
            Console.WriteLine($"sizeof(AlchemicalData) = {size}");
            if (size != 8) { Console.WriteLine("FAIL: size != 8"); return 1; }

            listener.Flush();
            Console.WriteLine("ALL PASS");
            return 0;
        }
    }
}
