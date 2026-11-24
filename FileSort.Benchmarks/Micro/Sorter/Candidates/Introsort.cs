using System.Numerics;
using System.Runtime.CompilerServices;

namespace FileSort.Benchmarks.Micro.Sorter.Candidates;

/// <summary>
/// An introspective sort, generic over a comparer that is a struct, so that the JIT compiles it once per comparer and
/// inlines the comparison: the hand-written candidate of D7 of the design for the sort of a segment. The framework's
/// sort calls a comparison delegate on every comparison, as the comparison of index entries needs the lines. M1
/// measured this sort slower than the framework's sort of elements that compare themselves, so the engine does not
/// use it; the benchmarks keep it as the candidate that was measured.
/// </summary>
/// <remarks>
/// The algorithm is the textbook one, as the framework uses it too: quicksort with the median of three as the pivot;
/// insertion sort for partitions of at most 16 elements; heapsort for a partition once the recursion is deeper than
/// twice the logarithm of the length, so that no input takes quadratic time. It is not stable, which the sorter does
/// not need: elements that compare equal are identical lines.
/// </remarks>
internal static class Introsort
{
    private const int InsertionSortThreshold = 16;

    public static void Sort<T, TComparer>(Span<T> items, TComparer comparer)
        where TComparer : IComparer<T>
    {
        if (items.Length > 1)
        {
            SortPartition(items, DepthLimit(items.Length), comparer);
        }
    }

    // Twice the number of times the length can be halved: 2 × 21 = 42 levels for a million elements.
    private static int DepthLimit(int length)
    {
        return 2 * (BitOperations.Log2((uint)length) + 1);
    }

    // Sorts the smaller side by recursion and loops on the larger one, so the stack stays shallow.
    private static void SortPartition<T, TComparer>(Span<T> items, int depthLimit, TComparer comparer)
        where TComparer : IComparer<T>
    {
        while (items.Length > InsertionSortThreshold)
        {
            if (depthLimit == 0)
            {
                HeapSort(items, comparer);
                return;
            }

            depthLimit--;
            int pivot = Partition(items, comparer);
            Span<T> left = items[..pivot];
            Span<T> right = items[(pivot + 1)..];
            if (left.Length < right.Length)
            {
                SortPartition(left, depthLimit, comparer);
                items = right;
            }
            else
            {
                SortPartition(right, depthLimit, comparer);
                items = left;
            }
        }

        InsertionSort(items, comparer);
    }

    // Puts the median of the first, middle and last element at the end, then every element below it to its left.
    // Returns the final place of the pivot: the elements before it are not greater, the elements after it not less.
    private static int Partition<T, TComparer>(Span<T> items, TComparer comparer)
        where TComparer : IComparer<T>
    {
        int last = items.Length - 1;
        int middle = last / 2;
        SwapIfGreater(items, 0, middle, comparer);
        SwapIfGreater(items, 0, last, comparer);
        SwapIfGreater(items, middle, last, comparer);

        // The median is now in the middle; it moves next to the end, and the last element, not less than it, stays.
        int pivotIndex = last - 1;
        Swap(items, middle, pivotIndex);
        T pivot = items[pivotIndex];

        int left = 0;
        int right = pivotIndex;
        while (left < right)
        {
            while (comparer.Compare(items[++left], pivot) < 0)
            {
            }

            while (comparer.Compare(pivot, items[--right]) < 0)
            {
            }

            if (left >= right)
            {
                break;
            }

            Swap(items, left, right);
        }

        if (left != pivotIndex)
        {
            Swap(items, left, pivotIndex);
        }

        return left;
    }

    private static void InsertionSort<T, TComparer>(Span<T> items, TComparer comparer)
        where TComparer : IComparer<T>
    {
        for (int i = 1; i < items.Length; i++)
        {
            T item = items[i];
            int j = i - 1;
            while (j >= 0 && comparer.Compare(item, items[j]) < 0)
            {
                items[j + 1] = items[j];
                j--;
            }

            items[j + 1] = item;
        }
    }

    private static void HeapSort<T, TComparer>(Span<T> items, TComparer comparer)
        where TComparer : IComparer<T>
    {
        int length = items.Length;
        for (int i = (length / 2) - 1; i >= 0; i--)
        {
            SiftDown(items, i, length, comparer);
        }

        for (int end = length - 1; end > 0; end--)
        {
            Swap(items, 0, end);
            SiftDown(items, 0, end, comparer);
        }
    }

    // Moves the element at the root down the heap of the first length elements, whose children of i are 2i + 1 and
    // 2i + 2, until no child is greater.
    private static void SiftDown<T, TComparer>(Span<T> items, int root, int length, TComparer comparer)
        where TComparer : IComparer<T>
    {
        T item = items[root];
        while (true)
        {
            int child = (2 * root) + 1;
            if (child >= length)
            {
                break;
            }

            if (child + 1 < length && comparer.Compare(items[child], items[child + 1]) < 0)
            {
                child++;
            }

            if (comparer.Compare(item, items[child]) >= 0)
            {
                break;
            }

            items[root] = items[child];
            root = child;
        }

        items[root] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapIfGreater<T, TComparer>(Span<T> items, int i, int j, TComparer comparer)
        where TComparer : IComparer<T>
    {
        if (comparer.Compare(items[i], items[j]) > 0)
        {
            Swap(items, i, j);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap<T>(Span<T> items, int i, int j)
    {
        (items[i], items[j]) = (items[j], items[i]);
    }
}
