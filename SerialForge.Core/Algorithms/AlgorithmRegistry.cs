namespace SerialForge.Core.Algorithms;

public sealed class AlgorithmRegistry
{
    private readonly Dictionary<string, IComputeAlgorithm> _algos = new(StringComparer.OrdinalIgnoreCase);

    public AlgorithmRegistry()
    {
        Register("length", new LengthAlgorithm());
        Register("sum8", new Sum8Algorithm());
        Register("xor8", new Xor8Algorithm());
        Register("crc16", new Crc16Algorithm());
        Register("crc32", new Crc32Algorithm());
    }

    public void Register(string id, IComputeAlgorithm algo) => _algos[id] = algo;
    public IComputeAlgorithm Get(string id) =>
        _algos.TryGetValue(id, out var a) ? a : throw new KeyNotFoundException($"unknown algorithm '{id}'");
}
