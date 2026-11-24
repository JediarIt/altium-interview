using FileSort.Engine.Lines;

namespace FileSort.Engine.Merging;

/// <summary>
/// A tournament of sorted sources that finds the least of their current lines with about log2(k) comparisons for k
/// sources: each inner node keeps the loser of the match played there, so when the winner's source moves on, only
/// the matches on the path from that source to the top are played again. A binary heap needs about twice as many
/// comparisons. The current line of each source is its head, an index entry whose line lies in the buffer of the
/// comparer.
/// </summary>
/// <remarks>
/// The k sources are the leaves of a binary tree laid out in an array, as in a heap: the inner nodes are 1 to k - 1,
/// node n has the children 2n and 2n + 1, and the leaf of source i stands at k + i. Node 0 holds the winner of the
/// whole tournament. A source that has no more lines loses every match.
/// </remarks>
internal sealed class LoserTree
{
    private readonly int[] _nodes;
    private readonly IndexEntry[] _heads;
    private readonly bool[] _exhausted;
    private readonly EntryComparer _comparer;

    public LoserTree(int sources, EntryComparer comparer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sources, 1);
        _nodes = new int[sources];
        _heads = new IndexEntry[sources];
        _exhausted = new bool[sources];
        _comparer = comparer;
    }

    public int Sources => _heads.Length;

    /// <summary>The source whose head comes first.</summary>
    public int Winner => _nodes[0];

    /// <summary>The head of the winner: the next line in the merged order.</summary>
    public ref readonly IndexEntry WinnerHead => ref _heads[_nodes[0]];

    /// <summary>Every source has run out: the winner itself has no head.</summary>
    public bool Done => _exhausted[_nodes[0]];

    /// <summary>Sets the first head of a source before <see cref="Start"/>.</summary>
    public void SetHead(int source, in IndexEntry head)
    {
        _heads[source] = head;
        _exhausted[source] = false;
    }

    /// <summary>Marks a source that has no lines at all, before <see cref="Start"/>.</summary>
    public void SetEmpty(int source)
    {
        _exhausted[source] = true;
    }

    /// <summary>Plays the whole tournament once every source has its first head or is marked empty.</summary>
    public void Start()
    {
        _nodes[0] = Play(1);
    }

    /// <summary>The winner's source moves on to its next head; the matches on its path are played again.</summary>
    public void Advance(in IndexEntry next)
    {
        int winner = _nodes[0];
        _heads[winner] = next;
        Replay(winner);
    }

    /// <summary>The winner's source has run out; the matches on its path are played again.</summary>
    public void Exhaust()
    {
        int winner = _nodes[0];
        _exhausted[winner] = true;
        Replay(winner);
    }

    // Plays the subtree under a node and returns its winner, keeping the loser of every inner node. A node at or
    // beyond the count of sources is the leaf of source node - k.
    private int Play(int node)
    {
        int sources = _heads.Length;
        if (node >= sources)
        {
            return node - sources;
        }

        int left = Play(2 * node);
        int right = Play((2 * node) + 1);
        bool leftWins = Before(left, right);
        _nodes[node] = leftWins ? right : left;
        return leftWins ? left : right;
    }

    // From the parent of the source's leaf up to the top: where the loser kept at a node beats the climbing winner,
    // the two swap places.
    private void Replay(int source)
    {
        int winner = source;
        for (int node = (source + _heads.Length) / 2; node > 0; node /= 2)
        {
            int loser = _nodes[node];
            if (Before(loser, winner))
            {
                _nodes[node] = winner;
                winner = loser;
            }
        }

        _nodes[0] = winner;
    }

    // Whether the head of source x comes before the head of source y. A source without a head comes after all; of
    // equal heads, which are identical lines, neither comes first.
    private bool Before(int x, int y)
    {
        if (_exhausted[x])
        {
            return false;
        }

        return _exhausted[y] || _comparer.Compare(in _heads[x], in _heads[y]) < 0;
    }
}
