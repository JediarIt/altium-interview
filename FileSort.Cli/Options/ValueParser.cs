using System.Diagnostics.CodeAnalysis;

namespace FileSort.Cli.Options;

/// <summary>
/// Reads the text of one value of an option. On failure it returns false with a message that quotes the text; the
/// name of the option is added where the error is printed, by <see cref="CommandRunner"/>.
/// </summary>
public delegate bool ValueParser<T>(string text, out T value, [NotNullWhen(false)] out string? error);
