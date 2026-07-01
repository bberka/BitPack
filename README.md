# BitPack: Bit-Level Serialization Class Library

[![NuGet Version](https://img.shields.io/nuget/v/BitPack.svg?style=flat-square)](https://www.nuget.org/packages/BitPack/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/bberka/BitPack/publish.yml?branch=master&style=flat-square)](https://github.com/bberka/BitPack/actions)
[![License](https://img.shields.io/github/license/bberka/BitPack.svg?style=flat-square)](LICENSE)

BitPack is a high-performance, native AOT-compatible, zero-allocation C# bit-level serialization class library. It is designed specifically for multiplayer games and high-throughput network architectures where bandwidth footprint and execution overhead must be kept to an absolute minimum.

By generating serialization logic at compile time using Roslyn Incremental Source Generators, BitPack reads and writes variables directly to a bitstream, allowing properties to occupy fractional byte lengths on the wire (e.g., storing a rotation angle in exactly 9 bits instead of a 32-bit integer).

---

## Installation

Install the `BitPack` class library package via the NuGet Package Manager or .NET CLI:

### .NET CLI
```bash
dotnet add package BitPack
```

### Package Manager
```powershell
Install-Package BitPack
```

### PackageReference
```xml
<PackageReference Include="BitPack" Version="0.1.0" />
```

---

## Benchmark Results

The following benchmarks compare serialization and deserialization of two packet structures under **High Process Priority** using **.NET 10.0**:

1.  **Simple Packet (Primitive-only)**: Contains 12 properties (integer, float, double, byte, sbyte, short, ushort, ticks, and boolean states) with no string fields.
2.  **Complex Packet**: Contains 12 properties including strings, DateTimes, enums, and nested structs.

---

### 1. Simple Packet Benchmarks (12 Properties, No Strings)

#### Wire Payload Sizes
| Serializer | Wire Size (Bytes) | Bandwidth Saved vs MessagePack | Bandwidth Saved vs JSON |
| :--- | :---: | :---: | :---: |
| **BitPack** | **17 bytes** | **55.3%** | **89.7%** |
| MemoryPack | 48 bytes | -26.3% | 70.9% |
| MessagePack | 38 bytes | Reference | 77.0% |
| protobuf-net | 50 bytes | -31.6% | 69.7% |
| System.Text.Json | 165 bytes | -334.2% | Reference |

#### Execution Speed & Allocations
*BenchmarkDotNet v0.13.12, .NET 10.0 (X64 RyuJIT AVX2)*

| Method | Mean Speed | Median Speed | Managed Allocated Memory |
| :--- | :---: | :---: | :---: |
| **Serialize_Simple_BitPack** | **21.41 ns** | **21.21 ns** | **0 B** |
| **Deserialize_Simple_BitPack** | **10.47 ns** | **10.22 ns** | **0 B** |
| Serialize_MemoryPack | 17.98 ns | 17.70 ns | 312 B |
| Deserialize_MemoryPack\* | 0.22 ns | 0.22 ns | 0 B |
| Serialize_MessagePack | 61.48 ns | 61.39 ns | 312 B |
| Deserialize_MessagePack | 63.45 ns | 63.48 ns | 0 B |
| Serialize_ProtoBuf | 162.50 ns | 161.96 ns | 64 B |
| Deserialize_ProtoBuf | 160.40 ns | 159.94 ns | 88 B |
| Serialize_SystemTextJson_Context | 268.62 ns | 268.11 ns | 512 B |
| Deserialize_SystemTextJson_Context | 436.31 ns | 435.55 ns | 64 B |

*\*MemoryPack's `Deserialize_Simple` result of 0.22 ns is an artifact of JIT constant folding — the pre-serialized buffer holds a known value (789) and .NET 10's JIT eliminates the entire deserialization, leaving only empty method call overhead. This is not a meaningful performance measurement.*

---

### 2. Complex Packet Benchmarks (12 Properties, Strings, DateTime, Enum, Nested Structs)

#### Wire Payload Sizes
| Serializer | Wire Size (Bytes) | Bandwidth Saved vs MessagePack | Bandwidth Saved vs JSON |
| :--- | :---: | :---: | :---: |
| **BitPack** | **68 bytes** | **40.9%** | **83.2%** |
| MemoryPack | 139 bytes | -20.9% | 65.7% |
| MessagePack | 115 bytes | Reference | 71.6% |
| protobuf-net | 138 bytes | -20.0% | 65.9% |
| System.Text.Json | 405 bytes | -252.2% | Reference |

#### Execution Speed & Allocations
*BenchmarkDotNet v0.13.12, .NET 10.0 (X64 RyuJIT AVX2)*

| Method | Mean Speed | Median Speed | Managed Allocated Memory |
| :--- | :---: | :---: | :---: |
| **Serialize_Complex_BitPack** | **98.92 ns** | **98.62 ns** | **0 B** |
| **Deserialize_Complex_BitPack** | **78.87 ns** | **78.92 ns** | **104 B** |
| Serialize_MemoryPack | 51.53 ns | 51.60 ns | 312 B |
| Deserialize_MemoryPack | 40.85 ns | 40.89 ns | 104 B |
| Serialize_MessagePack | 138.57 ns | 138.43 ns | 312 B |
| Deserialize_MessagePack | 189.13 ns | 189.07 ns | 104 B |
| Serialize_ProtoBuf | 340.04 ns | 340.12 ns | 64 B |
| Deserialize_Complex_ProtoBuf | 413.67 ns | 414.00 ns | 192 B |
| Serialize_SystemTextJson_Context | 1027.55 ns | 1033.71 ns | 5016 B |
| Deserialize_SystemTextJson_Context | 1238.11 ns | 1237.25 ns | 792 B |

*Note on allocations:* The 104 bytes allocated during `Deserialize_Complex_BitPack` represent the two deserialized `string` objects themselves (`"Alex"` and `"Multiplayer Engine Pilot"`). BitPack's deserializer internals perform 0 garbage/helper allocations.

---

## Supported Types Reference

BitPack supports a dedicated subset of types designed for predictable sizing and serialization:

| Type Category | Supported Types | Wire Serialization Mechanics |
| :--- | :--- | :--- |
| **Primitives** | `bool` | 1 bit (true = 1, false = 0) |
| | `char` | 16 bits (standard UTF-16 character value) |
| | `sbyte`, `byte` | 8 bits (default, range-compressible) |
| | `short`, `ushort` | 16 bits (default, range-compressible) |
| | `int`, `uint` | 32 bits (default, range-compressible) |
| | `long`, `ulong` | 64 bits (default, range-compressible) |
| **Floating-Point** | `float`, `double` | 32/64 bits (default IEEE 754) or quantized fixed-point integer bits |
| **High-Precision** | `decimal` | 128 bits (lossless 4-segment 32-bit integer backing) |
| **Native Integers**| `nint`, `nuint` | 64 bits (default, range-compressible) |
| **Text** | `string` | UTF-8 byte stream prepended by a bit-length header |
| **Enums** | Any C# `enum` | Compact bits based on range or the enum's maximum declared value |
| **Date & Time** | `DateTime` | 64 bits (UTC tick representation) |
| **Value Objects** | User-defined `struct` | Supported via custom static `Read(BitReader)` and `Serialize(BitWriter)` hook detection |
| **Nested Objects** | Types marked `[BitPacket]` | Recursively serialized inline |
| **Interfaces** | Interfaces implementing `IBitSerializable` | Deserialized directly into pre-allocated concrete objects |

---

## Attribute Reference

Use the following attributes to configure bit constraints, quantization, versioning, and member selection:

### 1. BitPack Attributes

*   `[BitPacket(Version = 1)]`
    *   **Targets**: Classes, structs, or records.
    *   **Description**: Marks a type as a serialization target. The generator creates `Serialize(BitWriter)`, `Deserialize(BitReader)`, and static `Read(BitReader)` methods. If `Version > 1`, a 4-bit protocol version header is prepended to the bitstream.

*   `[Precision(int decimals)]`
    *   **Targets**: Float and double properties/fields.
    *   **Description**: Combines with a `[Range]` attribute to quantize floating-point values into fixed-point representations. For example, `[Precision(2)]` multiplies the value by 100 before writing it as an integer, reserving only the bits needed for that integer range.

*   `[SinceVersion(int version)]`
    *   **Targets**: Properties or fields in a type marked `[BitPacket]`.
    *   **Description**: Restricts serialization of the member to streams matching or exceeding the specified version index, ensuring backward compatibility.

### 2. Standard C# Data Annotation Attributes

*   `[Range(double minimum, double maximum)]`
    *   **Targets**: Integer, float, and double properties/fields.
    *   **Description**: Defines the numerical boundaries on the wire. BitPack calculates the absolute minimum bit-width required to store the range size (`maximum - minimum`).

*   `[MaxLength(int length)]` or `[StringLength(int length)]`
    *   **Targets**: String properties/fields.
    *   **Description**: Dictates the maximum expected character length. BitPack uses this value to allocate the minimum size header required to encode the string length on the wire.

### 3. Member Control Attributes

*   `[JsonIgnore]` or `[IgnoreDataMember]`
    *   **Targets**: Public properties/fields.
    *   **Description**: Excludes the decorated public member from serialization.

*   `[DataMember]`
    *   **Targets**: Non-public (private/internal) properties or fields.
    *   **Description**: Promotes the decorated non-public member to a serialization target.

---

## Member Selection Code Examples

The code example below shows how to control which properties and fields are serialized using ignore and inclusion attributes:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using BitPack;

[BitPacket]
public partial class PlayerState
{
    // Target: Public read-write property.
    // Serialized by default. Constrained to 0-1000 range (uses 10 bits).
    [Range(0, 1000)]
    public int PlayerId { get; set; }

    // Target: Public property.
    // Excluded from serialization using standard JsonIgnore.
    [JsonIgnore]
    public string TemporarySessionToken { get; set; }

    // Target: Public property.
    // Excluded from serialization using IgnoreDataMember.
    [IgnoreDataMember]
    public int UIReferenceCount { get; set; }

    // Target: Private field.
    // Promoted to serialization target using DataMember.
    [DataMember]
    [Range(0, 100)]
    private int _health = 100;

    // Target: Internal property.
    // Promoted to serialization target using DataMember.
    [DataMember]
    [MaxLength(16)]
    internal string ClanTag { get; set; }

    public int GetHealth() => _health;
    public void SetHealth(int value) => _health = value;
}
```

---

## Range-Based Bit Optimization Examples

### 1. Fixed-Point Quantization
```csharp
[BitPacket]
public partial struct NavigationSnapshot
{
    // Quantized coordinates:
    // Min: -100.0, Max: 100.0. Range size = 200.0.
    // Precision: 2 decimal places. Scale = 10^2 = 100.
    // Integer Range: 0 to (200.0 * 100) = 20000.
    // Bits required: ceil(log2(20001)) = 15 bits on the wire.
    [Range(-100.0, 100.0)]
    [Precision(2)]
    public float Latitude { get; set; }

    [Range(-180.0, 180.0)]
    [Precision(2)]
    public float Longitude { get; set; }
}
```

### 2. Custom Read-Only Structs
Custom read-only value object structs are supported if they expose a custom `Serialize(BitWriter)` method and a static `Read(BitReader)` factory:

```csharp
public readonly struct NetworkId
{
    public uint Value { get; }

    public NetworkId(uint value) => Value = value;

    // Serialization hook called automatically by the generator
    public void Serialize(BitWriter writer) => writer.WriteULong(Value, 24); // Packs in 24 bits

    // Deserialization hook called automatically by the generator
    public static NetworkId Read(BitReader reader) => new NetworkId((uint)reader.ReadULong(24));
}

[BitPacket]
public partial record NetworkHeader
{
    public NetworkId SourceId { get; set; }
}
```

---

## Serialization Wire Format

BitPack writes the entire bitstream as a dense, unbroken sequence of bits — there are **no delimiters, field names, separators, or type metadata** between properties. The wire is self-describing only to code that was compiled with the exact same `[BitPacket]` layout.

### Property Order

Properties (and `[DataMember]`-promoted fields) are serialized **in declaration order** as they appear in the source file:

```csharp
[BitPacket]
public partial record PlayerInput
{
    [Range(0, 360)] public int AimAngle { get; set; }   // Offset 0 bits
    public bool IsMoving { get; set; }                    // Offset 9 bits
    [MaxLength(16)] public string Name { get; set; }     // Offset 10 bits
}
```

The wire layout for a single packet is:

```
[bits 0–8: AimAngle (9 bits)] [bit 9: IsMoving (1 bit)] [bits 10+: Name (6-bit length header + UTF-8 bytes)]
```

If the packet has `[BitPacket(Version = N)]` with `N > 1`, a 4-bit version header is prepended at offset 0 before all properties.

The deserializer reads properties in the same sequential order, consuming exactly the same number of bits per property. Any mismatch in order, type, or bit-width between writer and reader produces **silent data corruption** — not an error message.

### How Bit-Widths Are Determined

| Property type | Wire bits (constrained) | Wire bits (unconstrained) |
| :--- | :--- | :--- |
| `bool` | 1 bit (always) | — |
| `[Range(min, max)] int` | `⌈log₂(max − min + 1)⌉` | 32 bits |
| `[Range(min, max)][Precision(d)] float` | `⌈log₂((max − min) × 10ᵈ + 1)⌉` | 32 bits (IEEE 754) |
| `[MaxLength(n)] string` | `⌈log₂(n × 4)⌉` length header + UTF-8 bytes | `⌈log₂(256 × 4)⌉` = 10-bit header |
| `decimal` | 128 bits (always) | — |
| `enum` | `⌈log₂(max value + 1)⌉` or `[Range]` override | — |
| `[BitPacket]` nested type | Sum of its own property bits | — |

---

## Backward Compatibility

### Protocol Version Gating

Use `[SinceVersion]` to add new fields to live protocols without breaking backwards compatibility:

```csharp
[BitPacket(Version = 2)] // Prepends a 4-bit header indicating V2
public partial record PlayerInput
{
    [Range(0, 360)]
    public int AimAngle { get; set; } // Version 1 (default)

    [SinceVersion(2)]
    public bool ExtraBoost { get; set; } // Only serialized if stream version >= 2
}
```

### Stream Gating Rules

1.  **V2 Client to V2 Server**: Version 2 header is sent. Both `AimAngle` and `ExtraBoost` are read and written.
2.  **V1 Client to V2 Server**: Version 1 header is sent. The server reads `AimAngle`, sees `packetVersion (1) < SinceVersion (2)`, skips reading `ExtraBoost`, and defaults it to `false`. The bitstream remains aligned.

### Compatibility Pitfalls

BitPack's dense bitstream layout means that **any** change to the property sequence, count, types, or constraint attributes can silently break compatibility. Since there are no field identifiers or type tags on the wire, mismatches produce garbled data rather than clean errors.

#### Changing Property Order **BREAKS** Compatibility

Properties are serialized in declaration order. Reordering them in source code changes the wire layout:

```csharp
// Version A — compatible stream
[BitPacket]
public partial record PlayerInput
{
    public bool IsMoving { get; set; }    // bit 0
    public int AimAngle { get; set; }      // bits 1–9
}

// Version B — INCOMPATIBLE (reordered)
[BitPacket]
public partial record PlayerInput
{
    public int AimAngle { get; set; }      // NOW bit 0 — misaligned!
    public bool IsMoving { get; set; }    // NOW bit 9 — reads garbage
}
```

A V2 client sending in the new order to a V1 server will interleave `AimAngle` bits where `IsMoving` was expected, corrupting every subsequent field. **Always append new properties to the end of the class.**

#### Adding New Properties

| Action | Result |
| :--- | :--- |
| Add at end + `[SinceVersion]` | New clients write the field; old clients skip it on read. Safe. |
| Add at end without `[SinceVersion]` | Old clients won't know the field exists — deserialization reads incorrect bits from the new field's slot. Unsafe. |
| Add in the middle | Same as reordering — breaks alignment for all subsequent fields. Unsafe. |

#### Removing Properties

Removing a property creates a "hole" in the bitstream. Old clients still encode bits for the removed field; the new server or client will read those bits as the next property, shifting all subsequent reads:

```csharp
// Version A
[BitPacket(Version = 2)]
public partial record PlayerInput
{
    [SinceVersion(2)]
    public bool SprintEnabled { get; set; } // bits 0–0
    public int AmmoCount { get; set; }       // bits 1–7
}
// Stream: [SprintEnabled:1b][AmmoCount:7b]

// Version B — REMOVED SprintEnabled
[BitPacket(Version = 2)]
public partial record PlayerInput
{
    public int AmmoCount { get; set; }       // NOW bits 0–6 — misaligned!
}
// But old client writes: [SprintEnabled:1b][AmmoCount:7b]
// New server reads AmmoCount from bits 0–6, losing SprintEnabled bit + 1 AmmoCount bit
```

**Instead of removing a field, gate it behind `[SinceVersion]` on a bumped packet version and leave its declaration in place.** This ensures old clients produce a stream that the new server can still parse correctly.

#### Renaming Properties — SAFE

Renaming a property has no effect on the wire format. BitPack serializes by **declaration order**, not by member name.

#### Changing Type or Constraints **BREAKS** Compatibility

| Change | Effect |
| :--- | :--- |
| `[Range(0, 100)]` → `[Range(0, 1000)]` | Bit-width changes (7 → 10 bits). Wire layout shifts. |
| `[MaxLength(16)]` → `[MaxLength(32)]` | Length header bit-width changes. All subsequent fields shift. |
| `int` → `long` | 32 → 64 bits (or constrained bit-width changes). Shift. |
| `float` → `[Range][Precision] float` | 32 bits → range bits. Shift. |

#### Compatibility Decision Table

| Change | Safe? | Mitigation |
| :--- | :--- | :--- |
| Append new property at end | If `[SinceVersion]` and version bumped | Gate behind version |
| Append new property at end (no version) | No | Bump packet version first |
| Reorder properties | **No** | Never reorder |
| Delete a property | **No** | Gate behind `[SinceVersion]` instead |
| Rename a property | **Yes** | No action needed |
| Change `[Range]` on existing property | **No** | Fork to a new packet type, or bump version and add a new constrained property at end while deprecating the old one |
| Change `[MaxLength]` on existing property | **No** | Same as range change |
| Change property type (`int` → `long`) | **No** | Add new property at end; keep old as unused |

#### Detection at Runtime

BitPack cannot detect wire format mismatches at runtime (there is no schema negotiation). The recommended approach is:

1. **Protocol negotiation**: Exchange a version handshake at connection time (independent of BitPack). The server rejects clients with incompatible packet versions.
2. **Packet type IDs**: Prefix each packet with a small integer identifying its schema version, checked before deserialization.
3. **Integration tests**: Serialize known payloads to golden byte arrays in CI. Any change to the wire format breaks the golden test, alerting the developer.

```csharp
// Golden-file test pattern
[Fact]
public void PlayerInput_WireFormat_IsStable()
{
    var packet = new PlayerInput { AimAngle = 90, IsMoving = true };
    var writer = new BitWriter(new byte[16]);
    packet.Serialize(writer);

    var golden = Convert.ToHexString(writer.ToArray());
    Assert.Equal("5A02", golden); // Fails if wire format ever changes
}
```

---

## Diagnostic Rules

BitPack enforces strict compile-time checks to prevent unoptimized layouts or invalid configurations:

*   **Error BP0001**: Raised when a property type is not packable (lacks BitPacket attribute, does not implement custom serialization hooks, or is dynamic/object), or when a property's `SinceVersion` exceeds its parent's version. Also raised when a property target lacks a getter accessor, or lacks a setter/init accessor while not matching any constructor parameters (preventing successful serialization/deserialization).
*   **Warning BP0002**: Raised when a primitive integer or string does not specify optimization attributes (such as Range or MaxLength), indicating that full type storage (unquantized) will be used as a fallback.

## Annotations and Runtime Safety

### Why Annotations Matter

BitPack's bit-width optimization is driven entirely by compile-time attributes. Without them, every property falls back to its full C# type width (32 bits for `int`, 64 bits for `long`, etc.):

| Attribute | Effect on wire size |
| :--- | :--- |
| `[Range(0, 1000)]` | Stores the value offset in exactly ⌈log₂(1001)⌉ = 10 bits instead of 32 |
| `[MaxLength(16)]` | Allocates only ⌈log₂(16×4)⌉ = 6 bits for the string length header |
| `[Precision(2)]` (with `[Range]`) | Quantizes a `float`/`double` into a fixed-point integer needing only range bits |

**Enums are detected automatically.** BitPack calculates the minimum bit-width from the largest declared enum field value. To override this (e.g., to reserve headroom for future values), apply `[Range]` directly on the enum property:

```csharp
[BitPacket]
public partial record GamePacket
{
    // Auto-detected: max field is 2 → 2 bits on the wire
    public GameState State { get; set; }

    // Override: reserve up to 15 → 4 bits on the wire
    [Range(0, 15)]
    public GameState FutureProofState { get; set; }
}

public enum GameState { Idle = 0, Running = 1, Paused = 2 }
```

### Runtime Mismatch and Overflow Behaviour

BitPack trusts the annotations at face value. Assigning values that exceed declared constraints throws an `ArgumentOutOfRangeException` at `Serialize()` time for **all** constrained types — strings, integers, floats, and enums:

**Strings — exceeds `[MaxLength]` or `[StringLength]`:**
```csharp
[BitPacket]
public partial record PlayerPacket
{
    [MaxLength(16)]
    public string Name { get; set; }
}

// Throws ArgumentOutOfRangeException at Serialize():
new PlayerPacket { Name = "This string is way longer than sixteen characters" };
```

**Integers — outside `[Range]`:**
```csharp
[BitPacket]
public partial record PlayerPacket
{
    [Range(0, 100)]
    public int Health { get; set; }
}

// Throws ArgumentOutOfRangeException at Serialize():
var packet = new PlayerPacket { Health = 999 };
packet.Serialize(writer);
```

**Enums — auto-detected or `[Range]`-declared:**
```csharp
[BitPacket]
public partial record GamePacket
{
    // Auto-detected max: Paused=2 → throws for values > 2
    public GameState State { get; set; }

    // Explicit [Range]: throws for values outside [0, 15]
    [Range(0, 15)]
    public GameState FutureProofState { get; set; }
}

// Throws ArgumentOutOfRangeException at Serialize():
new GamePacket { State = (GameState)99 };
```

**Floats/doubles with `[Precision]`** — values outside the `[Range]` throw identically.

BitPack cannot validate runtime values at compile time. Guard your setters or validate inputs before constructing packets to stay within declared bounds.

---

## Architectural Guidance

### 1. Object Instantiation and Constructors
BitPack supports C# object initialization patterns:
*   **Parameterless Constructors**: If a class or record exposes a parameterless constructor, the static `Read` factory instantiates the type and sets properties using object initializers.
*   **Init-Only and Required Properties**: Properties marked `init` or `required` are initialized during object construction in the static `Read(BitReader)` factory.
*   **Parameterized and Primary Constructors**: If a type defines a parameterized constructor (such as C# records with primary constructors), the generator matches the constructor parameter names to the serialization targets (case-insensitive). It parses the values from the bitstream first, instantiates the type using the matched constructor, and sets any remaining targets via object initializers.

### 2. Character and String Encoding
All string properties are serialized using UTF-8 encoding. This supports international characters and Unicode symbols. Span-based `stackalloc byte` blocks are used internally to prevent heap allocations during deserialization.

### 3. Encryption and Compression Pipelines
BitPack is engineered solely for structural packet size optimization (fitting fields into precise bit configurations). 
*   **Compression**: Generic compression algorithms (like Brotli, Deflate, or Gzip) operate on byte patterns. Trying to compress individual bits during serialization is counterproductive.
*   **Encryption**: Symmetric encryption (like AES-GCM or ChaCha20) must be applied to the completed byte array.
*   **Pipeline Recommendation**:
    ```text
    [C# Objects] ──> BitPack (Serialize) ──> [Raw Bytes] ──> Encrypt/Compress ──> [UDP/TCP Socket]
    ```
    If encryption or compression are required, they should be applied to the resulting byte array (e.g. from `BitWriter.ToArray()` or `BitWriter.Buffer`) rather than attempting to perform them within the serializer itself.

### 4. Performance Design

BitPack's performance is achieved through several deliberate design choices:

*   **Tiered bit-width writes**: The `WriteLong`/`ReadLong` core methods branch on the total bit span (`bitOffset + bitCount`), dispatching to byte (≤8 bits), `ushort` (≤16), `uint` (≤32), or `ulong` (≤64) read-modify-write paths. This avoids full 64-bit memory traffic for narrow values like booleans or range-constrained integers.
*   **Zero-heap allocations**: All internal buffers for string encoding use `stackalloc` for small payloads and `ArrayPool<byte>.Shared` for large ones. The `ReadDecimal` method uses the `decimal(int,int,int,bool,byte)` constructor to avoid allocating `new int[4]`.
*   **Span-based APIs throughout**: `WriteBytes` and `ReadBytes` operate on `ReadOnlySpan<byte>` / `Span<byte>`, enabling the JIT to eliminate bounds checks and enabling vectorized copy for byte-aligned writes.
*   **Compile-time code generation**: The Roslyn incremental source generator emits specialized serialization logic per type, avoiding any runtime reflection or dynamic code generation.

---

## Contributing

Contributions are welcome. Please open an issue first to discuss what you'd like to change.

### Prerequisites

*   [.NET SDK 10.0](https://dotnet.microsoft.com/download) (or later)

### Build

```bash
dotnet build BitPack.slnx
```

### Run Tests

```bash
dotnet test BitPack.Tests/BitPack.Tests.csproj
```

### Run Benchmarks

```bash
dotnet run -c Release -f net10.0 --project BitPack.Benchmarks/BitPack.Benchmarks.csproj
```

Benchmarks compare BitPack against MemoryPack, MessagePack, protobuf-net, and System.Text.Json using BenchmarkDotNet. Results are written to `BenchmarkDotNet.Artifacts/results/`.

### Project Structure

| Directory | Description |
| :--- | :--- |
| `BitPack/` | Core library — `BitWriter`, `BitReader`, attributes, and the `IBitSerializable` interface |
| `BitPack.Generator/` | Roslyn incremental source generator — emits `Serialize`, `Deserialize`, and `Read` methods at compile time |
| `BitPack.Tests/` | xUnit test suite covering all serialization scenarios |
| `BitPack.Benchmarks/` | BenchmarkDotNet harness comparing BitPack against other serializers |

### Code Style

*   All public API methods are annotated `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
*   Internal hot-path methods use `Unsafe` intrinsics for unaligned memory access.
*   The library targets `netstandard2.1` for broad compatibility while the generator targets `netstandard2.0`.
