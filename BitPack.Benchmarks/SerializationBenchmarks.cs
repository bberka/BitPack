using System;
using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using MemoryPack;
using MessagePack;
using MessagePack.Resolvers;
using ProtoBuf;

namespace BitPack.Benchmarks;

[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private readonly Consumer _consumer = new Consumer();

    private byte[] _simpleBitBuffer = null!;
    private byte[] _simpleJsonBuffer = null!;
    private byte[] _simpleMsgPackBuffer = null!;
    private byte[] _simpleMemPackBuffer = null!;
    private byte[] _simpleProtoBufBuffer = null!;
    private int _simpleJsonLen;
    private int _simpleMsgPackLen;
    private int _simpleMemPackLen;
    private int _simpleProtoBufLen;
    private SimplePacket _simplePacket;
    private BitWriter _simpleBitWriter = null!;
    private BitReader _simpleBitReader = null!;
    private ArrayBufferWriter<byte> _simpleMemoryPackWriter = null!;
    private ArrayBufferWriter<byte> _simpleMessagePackWriter = null!;

    private byte[] _complexBitBuffer = null!;
    private byte[] _complexJsonBuffer = null!;
    private byte[] _complexMsgPackBuffer = null!;
    private byte[] _complexMemPackBuffer = null!;
    private byte[] _complexProtoBufBuffer = null!;
    private int _complexJsonLen;
    private int _complexMsgPackLen;
    private int _complexMemPackLen;
    private int _complexProtoBufLen;
    private ComplexPacket _complexPacket;
    private BitWriter _complexBitWriter = null!;
    private BitReader _complexBitReader = null!;
    private ArrayBufferWriter<byte> _complexMemoryPackWriter = null!;
    private ArrayBufferWriter<byte> _complexMessagePackWriter = null!;

    [GlobalSetup]
    public void Setup()
    {
        try
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Console.WriteLine("[Setup] Set process priority to High.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Setup] Failed to set process priority: {ex.Message}");
        }

        _simpleBitBuffer = new byte[512];
        _simpleJsonBuffer = new byte[512];
        _simpleMsgPackBuffer = new byte[512];
        _simpleMemPackBuffer = new byte[512];
        _simpleProtoBufBuffer = new byte[512];

        _complexBitBuffer = new byte[1024];
        _complexJsonBuffer = new byte[1024];
        _complexMsgPackBuffer = new byte[1024];
        _complexMemPackBuffer = new byte[1024];
        _complexProtoBufBuffer = new byte[1024];

        _simplePacket = new SimplePacket
        {
            Value1 = 789,
            Flag1 = true,
            Code1 = 8,
            Speed = 45.67f,
            Id1 = 987654321,
            Score = 850.5,
            Angle = 120,
            Length = 3456,
            Temperature = -15,
            Ticks = 88888,
            Flag2 = false,
            Code2 = 220
        };

        _complexPacket = new ComplexPacket
        {
            Name = "Alex",
            Description = "Multiplayer Engine Pilot",
            Value1 = 789,
            Flag1 = true,
            Code1 = 8,
            Speed = 45.67f,
            Id1 = 987654321,
            Score = 850.5,
            CreatedAt = DateTime.UtcNow,
            InnerSimple = _simplePacket,
            Snapshot = new InnerSnapshot { X = -234.56f, Y = 456.78f },
            State = BenchmarkGameState.Playing
        };

        _simpleBitWriter = new BitWriter(_simpleBitBuffer);
        _simpleBitReader = new BitReader(_simpleBitBuffer);
        _complexBitWriter = new BitWriter(_complexBitBuffer);
        _complexBitReader = new BitReader(_complexBitBuffer);
        _simpleMemoryPackWriter = new ArrayBufferWriter<byte>(512);
        _simpleMessagePackWriter = new ArrayBufferWriter<byte>(512);
        _complexMemoryPackWriter = new ArrayBufferWriter<byte>(1024);
        _complexMessagePackWriter = new ArrayBufferWriter<byte>(1024);

        _simpleBitWriter.Reset();
        _simplePacket.Serialize(_simpleBitWriter);
        var simpleBitSize = _simpleBitWriter.BytesWritten;

        using (var ms = new MemoryStream(_simpleJsonBuffer))
        using (var writer = new Utf8JsonWriter(ms))
        {
            JsonSerializer.Serialize(writer, _simplePacket, BenchmarkPacketJsonContext.Default.SimplePacket);
            _simpleJsonLen = (int)ms.Position;
        }

        var sMpBytes = MessagePackSerializer.Serialize(_simplePacket, ContractlessStandardResolver.Options);
        Array.Copy(sMpBytes, _simpleMsgPackBuffer, sMpBytes.Length);
        _simpleMsgPackLen = sMpBytes.Length;

        var sMemPackBytes = MemoryPackSerializer.Serialize(_simplePacket);
        Array.Copy(sMemPackBytes, _simpleMemPackBuffer, sMemPackBytes.Length);
        _simpleMemPackLen = sMemPackBytes.Length;

        using (var ms = new MemoryStream(_simpleProtoBufBuffer))
        {
            Serializer.Serialize(ms, _simplePacket);
            _simpleProtoBufLen = (int)ms.Position;
        }

        _complexBitWriter.Reset();
        _complexPacket.Serialize(_complexBitWriter);
        var complexBitSize = _complexBitWriter.BytesWritten;

        using (var ms = new MemoryStream(_complexJsonBuffer))
        using (var writer = new Utf8JsonWriter(ms))
        {
            JsonSerializer.Serialize(writer, _complexPacket, BenchmarkPacketJsonContext.Default.ComplexPacket);
            _complexJsonLen = (int)ms.Position;
        }

        var cMpBytes = MessagePackSerializer.Serialize(_complexPacket, ContractlessStandardResolver.Options);
        Array.Copy(cMpBytes, _complexMsgPackBuffer, cMpBytes.Length);
        _complexMsgPackLen = cMpBytes.Length;

        var cMemPackBytes = MemoryPackSerializer.Serialize(_complexPacket);
        Array.Copy(cMemPackBytes, _complexMemPackBuffer, cMemPackBytes.Length);
        _complexMemPackLen = cMemPackBytes.Length;

        using (var ms = new MemoryStream(_complexProtoBufBuffer))
        {
            Serializer.Serialize(ms, _complexPacket);
            _complexProtoBufLen = (int)ms.Position;
        }

        Console.WriteLine($"[Setup] Simple BitPack Size: {simpleBitSize} bytes.");
        Console.WriteLine($"[Setup] Simple JSON Size: {_simpleJsonLen} bytes.");
        Console.WriteLine($"[Setup] Simple MessagePack Size: {_simpleMsgPackLen} bytes.");
        Console.WriteLine($"[Setup] Simple MemoryPack Size: {_simpleMemPackLen} bytes.");
        Console.WriteLine($"[Setup] Simple ProtoBuf Size: {_simpleProtoBufLen} bytes.");
        Console.WriteLine($"[Setup] Complex BitPack Size: {complexBitSize} bytes.");
        Console.WriteLine($"[Setup] Complex JSON Size: {_complexJsonLen} bytes.");
        Console.WriteLine($"[Setup] Complex MessagePack Size: {_complexMsgPackLen} bytes.");
        Console.WriteLine($"[Setup] Complex MemoryPack Size: {_complexMemPackLen} bytes.");
        Console.WriteLine($"[Setup] Complex ProtoBuf Size: {_complexProtoBufLen} bytes.");
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "ReusableBuffer")]
    public int Serialize_Simple_BitPack_ReusableBuffer()
    {
        _simpleBitWriter.Reset();
        _simplePacket.Serialize(_simpleBitWriter);
        return _simpleBitWriter.BytesWritten;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "FreshBuffer")]
    public int Serialize_Simple_BitPack_FreshBuffer()
    {
        var buffer = new byte[SimplePacket.MaxBytes];
        var writer = new BitWriter(buffer);
        _simplePacket.Serialize(writer);
        return writer.BytesWritten;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "PooledBuffer")]
    public int Serialize_Simple_BitPack_PooledBuffer()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(SimplePacket.MaxBytes);
        try
        {
            var writer = new BitWriter(buffer);
            _simplePacket.Serialize(writer);
            return writer.BytesWritten;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Deserialize")]
    public void Deserialize_Simple_BitPack()
    {
        _simpleBitReader.Reset();
        _consumer.Consume(SimplePacket.Read(_simpleBitReader));
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "ReusableWriter")]
    public int Serialize_Simple_MemoryPack_ReusableWriter()
    {
        _simpleMemoryPackWriter.Clear();
        MemoryPackSerializer.Serialize(_simpleMemoryPackWriter, _simplePacket);
        return _simpleMemoryPackWriter.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "FreshWriter")]
    public int Serialize_Simple_MemoryPack_FreshWriter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        MemoryPackSerializer.Serialize(buffer, _simplePacket);
        return buffer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Deserialize")]
    public void Deserialize_Simple_MemoryPack()
    {
        var readSpan = new ReadOnlySpan<byte>(_simpleMemPackBuffer, 0, _simpleMemPackLen);
        _consumer.Consume(MemoryPackSerializer.Deserialize<SimplePacket>(readSpan));
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "ReusableWriter")]
    public int Serialize_Simple_MessagePack_ReusableWriter()
    {
        _simpleMessagePackWriter.Clear();
        MessagePackSerializer.Serialize(_simpleMessagePackWriter, _simplePacket, ContractlessStandardResolver.Options);
        return _simpleMessagePackWriter.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "FreshWriter")]
    public int Serialize_Simple_MessagePack_FreshWriter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        MessagePackSerializer.Serialize(buffer, _simplePacket, ContractlessStandardResolver.Options);
        return buffer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Deserialize")]
    public void Deserialize_Simple_MessagePack()
    {
        var readSpan = new ReadOnlyMemory<byte>(_simpleMsgPackBuffer, 0, _simpleMsgPackLen);
        _consumer.Consume(MessagePackSerializer.Deserialize<SimplePacket>(readSpan, ContractlessStandardResolver.Options));
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "ReusableBuffer")]
    public int Serialize_Simple_ProtoBuf_ReusableBuffer()
    {
        using var ms = new MemoryStream(_simpleProtoBufBuffer);
        Serializer.Serialize(ms, _simplePacket);
        return (int)ms.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "FreshBuffer")]
    public int Serialize_Simple_ProtoBuf_FreshBuffer()
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, _simplePacket);
        return (int)ms.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Deserialize")]
    public void Deserialize_Simple_ProtoBuf()
    {
        using var ms = new MemoryStream(_simpleProtoBufBuffer, 0, _simpleProtoBufLen);
        _consumer.Consume(Serializer.Deserialize<SimplePacket>(ms));
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "ReusableBuffer")]
    public int Serialize_Simple_JsonContext_ReusableBuffer()
    {
        using var ms = new MemoryStream(_simpleJsonBuffer);
        using var writer = new Utf8JsonWriter(ms);
        JsonSerializer.Serialize(writer, _simplePacket, BenchmarkPacketJsonContext.Default.SimplePacket);
        return (int)ms.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize", "FreshBuffer")]
    public int Serialize_Simple_JsonContext_FreshBuffer()
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        JsonSerializer.Serialize(writer, _simplePacket, BenchmarkPacketJsonContext.Default.SimplePacket);
        return (int)ms.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Deserialize")]
    public void Deserialize_Simple_JsonContext()
    {
        var utf8Span = new ReadOnlySpan<byte>(_simpleJsonBuffer, 0, _simpleJsonLen);
        _consumer.Consume(JsonSerializer.Deserialize(utf8Span, BenchmarkPacketJsonContext.Default.SimplePacket)!);
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "ReusableBuffer")]
    public int Serialize_Complex_BitPack_ReusableBuffer()
    {
        _complexBitWriter.Reset();
        _complexPacket.Serialize(_complexBitWriter);
        return _complexBitWriter.BytesWritten;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "FreshBuffer")]
    public int Serialize_Complex_BitPack_FreshBuffer()
    {
        var buffer = new byte[ComplexPacket.MaxBytes];
        var writer = new BitWriter(buffer);
        _complexPacket.Serialize(writer);
        return writer.BytesWritten;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "PooledBuffer")]
    public int Serialize_Complex_BitPack_PooledBuffer()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ComplexPacket.MaxBytes);
        try
        {
            var writer = new BitWriter(buffer);
            _complexPacket.Serialize(writer);
            return writer.BytesWritten;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Deserialize")]
    public void Deserialize_Complex_BitPack()
    {
        _complexBitReader.Reset();
        _consumer.Consume(ComplexPacket.Read(_complexBitReader));
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "ReusableWriter")]
    public int Serialize_Complex_MemoryPack_ReusableWriter()
    {
        _complexMemoryPackWriter.Clear();
        MemoryPackSerializer.Serialize(_complexMemoryPackWriter, _complexPacket);
        return _complexMemoryPackWriter.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "FreshWriter")]
    public int Serialize_Complex_MemoryPack_FreshWriter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        MemoryPackSerializer.Serialize(buffer, _complexPacket);
        return buffer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Deserialize")]
    public void Deserialize_Complex_MemoryPack()
    {
        var readSpan = new ReadOnlySpan<byte>(_complexMemPackBuffer, 0, _complexMemPackLen);
        _consumer.Consume(MemoryPackSerializer.Deserialize<ComplexPacket>(readSpan));
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "ReusableWriter")]
    public int Serialize_Complex_MessagePack_ReusableWriter()
    {
        _complexMessagePackWriter.Clear();
        MessagePackSerializer.Serialize(_complexMessagePackWriter, _complexPacket, ContractlessStandardResolver.Options);
        return _complexMessagePackWriter.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "FreshWriter")]
    public int Serialize_Complex_MessagePack_FreshWriter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        MessagePackSerializer.Serialize(buffer, _complexPacket, ContractlessStandardResolver.Options);
        return buffer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Deserialize")]
    public void Deserialize_Complex_MessagePack()
    {
        var readSpan = new ReadOnlyMemory<byte>(_complexMsgPackBuffer, 0, _complexMsgPackLen);
        _consumer.Consume(MessagePackSerializer.Deserialize<ComplexPacket>(readSpan, ContractlessStandardResolver.Options));
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "ReusableBuffer")]
    public int Serialize_Complex_ProtoBuf_ReusableBuffer()
    {
        using var ms = new MemoryStream(_complexProtoBufBuffer);
        Serializer.Serialize(ms, _complexPacket);
        return (int)ms.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "FreshBuffer")]
    public int Serialize_Complex_ProtoBuf_FreshBuffer()
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, _complexPacket);
        return (int)ms.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Deserialize")]
    public void Deserialize_Complex_ProtoBuf()
    {
        using var ms = new MemoryStream(_complexProtoBufBuffer, 0, _complexProtoBufLen);
        _consumer.Consume(Serializer.Deserialize<ComplexPacket>(ms));
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "ReusableBuffer")]
    public int Serialize_Complex_JsonContext_ReusableBuffer()
    {
        using var ms = new MemoryStream(_complexJsonBuffer);
        using var writer = new Utf8JsonWriter(ms);
        JsonSerializer.Serialize(writer, _complexPacket, BenchmarkPacketJsonContext.Default.ComplexPacket);
        return (int)ms.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize", "FreshBuffer")]
    public int Serialize_Complex_JsonContext_FreshBuffer()
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        JsonSerializer.Serialize(writer, _complexPacket, BenchmarkPacketJsonContext.Default.ComplexPacket);
        return (int)ms.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Deserialize")]
    public void Deserialize_Complex_JsonContext()
    {
        var utf8Span = new ReadOnlySpan<byte>(_complexJsonBuffer, 0, _complexJsonLen);
        _consumer.Consume(JsonSerializer.Deserialize(utf8Span, BenchmarkPacketJsonContext.Default.ComplexPacket)!);
    }
}

