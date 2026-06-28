using System;
using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BitPack;
using MessagePack;
using MemoryPack;
using ProtoBuf;

namespace BitPack.Benchmarks;

[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private byte[] _bitBuffer = null!;
    private byte[] _jsonBuffer = null!;
    private byte[] _msgPackBuffer = null!;
    private byte[] _nbMsgPackBuffer = null!;
    private byte[] _memPackBuffer = null!;
    private byte[] _protoBufBuffer = null!;
    
    private int _jsonLen;
    private int _msgPackLen;
    private int _nbMsgPackLen;
    private int _memPackLen;
    private int _protoBufLen;

    private BenchmarkPacket _testPacket;
    
    private BitWriter _bitWriter = null!;
    private BitReader _bitReader = null!;
    private Nerdbank.MessagePack.MessagePackSerializer _nbSerializer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bitBuffer = new byte[256];
        _jsonBuffer = new byte[256];
        _msgPackBuffer = new byte[256];
        _nbMsgPackBuffer = new byte[256];
        _memPackBuffer = new byte[256];
        _protoBufBuffer = new byte[256];
        
        _testPacket = new BenchmarkPacket
        {
            AimAngle = 270,
            IsMoving = true,
            ShipClass = 2,
            PilotName = "Alex"
        };

        _bitWriter = new BitWriter(_bitBuffer);
        _bitReader = new BitReader(_bitBuffer);
        _nbSerializer = new Nerdbank.MessagePack.MessagePackSerializer();
        
        // 1. BitPack setup
        _bitWriter.Reset();
        _testPacket.Serialize(_bitWriter);
        var bitSize = _bitWriter.BytesWritten;

        // 2. System.Text.Json setup
        using (var ms = new MemoryStream(_jsonBuffer))
        using (var writer = new Utf8JsonWriter(ms))
        {
            JsonSerializer.Serialize(writer, _testPacket, BenchmarkPacketJsonContext.Default.BenchmarkPacket);
            _jsonLen = (int)ms.Position;
        }

        // 3. MessagePack setup
        var mpBytes = MessagePackSerializer.Serialize(_testPacket, MessagePack.Resolvers.ContractlessStandardResolver.Options);
        Array.Copy(mpBytes, _msgPackBuffer, mpBytes.Length);
        _msgPackLen = mpBytes.Length;

        // 4. Nerdbank.MessagePack setup
        var nbBytes = _nbSerializer.Serialize(_testPacket);
        Array.Copy(nbBytes, _nbMsgPackBuffer, nbBytes.Length);
        _nbMsgPackLen = nbBytes.Length;

        // 5. MemoryPack setup
        var memPackBytes = MemoryPackSerializer.Serialize(_testPacket);
        Array.Copy(memPackBytes, _memPackBuffer, memPackBytes.Length);
        _memPackLen = memPackBytes.Length;

        // 6. ProtoBuf setup
        using (var ms = new MemoryStream(_protoBufBuffer))
        {
            Serializer.Serialize(ms, _testPacket);
            _protoBufLen = (int)ms.Position;
        }

        Console.WriteLine($"[Setup] BitPack Size: {bitSize} bytes.");
        Console.WriteLine($"[Setup] System.Text.Json Size: {_jsonLen} bytes.");
        Console.WriteLine($"[Setup] MessagePack Size: {_msgPackLen} bytes.");
        Console.WriteLine($"[Setup] Nerdbank.MessagePack Size: {_nbMsgPackLen} bytes.");
        Console.WriteLine($"[Setup] MemoryPack Size: {_memPackLen} bytes.");
        Console.WriteLine($"[Setup] ProtoBuf Size: {_protoBufLen} bytes.");
    }

    // --- BitPack ---

    [Benchmark]
    public int Serialize_BitPack()
    {
        _bitWriter.Reset();
        _testPacket.Serialize(_bitWriter);
        return _bitWriter.BytesWritten;
    }

    [Benchmark]
    public BenchmarkPacket Deserialize_BitPack()
    {
        _bitReader.Reset();
        var packet = new BenchmarkPacket();
        packet.Deserialize(_bitReader);
        return packet;
    }

    // --- MessagePack ---

    [Benchmark]
    public int Serialize_MessagePack()
    {
        var bytes = MessagePackSerializer.Serialize(_testPacket, MessagePack.Resolvers.ContractlessStandardResolver.Options);
        return bytes.Length;
    }

    [Benchmark]
    public BenchmarkPacket Deserialize_MessagePack()
    {
        var utf8Span = new ReadOnlySpan<byte>(_msgPackBuffer, 0, _msgPackLen);
        return MessagePackSerializer.Deserialize<BenchmarkPacket>(utf8Span.ToArray(), MessagePack.Resolvers.ContractlessStandardResolver.Options);
    }

    // --- Nerdbank.MessagePack ---

    [Benchmark]
    public int Serialize_NerdbankMessagePack()
    {
        var bytes = _nbSerializer.Serialize(_testPacket);
        return bytes.Length;
    }

    [Benchmark]
    public BenchmarkPacket Deserialize_NerdbankMessagePack()
    {
        var sequence = new ReadOnlySequence<byte>(_nbMsgPackBuffer, 0, _nbMsgPackLen);
        return _nbSerializer.Deserialize<BenchmarkPacket>(sequence);
    }

    // --- MemoryPack ---

    [Benchmark]
    public int Serialize_MemoryPack()
    {
        var bytes = MemoryPackSerializer.Serialize(_testPacket);
        return bytes.Length;
    }

    [Benchmark]
    public BenchmarkPacket Deserialize_MemoryPack()
    {
        var utf8Span = new ReadOnlySpan<byte>(_memPackBuffer, 0, _memPackLen);
        return MemoryPackSerializer.Deserialize<BenchmarkPacket>(utf8Span)!;
    }

    // --- ProtoBuf ---

    [Benchmark]
    public int Serialize_ProtoBuf()
    {
        using var ms = new MemoryStream(_protoBufBuffer);
        ms.Position = 0;
        Serializer.Serialize(ms, _testPacket);
        return (int)ms.Position;
    }

    [Benchmark]
    public BenchmarkPacket Deserialize_ProtoBuf()
    {
        using var ms = new MemoryStream(_protoBufBuffer, 0, _protoBufLen);
        return Serializer.Deserialize<BenchmarkPacket>(ms);
    }

    // --- System.Text.Json (Source Gen Context) ---

    [Benchmark]
    public int Serialize_SystemTextJson_Context()
    {
        using var ms = new MemoryStream(_jsonBuffer);
        ms.Position = 0;
        using var writer = new Utf8JsonWriter(ms);
        JsonSerializer.Serialize(writer, _testPacket, BenchmarkPacketJsonContext.Default.BenchmarkPacket);
        return (int)ms.Position;
    }

    [Benchmark]
    public BenchmarkPacket Deserialize_SystemTextJson_Context()
    {
        var utf8Span = new ReadOnlySpan<byte>(_jsonBuffer, 0, _jsonLen);
        return JsonSerializer.Deserialize(utf8Span, BenchmarkPacketJsonContext.Default.BenchmarkPacket)!;
    }

    // --- System.Text.Json (Dynamic Reflection) ---

    [Benchmark]
    public int Serialize_SystemTextJson_Dynamic()
    {
        using var ms = new MemoryStream(_jsonBuffer);
        ms.Position = 0;
        using var writer = new Utf8JsonWriter(ms);
        JsonSerializer.Serialize(writer, _testPacket);
        return (int)ms.Position;
    }

    [Benchmark]
    public BenchmarkPacket Deserialize_SystemTextJson_Dynamic()
    {
        var utf8Span = new ReadOnlySpan<byte>(_jsonBuffer, 0, _jsonLen);
        return JsonSerializer.Deserialize<BenchmarkPacket>(utf8Span)!;
    }
}

[BitPacket]
[MessagePackObject]
[PolyType.GenerateShape]
[MemoryPackable]
[ProtoContract]
public partial record struct BenchmarkPacket
{
    [MessagePack.Key(0)]
    [ProtoMember(1)]
    [Range(0, 360)]
    public int AimAngle { get; set; }

    [MessagePack.Key(1)]
    [ProtoMember(2)]
    public bool IsMoving { get; set; }

    [MessagePack.Key(2)]
    [ProtoMember(3)]
    [Range(0, 3)]
    public byte ShipClass { get; set; }

    [MessagePack.Key(3)]
    [ProtoMember(4)]
    [MaxLength(16)]
    public string PilotName { get; set; }
}

[JsonSerializable(typeof(BenchmarkPacket))]
internal partial class BenchmarkPacketJsonContext : JsonSerializerContext
{
}
