# SpacePacker


[简体中文](README.md) | [English](README_en.md)
---

SpacePacker is a highly efficient, zero-dependency, platform-agnostic **Tri-axial Branchless Spatial Quantization (TBSQ)** library for .NET. It uses bitwise operations to compress 12-byte 3D floating-point position data into an 8-byte `ulong` with high precision, significantly saving memory bandwidth and improving cache hit rates.

*Tips: Currently, only the core logical concepts are implemented.*

### Core Features

*   **Extreme Compression**: Allocates 20 bits for each floating-point coordinate of the X, Y, and Z dimensions, packing them into a single `ulong` (60 bits of effective data in total).
*   **Zero-Dependency Pure .NET**: Relies only on the BCL. Works in any .NET environment (.NET / .NET Core / .NET Framework / Mono), and can also be integrated directly as source files.
*   **Compiler Constant Inlining**: The core converter `AlchemicalConverter` is designed around a generic recipe (`IAlchemicalRecipe`) pattern, enabling JIT/AOT compilers to inline recipe constants at compile time for zero-overhead conversion.
*   **Dual-Mode Encoding**: Each 20-bit axis supports two modes — `(1+19)` sign-magnitude mode (bit 19 = sign, zero exactly representable) and 20-bit unsigned magnitude mode, switched via the recipe's `EnableSymbol`.
*   **Intuitive Precision Customization**: Recipes declare a decimal step `Step` (e.g. `0.001f`) as the primary precision parameter; the converter derives the multiplication scale from it exactly once, so each conversion performs zero divisions. The default recipe covers `±524.287` with an exact `0.001` step.
*   **Compact Memory Layout**: `AlchemicalData` is an 8-byte, explicitly laid out (`LayoutKind.Explicit`) value type that wraps the 60-bit compressed coordinates as a strong type, ready to be embedded in any data structure with zero overhead.

### Encoding Model

The bit budget per axis follows a conservation law: `2^bits = range × (1 / Step)` — range and precision trade off against each other (≈ 10⁶ dynamic range).

```
EnableSymbol = true (sign-magnitude, default)
  [bit19: sign][bit18~0: magnitude]     range ±(524287 × Step), exact zero

EnableSymbol = false (unsigned magnitude)
  [bit19~0: magnitude]                  range [0, 1048575 × Step)
```

A custom recipe only needs to implement two properties:

```csharp
public struct MyRecipe : IAlchemicalRecipe
{
    public readonly bool EnableSymbol => true;
    public readonly float Step => 0.01f;   // range ±5242.87, step 0.01
}
```

### Quick Example

```csharp
using SpacePacker;

// Pack: 3 floats -> 1 ulong
ulong packed = AlchemicalConverter.PackPosition<DefaultPositionRecipe>(12.5f, -3.25f, 100f);

// Unpack: 1 ulong -> 3 floats
AlchemicalConverter.UnpackPosition<DefaultPositionRecipe>(packed, out float x, out float y, out float z);
```

### Use Cases

Particularly suitable for projects that require massive entity synchronization, persistent storage, or have extremely high demands on memory bandwidth and cache hit rates (e.g., large-cluster RTS, sandbox games, high-frequency physics network synchronization, or any scenario requiring compact 3D coordinate storage).
