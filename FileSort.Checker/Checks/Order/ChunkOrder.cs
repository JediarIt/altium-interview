namespace FileSort.Checker.Checks.Order;

/// <summary>
/// The order check of one chunk of a file: the counts, and the first violations, with line numbers that count from
/// 1 for the first line of the chunk. The first line of the chunk is compared with the last line of the chunk before
/// it, so these are all the violations of the chunk's lines.
/// </summary>
internal sealed record ChunkOrder(OrderResult Result, IReadOnlyList<OrderViolation> Violations);
