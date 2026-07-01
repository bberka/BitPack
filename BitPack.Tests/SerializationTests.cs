using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using BitPack.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace BitPack.Tests;

public interface IInventoryItem : IBitSerializable
{
    int ItemId { get; set; }
}

[BitPacket]
public partial record WeaponItem : IInventoryItem
{
    [Range(0, 100)] public int Durability { get; set; }

    public int ItemId { get; set; }
}

[BitPacket]
public partial record TestPlayerSnapshot
{
    [Range(-500.0, 500.0)]
    [Precision(2)] // Quantized fixed-point: -500.00 to 500.00 (100,001 values -> 17 bits)
    public float X { get; set; }

    [Range(-500.0, 500.0)] [Precision(2)] public float Y { get; set; }
}

[BitPacket]
public partial record TestPlayerInput
{
    [Range(0, 360)] public int AimAngle { get; set; }

    public bool IsMoving { get; set; }

    // Custom Readonly Struct Primitive
    public Percent HealthPercent { get; set; }

    // Nested Packet Type
    public TestPlayerSnapshot Snapshot { get; set; } = null!;
}

[BitPacket]
public partial record TestPlayerExtendedInput
{
    public decimal AccountBalance { get; set; }

    public nint NativeSessionId { get; set; }

    public nuint NativeThreadMask { get; set; }

    // Interface referencing concrete implementation
    public IInventoryItem Weapon { get; set; } = null!;
}

public enum TestGameState
{
    Idle = 0,
    Running = 1,
    Paused = 2
}

[BitPacket]
public partial record TestEnumPacket
{
    public TestGameState State { get; set; }
}

[BitPacket]
public partial record TestGeneratedApiPacket
{
    [Range(0, 100)] public int Value { get; set; }
    public bool Flag { get; set; }
    [MaxLength(4)] public string Name { get; set; } = "";
    public TestGameState State { get; set; }
}

public abstract class TestBaseGamePacket
{
    [BitFieldKey(0)] [Range(0, 1000)] public int Tick { get; set; }
    [BitFieldKey(1)] public bool IsReliable { get; set; }
}

[BitPacket]
public partial class TestMoveGamePacket : TestBaseGamePacket
{
    [BitFieldKey(2)] [Range(-100, 100)] public int DeltaX { get; set; }
    [BitFieldKey(3)] public bool IsJumping { get; set; }
}

[BitPacket]
public partial class TestKeyedPacket
{
    [BitFieldKey(2)] public bool Last { get; set; }
    [BitFieldKey(0)] [Range(0, 10)] public int First { get; set; }
    [BitFieldKey(1)] public bool Middle { get; set; }
}

[BitPacket]
public partial record TestArrayPacket
{
    [MaxLength(4)] [Range(0, 31)] public int[] EntityIds { get; set; } = Array.Empty<int>();
    [FixedCount(4)] public bool[] Buttons { get; set; } = Array.Empty<bool>();
    [MaxLength(2)] public TestPlayerSnapshot[] Snapshots { get; set; } = Array.Empty<TestPlayerSnapshot>();
}

[BitPacket(2)]
public partial record TestVersionedInput
{
    [Range(0, 100)] public int BaseSpeed { get; set; }

    [SinceVersion(2)] public bool ExtraBoost { get; set; }
}

[BitPacket]
public partial record TestIgnoredAndPrivatePacket
{
    [DataMember] private int privatePromotedField = 42;

    [JsonIgnore] public int IgnoredProperty1 { get; set; } = 999;

    [IgnoreDataMember] public string IgnoredProperty2 { get; set; } = "ignore me";

    public bool PublicTarget { get; set; }

    [DataMember] internal string InternalPromotedProperty { get; set; } = "internal";

    public int GetPrivateField()
    {
        return privatePromotedField;
    }

    public void SetPrivateField(int val)
    {
        privatePromotedField = val;
    }
}

[BitPacket]
public partial record TestUnicodePacket
{
    [MaxLength(16)] public string Message { get; set; } = "";
}

