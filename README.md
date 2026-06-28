# BitPack: Bit-Level Serialization Class Library

BitPack is a high-performance, native AOT-compatible, zero-allocation C# bit-level serialization class library. It is designed specifically for multiplayer games and high-throughput network architectures where bandwidth footprint and execution overhead must be kept to an absolute minimum.

By generating serialization logic at compile time using Roslyn Incremental Source Generators, BitPack reads and writes variables directly to a bitstream, allowing properties to occupy fractional byte lengths on the wire (e.g., storing a rotation angle in exactly 9 bits instead of a 32-bit integer).

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

## Protocol Version Gating

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

---

## Diagnostic Rules

BitPack enforces strict compile-time checks to prevent unoptimized layouts or invalid configurations:

*   **Error BP0001**: Raised when a property type is not packable (lacks BitPacket attribute, does not implement custom serialization hooks, or is dynamic/object), or when a property's `SinceVersion` exceeds its parent's version. Also raised when a property target lacks a getter accessor, or lacks a setter/init accessor while not matching any constructor parameters (preventing successful serialization/deserialization).
*   **Warning BP0002**: Raised when a primitive integer or string does not specify optimization attributes (such as Range or MaxLength), indicating that full type storage (unquantized) will be used as a fallback.

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
