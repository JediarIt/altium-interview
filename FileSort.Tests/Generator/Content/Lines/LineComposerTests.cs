using FileSort.Common;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Lines;
using FileSort.Generator.Random;

namespace FileSort.Tests.Generator.Content.Lines;

/// <summary>What the composer of a line decides for the line as a whole.</summary>
public class LineComposerTests
{
    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public void The_first_line_of_a_file_drops_the_carriage_return_it_rolled(LineEnding lineEnding)
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.CarriageReturn, lineEnding: lineEnding);
        LineComposer composer = new(options);
        Xoshiro256 random = RandomThatRollsACarriageReturn();
        byte[] first = new byte[composer.MaxLineLength + StringComposer.Slack];
        byte[] other = new byte[composer.MaxLineLength + StringComposer.Slack];

        // Two copies of the same random state: the same line, once as the first line of a file.
        Xoshiro256 copy = random;
        int firstLength = composer.WriteLine(ref random, first, firstLineOfFile: true);
        int otherLength = composer.WriteLine(ref copy, other, firstLineOfFile: false);

        int terminator = lineEnding.TerminatorLength();
        Assert.Equal(firstLength + 1, otherLength);
        Assert.Equal(first[..(firstLength - terminator)], other[..(firstLength - terminator)]);
        Assert.Equal((byte)'\r', other[otherLength - terminator - 1]);

        // Both lines took the same random values: dropping the carriage return changes nothing that follows.
        Assert.Equal(random.Next(), copy.Next());
    }

    // The first state, by seed, whose line rolls the category carriage-return: the roll is the second random value.
    private static Xoshiro256 RandomThatRollsACarriageReturn()
    {
        for (ulong seed = 1; ; seed++)
        {
            Xoshiro256 random = Xoshiro256.Create(seed, 0);
            Xoshiro256 probe = random;
            probe.Next();
            if (EdgeRoll.Apply(probe.Next(), EdgeCases.CarriageReturn) != EdgeCases.None)
            {
                return random;
            }
        }
    }
}