[BitPacket]
public partial record TestInitAndRequiredPacket
{
    public int RegularVal { get; set; }

    public int InitOnlyVal { get; init; }

    public required string RequiredName { get; set; }
}

[BitPacket]
public partial record TestPrimaryCtorPacket(
    [property: Range(0, 100)] int Age,
    [property: MaxLength(16)] string Nickname
);

[BitPacket]
public partial record TestRangeOverflowPacket
{
    [Range(0, 100)] public int SmallValue { get; set; }
    [Range(-180, 180)] public int Latitude { get; set; }
}

[BitPacket]
public partial record TestStringOverflowPacket
{
    [MaxLength(5)] public string Name { get; set; } = "";
    [StringLength(10)] public string Bio { get; set; } = "";
}

[BitPacket]
public partial record TestQuantizedOverflowPacket
{
    [Range(-500.0, 500.0)] [Precision(2)] public float Coord { get; set; }
}

[BitPacket]
public partial record TestEnumOverflowPacket
{
    [Range(0, 5)] public TestGameState SmallRangeState { get; set; }
    public TestGameState AutoDetectState { get; set; }
}

public class SerializationTests
{
    [Fact]
    public void GeneratedPacket_SerializesAndDeserializesCorrectlyWithNestedTypesAndCustomStructs()
    {
        // Arrange
        var buffer = new byte[128];

        var originalPacket = new TestPlayerInput
        {
            AimAngle = 270,
            IsMoving = true,
            HealthPercent = new Percent(750000), // 75%
            Snapshot = new TestPlayerSnapshot
            {
                X = -234.56f,
                Y = 456.78f
            }
        };

        // Act - Serialize
        var writer = new BitWriter(buffer);
        originalPacket.Serialize(writer);

        var bytesUsed = writer.BytesWritten;

        // Act - Deserialize back
        var reader = new BitReader(buffer);
        var restoredPacket = new TestPlayerInput();
        restoredPacket.Deserialize(reader);

        // Assert values match perfectly
        Assert.Equal(originalPacket.AimAngle, restoredPacket.AimAngle);
        Assert.Equal(originalPacket.IsMoving, restoredPacket.IsMoving);
        Assert.Equal(originalPacket.HealthPercent, restoredPacket.HealthPercent);
        Assert.Equal(originalPacket.Snapshot.X, restoredPacket.Snapshot.X, 2); // 2 decimal precision assertion
        Assert.Equal(originalPacket.Snapshot.Y, restoredPacket.Snapshot.Y, 2);
    }

    [Fact]
    public void GeneratedPacket_SerializesAndDeserializesExtendedTypesLosslessly()
    {
        // Arrange
        var buffer = new byte[128];

        var originalPacket = new TestPlayerExtendedInput
        {
            AccountBalance = 123456.7890m,
            NativeSessionId = 987654,
            NativeThreadMask = unchecked((nuint)0xAAAA_BBBB_CCCC_DDDD),
            Weapon = new WeaponItem { ItemId = 42, Durability = 85 }
        };

        // Act - Serialize
        var writer = new BitWriter(buffer);
        originalPacket.Serialize(writer);

        // Act - Deserialize back into pre-allocated model
        var reader = new BitReader(buffer);
        var restoredPacket = new TestPlayerExtendedInput
        {
            Weapon = new WeaponItem() // Pre-allocate interface field
        };
        restoredPacket.Deserialize(reader);

        // Assert
        Assert.Equal(originalPacket.AccountBalance, restoredPacket.AccountBalance);
        Assert.Equal(originalPacket.NativeSessionId, restoredPacket.NativeSessionId);
        Assert.Equal(originalPacket.NativeThreadMask, restoredPacket.NativeThreadMask);
        Assert.Equal(originalPacket.Weapon.ItemId, restoredPacket.Weapon.ItemId);
        Assert.Equal(
            ((WeaponItem)originalPacket.Weapon).Durability,
            ((WeaponItem)restoredPacket.Weapon).Durability
        );
    }

