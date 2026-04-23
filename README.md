# SpacePacker

[English](README.md) | [简体中文](README_zh.md)

---

SpacePacker is a highly efficient **Tri-axial Branchless Spatial Quantization (TBSQ)** designed specifically for Unity ECS (DOTS). It uses bitwise operations to compress 12-byte `float3` position data into an 8-byte `ulong` with high precision, significantly saving memory bandwidth and improving cache hit rates.

*Tips: Currently, only the core logical concepts are implemented; there might be syntax or dependency errors.*

### Core Features

*   **Extreme Compression**: Allocates 20 bits for each floating-point coordinate of the X, Y, and Z dimensions, packing them into a single `ulong` (60 bits of effective data in total).
*   **Perfect Burst Compatibility**: The core converter `AlchemicalConverter` is designed based on a generic recipe (`IAlchemicalRecipe`) pattern, supporting constant inlining by the Unity Burst compiler at compile time for zero-overhead conversion.
*   **High-Precision Customization**: Developers can customize coordinate ranges and scaling ratios by implementing the interface. For example, the default recipe maintains a high precision of about `0.001` units within the `[-500, 500]` range.
*   **Highly Optimized Memory Layout**: `EcsAlchemicalData`, designed specifically for ECS, uses an explicit memory layout (`LayoutKind.Explicit`), achieving overlapping reuse of compressed coordinates and state signatures (`StateSignature`) within a tight space.

### Use Cases

Particularly suitable for Unity DOTS game projects that require massive entity synchronization, persistent storage, or have extremely high demands on memory bandwidth and cache hit rates (e.g., large-cluster RTS, sandbox games, or high-frequency physics network synchronization).