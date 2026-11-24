namespace FileSort.Tests;

/// <summary>
/// The collection of the tests that measure how fast something reacts, such as a sort that must stop within two
/// seconds of a cancellation. They run after all other tests and one at a time: tests that run in parallel slow them
/// down by several times, which would measure the other tests.
/// </summary>
[CollectionDefinition(nameof(TimedTests), DisableParallelization = true)]
public sealed class TimedTests;