    [Fact]
    public void GeneratedPacket_DeserializesUndefinedEnum_FallsBackToZero()
    {
        // Arrange
        var buffer = new byte[128];
        var writer = new BitWriter(buffer);

        // Write undefined state index 3 (only 0, 1, 2 are defined)
        writer.WriteInt(3, 2);

        // Act
        var reader = new BitReader(buffer);
        var restored = new TestEnumPacket();
        restored.Deserialize(reader);

        // Assert - falls back to default 0 (Idle)
        Assert.Equal(TestGameState.Idle, restored.State);
    }

    [Fact]
    public void GeneratedPacket_SerializesAndDeserializesVersionGatedFields()
    {
        // Arrange
        var buffer = new byte[128];
        var original = new TestVersionedInput
        {
            BaseSpeed = 75,
            ExtraBoost = true
        };

        // Act - Serialize (writes version header 2)
        var writer = new BitWriter(buffer);
        original.Serialize(writer);

        // Act - Deserialize back as V2 client
        var reader = new BitReader(buffer);
        var restored = new TestVersionedInput();
        restored.Deserialize(reader);

        // Assert both properties are preserved
        Assert.Equal(75, restored.BaseSpeed);
        Assert.True(restored.ExtraBoost);
    }

    [Fact]
    public void GeneratedPacket_ExposesMaxSizeAndLayoutMetadata()
    {
        Assert.Equal(143, TestGeneratedApiPacket.MaxBits);
        Assert.Equal(18, TestGeneratedApiPacket.MaxBytes);
        Assert.NotEqual(0u, TestGeneratedApiPacket.LayoutHash);
        Assert.Contains("type=BitPack.Tests.TestGeneratedApiPacket", TestGeneratedApiPacket.LayoutManifest);
        Assert.Contains("field=Value;type=int;declaringType=BitPack.Tests.TestGeneratedApiPacket;key=;since=1;bits=7", TestGeneratedApiPacket.LayoutManifest);
        Assert.Contains("field=Name;type=string;declaringType=BitPack.Tests.TestGeneratedApiPacket;key=;since=1;bits=133", TestGeneratedApiPacket.LayoutManifest);
    }

    [Fact]
    public void GeneratedPacket_UnknownCustomFieldSizeUsesNegativeMaxSize()
    {
        Assert.Equal(-1, TestPlayerInput.MaxBits);
        Assert.Equal(-1, TestPlayerInput.MaxBytes);
        Assert.Contains("field=HealthPercent;type=BitPack.Tests.Percent;declaringType=BitPack.Tests.TestPlayerInput;key=;since=1;bits=-1", TestPlayerInput.LayoutManifest);
    }

    [Fact]
    public void GeneratedPacket_IncludesInheritedBaseProperties()
    {
        var packet = new TestMoveGamePacket
        {
            Tick = 123,
            IsReliable = true,
            DeltaX = -42,
            IsJumping = true
        };
        var buffer = new byte[TestMoveGamePacket.MaxBytes];

        packet.Serialize(new BitWriter(buffer));

        var restored = TestMoveGamePacket.Read(new BitReader(buffer));
        Assert.Equal(packet.Tick, restored.Tick);
        Assert.Equal(packet.IsReliable, restored.IsReliable);
        Assert.Equal(packet.DeltaX, restored.DeltaX);
        Assert.Equal(packet.IsJumping, restored.IsJumping);
        Assert.Contains("field=Tick;type=int;declaringType=BitPack.Tests.TestBaseGamePacket;key=0;since=1;bits=10", TestMoveGamePacket.LayoutManifest);
        Assert.Contains("field=DeltaX;type=int;declaringType=BitPack.Tests.TestMoveGamePacket;key=2;since=1;bits=8", TestMoveGamePacket.LayoutManifest);
    }

