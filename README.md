# SpacePacker


[简体中文](README.md) | [English](README_en.md)

---

SpacePacker 是一个零依赖、平台无关的高效**三轴无分支空间量化压缩 (Tri-axial Branchless Spatial Quantization, TBSQ)** .NET 库。它通过位运算将 12 字节的三维浮点位置数据高精度压缩为 8 字节的 `ulong`，以极大地节省内存带宽并提升缓存命中率。

*Tips：当前只实现了核心的逻辑思想。*

### 核心特性

*   **极致压缩**: 将 X、Y、Z 三个维度的浮点坐标各分配 20 位，打包进单个 `ulong` 中（共 60 位有效数据）。
*   **零依赖纯 .NET**: 仅依赖基础类库（BCL），可在任何 .NET 环境中使用（.NET / .NET Core / .NET Framework / Mono），亦可直接以源文件形式集成。
*   **编译器常量内联**: 核心转换器 `AlchemicalConverter` 基于泛型配方 (`IAlchemicalRecipe`) 设计，支持 JIT/AOT 编译器在编译期进行常量内联，实现零开销转换。
*   **双模式编码**: 每轴 20 位支持两种模式——`(1+19)` 符号-幅值模式（bit19=符号，零点精确可表示）与 20 位无符号幅值模式，由配方的 `EnableSymbol` 切换。
*   **直观的精度定制**: 配方以十进制步长 `Step`（如 `0.001f`）作为精度主参数，转换器内部一次性派生出乘法用的刻度密度，每次转换零除法开销。默认配方量程 `±524.287`，步长精确 `0.001`。
*   **紧凑的内存布局**: `AlchemicalData` 为仅 8 字节的显式布局 (`LayoutKind.Explicit`) 值类型，将 60 位压缩坐标封装为强类型，可直接嵌入各类数据结构，零额外开销。

### 编码模型

每轴 20 位的位预算遵循守恒关系：`2^位数 = 量程 × (1 / Step)`，量程与精度此消彼长（约 10⁶ 动态范围）。

```
EnableSymbol = true（符号-幅值，默认）
  [bit19: 符号][bit18~0: 幅值]     量程 ±(524287 × Step)，零点精确

EnableSymbol = false（无符号幅值）
  [bit19~0: 幅值]                  量程 [0, 1048575 × Step)
```

自定义配方只需实现接口的两个属性：

```csharp
public struct MyRecipe : IAlchemicalRecipe
{
    public readonly bool EnableSymbol => true;
    public readonly float Step => 0.01f;   // 量程 ±5242.87，步长 0.01
}
```

### 快速示例

```csharp
using SpacePacker;

// 打包：3 个 float -> 1 个 ulong
ulong packed = AlchemicalConverter.PackPosition<DefaultPositionRecipe>(12.5f, -3.25f, 100f);

// 解包：1 个 ulong -> 3 个 float
AlchemicalConverter.UnpackPosition<DefaultPositionRecipe>(packed, out float x, out float y, out float z);
```

### 适用场景

特别适用于需要海量实体同步、持久化存储，或对内存带宽与缓存命中率要求极高的项目（如大集群 RTS、沙盒游戏、高频物理网络同步，以及任何需要紧凑存储三维坐标的场景）。
