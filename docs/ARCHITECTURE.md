# Architecture and Reuse Guide

BitPack is a research project. This page explains how it is put together so you can lift the parts you need into your own game or engine.

## Layers

| Layer | Files | Role |
| :--- | :--- | :--- |
| Bit I/O (runtime) | `BitPack/BitSpanWriter.cs`, `BitSpanReader.cs` | `ref struct` readers/writers over a `Span<byte>`. Fastest path; no allocation. The whole bit-packing core is these two files. |
| Class wrappers | `BitPack/BitWriter.cs`, `BitReader.cs` | Heap-friendly wrappers over `byte[]`/`Memory<byte>` that forward to the span types. This is what generated `Serialize(BitWriter)` uses. |
| Attributes | `BitPack/Attributes.cs` | The declarative surface (`[BitPacket]`, `[Range]` via DataAnnotations, `[Precision]`, `[SinceVersion]`, ...). |
| Source generator | `BitPack.Generator/PacketGenerator*.cs` | Reads attributes and emits `Serialize`, `Read`, delta and union code at compile time. |

## Generator layout

`PacketGenerator` is one `partial` class split by responsibility:

| File | Responsibility |
| :--- | :--- |
| `PacketGenerator.cs` | Diagnostics descriptors, incremental pipeline, `Execute` (assembles the generated file). |
| `PacketGenerator.Targets.cs` | Which members are serialized, accessibility, validation, constructor matching. |
| `PacketGenerator.Emit.cs` | Code emitters per field kind (primitives, math codecs, arrays). |
| `PacketGenerator.Config.cs` | Parsing attribute arguments into small config structs. |
| `PacketGenerator.Unions.cs` | Discriminated unions. |
| `PacketGenerator.Layout.cs` | Bit-width math, `MaxBits`, layout manifest and `LayoutHash`, versioning. |
| `PacketGenerator.Model.cs` | Internal data types shared by the files above. |

To add a new field kind, add a config struct in `Config`, an emitter in `Emit`, a bit-width rule in `Layout`, and a test.

## Taking just what you need

- **Only the bit I/O:** copy `BitSpanWriter.cs` and `BitSpanReader.cs` (plus `BitWriter.CalculateBitsNeeded`, which they call). Write your own serialization by hand.
- **Your own wire layout:** use the generator as a reference for how widths, quantization and versioning are derived, and replace the emitters.
- **Different transport:** `BitWriter`/`BitReader` only need a `byte[]` or `Memory<byte>`; nothing assumes sockets.

## Guarding the wire format

`WireFormatTests` pins the exact bytes and `LayoutHash` of a sample packet. Each generated type also exposes `LayoutHash` and `LayoutManifest`; exchange the hash during connection setup to detect mismatched builds before parsing garbage.

## Known limits

- Not production-hardened; validate for your workload.
- `BitWriter`/`BitReader` are not thread-safe.
- Decoded strings and arrays rely on declared maximums; keep `[MaxLength]`/array bounds tight.