    [Fact]
    public void GeneratedPacket_BitFieldKeyControlsWireOrder()
    {
        var manifest = TestKeyedPacket.LayoutManifest;

        var firstIndex = manifest.IndexOf("field=First;", StringComparison.Ordinal);
        var middleIndex = manifest.IndexOf("field=Middle;", StringComparison.Ordinal);
        var lastIndex = manifest.IndexOf("field=Last;", StringComparison.Ordinal);

        Assert.True(firstIndex >= 0);
        Assert.True(middleIndex > firstIndex);
        Assert.True(lastIndex > middleIndex);
        Assert.Equal(6, TestKeyedPacket.MaxBits);
        Assert.Equal(1, TestKeyedPacket.MaxBytes);
    }

    [Fact]
    public void GeneratedPacket_BoundedArraysRoundTripAndReportMaxSize()
    {
        var packet = new TestArrayPacket
        {
            EntityIds = new[] { 1, 7, 31 },
            Buttons = new[] { true, false, true, true },
            Snapshots = new[]
            {
                new TestPlayerSnapshot { X = 1.23f, Y = -4.56f },
                new TestPlayerSnapshot { X = -7.89f, Y = 10.11f }
            }
        };
        var buffer = new byte[TestArrayPacket.MaxBytes];

        packet.Serialize(new BitWriter(buffer));

        var restored = TestArrayPacket.Read(new BitReader(buffer));
        Assert.Equal(packet.EntityIds, restored.EntityIds);
        Assert.Equal(packet.Buttons, restored.Buttons);
        Assert.Equal(packet.Snapshots.Length, restored.Snapshots.Length);
        Assert.Equal(packet.Snapshots[0].X, restored.Snapshots[0].X, 2);
        Assert.Equal(packet.Snapshots[0].Y, restored.Snapshots[0].Y, 2);
        Assert.Equal(packet.Snapshots[1].X, restored.Snapshots[1].X, 2);
        Assert.Equal(packet.Snapshots[1].Y, restored.Snapshots[1].Y, 2);
        Assert.Equal(97, TestArrayPacket.MaxBits);
        Assert.Equal(13, TestArrayPacket.MaxBytes);
        Assert.Contains("field=EntityIds;type=int[]", TestArrayPacket.LayoutManifest);
        Assert.Contains("array=max;count=4;elementType=int;elementBits=5;bits=23", TestArrayPacket.LayoutManifest);
        Assert.Contains("field=Buttons;type=bool[]", TestArrayPacket.LayoutManifest);
        Assert.Contains("array=fixed;count=4;elementType=bool;elementBits=1;bits=4", TestArrayPacket.LayoutManifest);
    }

    [Fact]
    public void GeneratedPacket_BoundedArrayTreatsNullVariableArrayAsEmpty()
    {
        var packet = new TestArrayPacket
        {
            EntityIds = null!,
            Buttons = new[] { false, false, false, false },
            Snapshots = null!
        };
        var buffer = new byte[TestArrayPacket.MaxBytes];

        packet.Serialize(new BitWriter(buffer));

        var restored = TestArrayPacket.Read(new BitReader(buffer));
        Assert.Empty(restored.EntityIds);
        Assert.Empty(restored.Snapshots);
        Assert.Equal(packet.Buttons, restored.Buttons);
    }

    [Fact]
    public void GeneratedPacket_BoundedArrayTooLongThrows()
    {
        var packet = new TestArrayPacket
        {
            EntityIds = new[] { 1, 2, 3, 4, 5 },
            Buttons = new[] { false, false, false, false },
            Snapshots = Array.Empty<TestPlayerSnapshot>()
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(new BitWriter(new byte[64])));
        Assert.Contains("EntityIds", ex.ParamName);
    }

    [Fact]
    public void GeneratedPacket_FixedArrayWrongLengthThrowsEvenWhenUnchecked()
    {
        var packet = new TestArrayPacket
        {
            EntityIds = Array.Empty<int>(),
            Buttons = new[] { true, false },
            Snapshots = Array.Empty<TestPlayerSnapshot>()
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(new BitWriter(new byte[64])));
        Assert.Throws<ArgumentOutOfRangeException>(() => packet.SerializeUnchecked(new BitWriter(new byte[64])));
    }