[BitPacket]
[MessagePackObject]
[MemoryPackable]
[ProtoContract]
public partial record struct SimplePacket
{
    [MessagePack.Key(0)] [ProtoMember(1)] [Range(0, 1000)] public int Value1 { get; set; }
    [MessagePack.Key(1)] [ProtoMember(2)] public bool Flag1 { get; set; }
    [MessagePack.Key(2)] [ProtoMember(3)] [Range(0, 10)] public byte Code1 { get; set; }
    [MessagePack.Key(3)] [ProtoMember(4)] [Range(-100.0f, 100.0f)] [Precision(2)] public float Speed { get; set; }
    [MessagePack.Key(4)] [ProtoMember(5)] [Range(0, 1000000000)] public long Id1 { get; set; }
    [MessagePack.Key(5)] [ProtoMember(6)] [Range(0.0, 1000.0)] [Precision(1)] public double Score { get; set; }
    [MessagePack.Key(6)] [ProtoMember(7)] [Range(-180, 180)] public short Angle { get; set; }
    [MessagePack.Key(7)] [ProtoMember(8)] [Range(0, 5000)] public ushort Length { get; set; }
    [MessagePack.Key(8)] [ProtoMember(9)] [Range(-40, 80)] public sbyte Temperature { get; set; }
    [MessagePack.Key(9)] [ProtoMember(10)] [Range(0, 100000)] public uint Ticks { get; set; }
    [MessagePack.Key(10)] [ProtoMember(11)] public bool Flag2 { get; set; }
    [MessagePack.Key(11)] [ProtoMember(12)] [Range(0, 250)] public byte Code2 { get; set; }
}

