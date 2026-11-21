using FileSort.Generator.Random;

namespace FileSort.Generator.Content.Lines;

/// <summary>
/// The pool of repeated Strings: <see cref="ContentFormat.PoolSize"/> Strings composed by the same rules as fresh
/// ones, from the pool's own random stream. Its content depends on the seed and the content options only, not on
/// the size of the file, so a smaller file stays a prefix of a larger one.
/// </summary>
internal sealed class RepeatPool
{
    private readonly byte[] _bytes;
    private readonly int[] _offsets;

    public RepeatPool(ContentOptions options, StringComposer strings)
    {
        Xoshiro256 random = Xoshiro256.Create(options.Seed, Xoshiro256.PoolStream);
        byte[] scratch = new byte[strings.MaxLength + StringComposer.Slack];
        MemoryStream bytes = new();
        _offsets = new int[ContentFormat.PoolSize + 1];

        for (int entry = 0; entry < ContentFormat.PoolSize; entry++)
        {
            EdgeCases applied = options.EdgeCases == EdgeCases.None
                ? EdgeCases.None
                : EdgeRoll.Apply(random.Next(), options.EdgeCases);
            int length = strings.Compose(ref random, applied, scratch);
            bytes.Write(scratch, 0, length);
            _offsets[entry + 1] = (int)bytes.Length;
        }

        _bytes = bytes.ToArray();
    }

    /// <summary>The bytes of one pool String.</summary>
    public ReadOnlySpan<byte> this[int index] => _bytes.AsSpan(_offsets[index], _offsets[index + 1] - _offsets[index]);
}
