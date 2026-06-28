using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;
using BitPack;

namespace BitPack.Tests;

public interface IInventoryItem : IBitSerializable
{
    int ItemId { get; set; }
}

[BitPacket]
public partial record WeaponItem : IInventoryItem
{
    public int ItemId { get; set; }
    
    [Range(0, 100)]
    public int Durability { get; set; }
}

[BitPacket]
public partial record TestPlayerSnapshot
{
    [Range(-500.0, 500.0)]
    [Precision(2)] // Quantized fixed-point: -500.00 to 500.00 (100,001 values -> 17 bits)
    public float X { get; set; }

    [Range(-500.0, 500.0)]
    [Precision(2)]
    public float Y { get; set; }
}

[BitPacket]
public partial record TestPlayerInput
{
    [Range(0, 360)]
    public int AimAngle { get; set; }

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

[BitPacket(2)]
public partial record TestVersionedInput
{
    [Range(0, 100)]
    public int BaseSpeed { get; set; }

    [SinceVersion(2)]
    public bool ExtraBoost { get; set; }
}

[BitPacket]
public partial record TestIgnoredAndPrivatePacket
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int IgnoredProperty1 { get; set; } = 999;

    [System.Runtime.Serialization.IgnoreDataMember]
    public string IgnoredProperty2 { get; set; } = "ignore me";

    public bool PublicTarget { get; set; }

    [System.Runtime.Serialization.DataMember]
    private int privatePromotedField = 42;

    [System.Runtime.Serialization.DataMember]
    internal string InternalPromotedProperty { get; set; } = "internal";

    public int GetPrivateField() => privatePromotedField;
    public void SetPrivateField(int val) => privatePromotedField = val;
}

[BitPacket]
public partial record TestUnicodePacket
{
    [MaxLength(16)]
    public string Message { get; set; } = "";
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
    public void GeneratedPacket_DeserializesOlderVersionStream_FallsBackGracefully()
    {
        // Arrange
        var buffer = new byte[128];
        var writer = new BitWriter(buffer);
        
        // Simulate a Version 1 stream manually:
        writer.WriteInt(1, 4);   // Version 1 Header
        writer.WriteInt(75, 7);  // BaseSpeed: Range 0-100 (7 bits)
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

        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(sourceCode);
        
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        var generator = new BitPack.Generator.PacketGenerator();
        var driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(generator);
        
        // Act
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        var errors = diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
        
        Assert.Contains(errors, e => e.GetMessage().Contains("must have a getter accessor"));
        Assert.Contains(errors, e => e.GetMessage().Contains("must have a setter or init accessor"));
    }
}