[BitPacket]
[MessagePackObject]
[MemoryPackable]
[ProtoContract]
public partial record struct ComplexPacket
{
    [MessagePack.Key(0)] [ProtoMember(1)] [MaxLength(16)] public string Name { get; set; }
    [MessagePack.Key(1)] [ProtoMember(2)] [MaxLength(32)] public string Description { get; set; }
    [MessagePack.Key(2)] [ProtoMember(3)] [Range(0, 1000)] public int Value1 { get; set; }
    [MessagePack.Key(3)] [ProtoMember(4)] public bool Flag1 { get; set; }
    [MessagePack.Key(4)] [ProtoMember(5)] [Range(0, 10)] public byte Code1 { get; set; }
    [MessagePack.Key(5)] [ProtoMember(6)] [Range(-100.0f, 100.0f)] [Precision(2)] public float Speed { get; set; }
    [MessagePack.Key(6)] [ProtoMember(7)] [Range(0, 1000000000)] public long Id1 { get; set; }
    [MessagePack.Key(7)] [ProtoMember(8)] [Range(0.0, 1000.0)] [Precision(1)] public double Score { get; set; }
    [MessagePack.Key(8)] [ProtoMember(9)] public DateTime CreatedAt { get; set; }
    [MessagePack.Key(9)] [ProtoMember(10)] public SimplePacket InnerSimple { get; set; }
    [MessagePack.Key(10)] [ProtoMember(11)] public InnerSnapshot Snapshot { get; set; }
    [MessagePack.Key(11)] [ProtoMember(12)] public BenchmarkGameState State { get; set; }
}

[BitPacket]
[MessagePackObject]
[MemoryPackable]
[ProtoContract]
public partial record struct InnerSnapshot
{
    [MessagePack.Key(0)] [ProtoMember(1)] [Range(-500.0f, 500.0f)] [Precision(2)] public float X { get; set; }
    [MessagePack.Key(1)] [ProtoMember(2)] [Range(-500.0f, 500.0f)] [Precision(2)] public float Y { get; set; }
}

public enum BenchmarkGameState
{
    Idle = 0,
    Playing = 1,
    GameOver = 2
}

[JsonSerializable(typeof(SimplePacket))]
[JsonSerializable(typeof(ComplexPacket))]
internal partial class BenchmarkPacketJsonContext : JsonSerializerContext
{
}