    [Fact]
    public void GeneratedPacket_TrySerializeReturnsBytesWrittenAndRejectsSmallBuffer()
    {
        var packet = new TestGeneratedApiPacket
        {
            Value = 42,
            Flag = true,
            Name = "AB",
            State = TestGameState.Running
        };

        Assert.False(packet.TrySerialize(new byte[TestGeneratedApiPacket.MaxBytes - 1], out var smallBytesWritten));
        Assert.Equal(0, smallBytesWritten);

        var buffer = new byte[TestGeneratedApiPacket.MaxBytes];
        Assert.True(packet.TrySerialize(buffer, out var bytesWritten));
        Assert.True(bytesWritten > 0);

        var restored = TestGeneratedApiPacket.Read(new BitReader(buffer));
        Assert.Equal(packet.Value, restored.Value);
        Assert.Equal(packet.Flag, restored.Flag);
        Assert.Equal(packet.Name, restored.Name);
        Assert.Equal(packet.State, restored.State);
    }

    [Fact]
    public void GeneratedPacket_SerializeUncheckedSkipsGeneratedRangeChecks()
    {
        var packet = new TestRangeOverflowPacket { SmallValue = 200, Latitude = 0 };
        var buffer = new byte[16];

        Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(new BitWriter(buffer)));

        packet.SerializeUnchecked(new BitWriter(buffer));

