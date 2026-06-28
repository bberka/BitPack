namespace BitPack;

public interface IBitSerializable
{
    /// <summary>
    /// Writes the packet properties to the bit-stream writer.
    /// </summary>
    void Serialize(BitWriter writer);

    /// <summary>
    /// Reads and restores the packet properties from the bit-stream reader.
    /// </summary>
    void Deserialize(BitReader reader);
}
