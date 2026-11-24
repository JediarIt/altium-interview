using FileSort.Engine.Blocks;

namespace FileSort.Tests.Engine.Blocks;

/// <summary>The window of the block buffers: a ring, reserved and freed in order, never holding more than it
/// has.</summary>
public class BlockWindowTests
{
    // A window of 1100 bytes with 100 set aside: a ring of 1000 bytes, which buffers blocks of up to 250.
    private static BlockWindow Window() => new(1100, 100);

    [Fact]
    public void The_ring_and_the_piece_buffer_share_the_window()
    {
        BlockWindow window = Window();

        Assert.Equal(1000, window.RingLength);
        Assert.Equal(1000, window.PieceOffset);
        Assert.Equal(100, window.PieceLength);
        Assert.Equal(250, window.LargestBuffered);
    }

    [Fact]
    public void Buffers_follow_each_other_and_wrap_to_the_start()
    {
        BlockWindow window = Window();

        Assert.True(window.TryReserve(250, out int first));
        Assert.True(window.TryReserve(250, out int second));
        Assert.True(window.TryReserve(250, out int third));
        Assert.True(window.TryReserve(200, out int fourth));
        Assert.Equal([0, 250, 500, 750], [first, second, third, fourth]);

        // 50 bytes are left at the end, and the start is taken: no room for 100.
        Assert.False(window.TryReserve(100, out _));

        window.Free(first);
        Assert.True(window.TryReserve(100, out int wrapped));
        Assert.Equal(0, wrapped);

        // After the wrap the room is between the newest and the oldest buffer.
        Assert.True(window.TryReserve(150, out int next));
        Assert.Equal(100, next);
        Assert.False(window.TryReserve(1, out _));
        Assert.Equal(950, window.MostBytesInFlight);
    }

    [Fact]
    public void An_empty_ring_starts_again_at_its_start()
    {
        BlockWindow window = Window();
        window.TryReserve(200, out int first);
        window.TryReserve(200, out int second);

        window.Free(first);
        window.Free(second);

        Assert.True(window.TryReserve(250, out int offset));
        Assert.Equal(0, offset);
    }

    [Fact]
    public void Buffers_are_freed_in_the_order_they_were_reserved()
    {
        BlockWindow window = Window();
        window.TryReserve(100, out _);
        window.TryReserve(100, out int second);

        Assert.Throws<InvalidOperationException>(() => window.Free(second));
    }

    [Fact]
    public void A_buffer_larger_than_a_quarter_of_the_ring_or_empty_is_refused()
    {
        BlockWindow window = Window();

        Assert.Throws<ArgumentOutOfRangeException>(() => window.TryReserve(251, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.TryReserve(0, out _));
    }
}
