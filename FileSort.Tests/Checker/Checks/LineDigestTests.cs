using System.IO.Hashing;
using System.Text;

using FileSort.Checker.Checks;

namespace FileSort.Tests.Checker.Checks;

public class LineDigestTests
{
    private static LineDigest Of(params string[] lines)
    {
        LineDigest digest = default;
        foreach (string line in lines)
        {
            digest = digest.Add(Encoding.UTF8.GetBytes(line));
        }

        return digest;
    }

    [Fact]
    public void An_empty_set_has_a_digest_of_zeros()
    {
        Assert.Equal(new LineDigest(0, 0, UInt128.Zero), Of());
    }

    [Fact]
    public void A_line_adds_itself_its_bytes_and_its_hash()
    {
        LineDigest digest = Of("415. Apple", "1. Ёлка");

        Assert.Equal(2, digest.Lines);
        Assert.Equal(10 + Encoding.UTF8.GetByteCount("1. Ёлка"), digest.Bytes);
        Assert.Equal(
            XxHash128.HashToUInt128("415. Apple"u8) + XxHash128.HashToUInt128(Encoding.UTF8.GetBytes("1. Ёлка")),
            digest.HashSum);
    }

    [Fact]
    public void The_digest_does_not_depend_on_the_order_of_the_lines()
    {
        string[] lines = [.. Enumerable.Range(0, 1000).Select(i => $"{i % 37}. Line {i % 101}")];
        string[] shuffled = [.. lines];
        new Random(1).Shuffle(shuffled);

        Assert.NotEqual(lines, shuffled);
        Assert.Equal(Of(lines), Of(shuffled));
        Assert.Equal(Of(lines), Of([.. lines.Reverse()]));
    }

    [Fact]
    public void A_pair_of_identical_lines_replaced_by_a_pair_of_another_line_changes_the_digest()
    {
        // Two lines of the same length, so only the hashes can tell. Under an exclusive or both sets would give
        // the hash of "2. Apple": the pair of "1. Apple" cancels itself, and so does a pair of "2. Apple".
        LineDigest before = Of("1. Apple", "1. Apple", "2. Apple");
        LineDigest after = Of("2. Apple", "2. Apple", "2. Apple");

        Assert.Equal(before.Lines, after.Lines);
        Assert.Equal(before.Bytes, after.Bytes);
        Assert.NotEqual(before.HashSum, after.HashSum);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void A_line_that_occurs_several_times_counts_every_time()
    {
        UInt128 hash = XxHash128.HashToUInt128("7. Kiwi"u8);

        Assert.Equal(hash + hash + hash, Of("7. Kiwi", "7. Kiwi", "7. Kiwi").HashSum);
        Assert.NotEqual(Of("7. Kiwi"), Of("7. Kiwi", "7. Kiwi"));
    }

    [Fact]
    public void Lines_with_equal_keys_and_different_bytes_are_different_lines()
    {
        Assert.NotEqual(Of("007. Apple").HashSum, Of("7. Apple").HashSum);
    }

    [Fact]
    public void One_changed_byte_changes_the_digest()
    {
        Assert.NotEqual(Of("1. Apple", "2. Banana"), Of("1. Apple", "2. Banaba"));
    }

    [Fact]
    public void The_sum_wraps_around_at_2_to_the_128()
    {
        UInt128 hash = XxHash128.HashToUInt128("1. Apple"u8);
        LineDigest almostFull = new(0, 0, UInt128.MaxValue);

        // The largest value plus a hash is that hash minus one, modulo 2^128, and no overflow is raised.
        Assert.Equal(hash - 1, almostFull.Add("1. Apple"u8).HashSum);
    }

    [Fact]
    public void The_terminator_is_not_part_of_a_line()
    {
        // The reader gives lines without terminators; a line given with one is another line.
        Assert.NotEqual(Of("1. Apple"), Of("1. Apple\n"));
    }
}