        var restored = TestRangeOverflowPacket.Read(new BitReader(buffer));
        Assert.NotEqual(200, restored.SmallValue);
    }

    [Fact]
    public void GeneratedPacket_DeserializesOlderVersionStream_FallsBackGracefully()
    {
        // Arrange
        var buffer = new byte[128];
        var writer = new BitWriter(buffer);

        // Simulate a Version 1 stream manually:
        writer.WriteInt(1, 4); // Version 1 Header
        writer.WriteInt(75, 7); // BaseSpeed: Range 0-100 (7 bits)
        // Note: version 2 field 'ExtraBoost' is NOT written!

        // Act - Deserialize version 1 stream into version 2 model
        var reader = new BitReader(buffer);
        var restored = new TestVersionedInput();
        restored.Deserialize(reader);

        // Assert BaseSpeed is read, and ExtraBoost is defaulted to false
        Assert.Equal(75, restored.BaseSpeed);
        Assert.False(restored.ExtraBoost); // Fallback is triggered and didn't crash
    }

    [Fact]
    public void GeneratedPacket_RespectsIgnoreAndPromotesPrivateMembers()
    {
        // Arrange
        var buffer = new byte[128];
        var original = new TestIgnoredAndPrivatePacket
        {
            IgnoredProperty1 = 123,
            IgnoredProperty2 = "ignored value",
            PublicTarget = true,
            InternalPromotedProperty = "test internal"
        };
        original.SetPrivateField(99);

        // Act - Serialize
        var writer = new BitWriter(buffer);
        original.Serialize(writer);

        // Act - Deserialize
        var reader = new BitReader(buffer);
        var restored = new TestIgnoredAndPrivatePacket();
        restored.Deserialize(reader);

        // Assert - promoted private and internal values are serialized
        Assert.True(restored.PublicTarget);
        Assert.Equal(99, restored.GetPrivateField());
        Assert.Equal("test internal", restored.InternalPromotedProperty);

        // Assert - ignored properties are NOT serialized (stay at their default values!)
        Assert.Equal(999, restored.IgnoredProperty1);
        Assert.Equal("ignore me", restored.IgnoredProperty2);
    }

    [Fact]
    public void GeneratedPacket_SerializesUnicodeAndEmojiStringsCorrectly()
    {
        // Arrange
        var buffer = new byte[256];
        var original = new TestUnicodePacket
        {
            Message = "你好👋🌟"
        };

        // Act - Serialize
        var writer = new BitWriter(buffer);
        original.Serialize(writer);

        // Act - Deserialize
        var reader = new BitReader(buffer);
        var restored = new TestUnicodePacket();
        restored.Deserialize(reader);

        // Assert
        Assert.Equal("你好👋🌟", restored.Message);
    }

    [Fact]
    public void GeneratedPacket_SupportsRequiredAndInitOnlyProperties()
    {
        // Arrange
        var buffer = new byte[128];
        var original = new TestInitAndRequiredPacket
        {
            RegularVal = 123,
            InitOnlyVal = 456,
            RequiredName = "required test"
        };

        // Act - Serialize
        var writer = new BitWriter(buffer);
        original.Serialize(writer);

        // Act - Deserialize (Static Read factory handles init/required)
        var reader = new BitReader(buffer);
        var restored = TestInitAndRequiredPacket.Read(reader);

        // Assert
        Assert.Equal(123, restored.RegularVal);
        Assert.Equal(456, restored.InitOnlyVal);
        Assert.Equal("required test", restored.RequiredName);
    }

    [Fact]
    public void GeneratedPacket_SupportsPrimaryConstructors()
    {
        // Arrange
        var buffer = new byte[128];
        var original = new TestPrimaryCtorPacket(25, "Alex");

        // Act - Serialize
        var writer = new BitWriter(buffer);
        original.Serialize(writer);

        // Act - Deserialize (Static Read factory matches constructor params)
        var reader = new BitReader(buffer);
        var restored = TestPrimaryCtorPacket.Read(reader);

        // Assert
        Assert.Equal(25, restored.Age);
        Assert.Equal("Alex", restored.Nickname);
    }

    [Fact]
    public void Generator_FailsWhenPropertyLacksGetterOrSetter()
    {
        // Arrange
        var sourceCode = @"
using BitPack;

[BitPacket]
public partial class BadPacketNoGetter
{
    // Public property with no getter
    public int AimAngle { set { } }
}

[BitPacket]
public partial class BadPacketNoSetter
{
    // Public property with no setter and no constructor parameter matching it
    public int AimAngle { get; }
}
";

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(BitPacketAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(MaxLengthAttribute).Assembly.Location)
            })
            .GroupBy(r => r.Display)
            .Select(g => g.First())
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new PacketGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.Contains(errors, e => e.GetMessage().Contains("must have a getter accessor"));
        Assert.Contains(errors, e => e.GetMessage().Contains("must have a setter or init accessor"));
    }

    [Fact]
    public void Generator_FailsWhenBitFieldKeysAreMixedWithUnkeyedMembers()
    {
        var errors = GetGeneratorErrors(@"
using BitPack;

[BitPacket]
public partial class BadMixedKeysPacket
{
    [BitFieldKey(0)] public int A { get; set; }
    public int B { get; set; }
}
");

        Assert.Contains(errors, e => e.Id == "BP0003" && e.GetMessage().Contains("all serializable members"));
    }

    [Fact]
    public void Generator_FailsWhenBitFieldKeysAreDuplicated()
    {
        var errors = GetGeneratorErrors(@"
using BitPack;

[BitPacket]
public partial class BadDuplicateKeysPacket
{
    [BitFieldKey(0)] public int A { get; set; }
    [BitFieldKey(0)] public int B { get; set; }
}
");

        Assert.Contains(errors, e => e.Id == "BP0003" && e.GetMessage().Contains("key 0"));
    }

    [Fact]
    public void Generator_FailsWhenBitFieldKeyIsNegative()
    {
        var errors = GetGeneratorErrors(@"
using BitPack;

[BitPacket]
public partial class BadNegativeKeyPacket
{
    [BitFieldKey(-1)] public int A { get; set; }
}
");

        Assert.Contains(errors, e => e.Id == "BP0003" && e.GetMessage().Contains("zero or greater"));
    }

    [Fact]
    public void Generator_FailsWhenAbstractTypeIsMarkedAsPacket()
    {
        var errors = GetGeneratorErrors(@"
using BitPack;

[BitPacket]
public abstract partial class BadAbstractPacket
{
    public int A { get; set; }
}
");

        Assert.Contains(errors, e => e.Id == "BP0001" && e.GetMessage().Contains("Abstract types cannot"));
    }

    [Fact]
    public void Generator_FailsWhenArrayIsUnbounded()
    {
        var errors = GetGeneratorErrors(@"
using BitPack;

[BitPacket]
public partial class BadUnboundedArrayPacket
{
    public int[] Values { get; set; }
}
");

        Assert.Contains(errors, e => e.Id == "BP0004" && e.GetMessage().Contains("MaxLength"));
    }

    [Fact]
    public void Generator_FailsWhenArrayHasBothLengthModes()
    {
        var errors = GetGeneratorErrors(@"
using System.ComponentModel.DataAnnotations;
using BitPack;

[BitPacket]
public partial class BadDualBoundArrayPacket
{
    [MaxLength(4)]
    [BitPack.FixedCountAttribute(4)]
    public int[] Values { get; set; }
}
");

        Assert.Contains(errors, e => e.Id == "BP0004" && e.GetMessage().Contains("either"));
    }

    [Fact]
    public void Generator_FailsWhenArrayIsMultidimensional()
    {
        var errors = GetGeneratorErrors(@"
using System.ComponentModel.DataAnnotations;
using BitPack;

[BitPacket]
public partial class BadMultidimensionalArrayPacket
{
    [MaxLength(4)]
    public int[,] Values { get; set; }
}
");

        Assert.Contains(errors, e => e.Id == "BP0004" && e.GetMessage().Contains("one-dimensional"));
    }

    [Fact]
    public void Generator_FailsWhenArrayElementIsString()
    {
        var errors = GetGeneratorErrors(@"
using System.ComponentModel.DataAnnotations;
using BitPack;

[BitPacket]
public partial class BadStringArrayPacket
{
    [MaxLength(4)]
    public string[] Values { get; set; }
}
");

        Assert.Contains(errors, e => e.Id == "BP0004" && e.GetMessage().Contains("string arrays"));
    }

    [Fact]
    public void GeneratedPacket_IntegerRangeOverflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestRangeOverflowPacket { SmallValue = 200, Latitude = 0 };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("SmallValue", ex.ParamName);
        Assert.Contains("[0, 100]", ex.Message);
    }

    [Fact]
    public void GeneratedPacket_IntegerRangeUnderflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestRangeOverflowPacket { SmallValue = -1, Latitude = 0 };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("SmallValue", ex.ParamName);
        Assert.Contains("[0, 100]", ex.Message);
    }

    [Fact]
    public void GeneratedPacket_NegativeRangeOverflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestRangeOverflowPacket { SmallValue = 0, Latitude = 200 };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("Latitude", ex.ParamName);
        Assert.Contains("[-180, 180]", ex.Message);
    }

    [Fact]
    public void GeneratedPacket_NegativeRangeUnderflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestRangeOverflowPacket { SmallValue = 0, Latitude = -200 };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("Latitude", ex.ParamName);
        Assert.Contains("[-180, 180]", ex.Message);
    }

    [Fact]
    public void GeneratedPacket_IntegerAtRangeBoundary_DoesNotThrow()
    {
        var buffer = new byte[128];

        var packetAtMin = new TestRangeOverflowPacket { SmallValue = 0, Latitude = 0 };
        var writer1 = new BitWriter(buffer);
        packetAtMin.Serialize(writer1);

        var reader1 = new BitReader(buffer);
        var restoredMin = new TestRangeOverflowPacket();
        restoredMin.Deserialize(reader1);
        Assert.Equal(0, restoredMin.SmallValue);

        buffer.AsSpan().Clear();
        writer1.Reset();

        var packetAtMax = new TestRangeOverflowPacket { SmallValue = 100, Latitude = 0 };
        packetAtMax.Serialize(writer1);

        var reader2 = new BitReader(buffer);
        var restoredMax = new TestRangeOverflowPacket();
        restoredMax.Deserialize(reader2);
        Assert.Equal(100, restoredMax.SmallValue);
    }

    [Fact]
    public void GeneratedPacket_StringMaxLengthOverflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestStringOverflowPacket { Name = "123456", Bio = "" };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("value", ex.ParamName);
    }

    [Fact]
    public void GeneratedPacket_StringAtMaxLength_DoesNotThrow()
    {
        var buffer = new byte[128];
        var packet = new TestStringOverflowPacket { Name = "12345", Bio = "" };

        var writer = new BitWriter(buffer);
        packet.Serialize(writer);

        var reader = new BitReader(buffer);
        var restored = new TestStringOverflowPacket();
        restored.Deserialize(reader);
        Assert.Equal("12345", restored.Name);
    }

    [Fact]
    public void GeneratedPacket_StringLengthAttribute_RoundTrips()
    {
        var buffer = new byte[128];
        var packet = new TestStringOverflowPacket { Name = "", Bio = "1234567890" };

        var writer = new BitWriter(buffer);
        packet.Serialize(writer);

        var reader = new BitReader(buffer);
        var restored = new TestStringOverflowPacket();
        restored.Deserialize(reader);
        Assert.Equal("1234567890", restored.Bio);
    }

    [Fact]
    public void GeneratedPacket_StringLengthAttributeOverflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestStringOverflowPacket { Name = "", Bio = "12345678901" };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("value", ex.ParamName);
    }

    [Fact]
    public void GeneratedPacket_QuantizedFloatRangeOverflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestQuantizedOverflowPacket { Coord = 600f };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("Coord", ex.ParamName);
        Assert.Contains("[-500, 500]", ex.Message);
    }

    [Fact]
    public void GeneratedPacket_QuantizedFloatRangeUnderflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestQuantizedOverflowPacket { Coord = -600f };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("Coord", ex.ParamName);
        Assert.Contains("[-500, 500]", ex.Message);
    }

    [Fact]
    public void GeneratedPacket_QuantizedFloatAtBoundary_DoesNotThrow()
    {
        var buffer = new byte[128];

        var packetAtMax = new TestQuantizedOverflowPacket { Coord = 500f };
        var writer = new BitWriter(buffer);
        packetAtMax.Serialize(writer);

        var reader = new BitReader(buffer);
        var restoredMax = new TestQuantizedOverflowPacket();
        restoredMax.Deserialize(reader);
        Assert.Equal(500f, restoredMax.Coord, 2);

        buffer.AsSpan().Clear();
        writer.Reset();

        var packetAtMin = new TestQuantizedOverflowPacket { Coord = -500f };
        packetAtMin.Serialize(writer);

        var reader2 = new BitReader(buffer);
        var restoredMin = new TestQuantizedOverflowPacket();
        restoredMin.Deserialize(reader2);
        Assert.Equal(-500f, restoredMin.Coord, 2);
    }

    [Fact]
    public void GeneratedPacket_EnumWithRangeOverflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestEnumOverflowPacket
            { SmallRangeState = (TestGameState)6, AutoDetectState = TestGameState.Idle };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("SmallRangeState", ex.ParamName);
        Assert.Contains("[0, 5]", ex.Message);
    }

    [Fact]
    public void GeneratedPacket_EnumAutoDetectOverflow_Throws()
    {
        var buffer = new byte[128];
        var packet = new TestEnumOverflowPacket
            { SmallRangeState = TestGameState.Idle, AutoDetectState = (TestGameState)99 };

        var writer = new BitWriter(buffer);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packet.Serialize(writer));
        Assert.Contains("AutoDetectState", ex.ParamName);
    }

    private static List<Diagnostic> GetGeneratorErrors(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new PacketGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return diagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
    }
}
