# SpacePacker

[English](README.md) | [简体中文](README_zh.md)

---

SpacePacker 是一个专为 Unity ECS (DOTS) 设计的高效**三轴无分支空间量化压缩 (Tri-axial Branchless Spatial Quantization, TBSQ)**。它通过位运算将 12 字节的 `float3` 位置数据高精度压缩为 8 字节的 `ulong`，以极大地节省内存带宽并提升缓存命中率。

*Tips：当前只实现了核心的逻辑思想，可能存在语法或者依赖错误。*

### 核心特性

*   **极致压缩**: 将 X、Y、Z 三个维度的浮点坐标各分配 20 位，打包进单个 `ulong` 中（共 60 位有效数据）。
*   **Burst 完美兼容**: 核心转换器 `AlchemicalConverter` 基于泛型配方 (`IAlchemicalRecipe`) 设计，支持 Unity Burst 编译器在编译期进行常量内联，实现零开销转换。
*   **高精度自定义**: 开发者可通过实现接口自定义坐标范围与缩放比例。例如默认配方在 `[-500, 500]` 范围内可保持约 `0.001` 单位的高精度。
*   **高度优化的内存布局**: 专为 ECS 设计的 `EcsAlchemicalData` 采用显式内存布局 (`LayoutKind.Explicit`)，在较小的空间内实现了压缩坐标与状态签名 (`StateSignature`) 的重叠复用。

### 适用场景

特别适用于需要海量实体同步、持久化存储，或对内存带宽与缓存命中率要求极高的 Unity DOTS 游戏项目（如大集群 RTS、沙盒游戏或高频物理网络同步）。