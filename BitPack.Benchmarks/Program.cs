using BenchmarkDotNet.Running;

namespace BitPack.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<SerializationBenchmarks>();
    }
}
