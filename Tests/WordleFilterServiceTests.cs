using WordleHelper.Services;
using static WordleHelper.Services.WordleFilterService;

namespace WordleHelper.Tests;

public class WordleFilterServiceTests
{
    private readonly WordleFilterService _service;

    public WordleFilterServiceTests()
    {
        _service = new WordleFilterService();
    }

    [Fact]
    public void MatchesGuess_GreenLetter_MustBeInExactPosition()
    {
        // Arrange
        var guess = new Guess(
            new[] { 'c', 'r', 'a', 'n', 'e' },
            new[] { LetterState.None, LetterState.None, LetterState.Green, LetterState.None, LetterState.None }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("crane", guess));
        Assert.True(_service.MatchesGuess("bravo", guess)); // 'a' at position 2
        Assert.False(_service.MatchesGuess("crone", guess)); // 'o' at position 2, not 'a'
    }

    [Fact]
    public void MatchesGuess_YellowLetter_MustBeInWordButNotAtPosition()
    {
        // Arrange
        var guess = new Guess(
            new[] { 'e', ' ', ' ', ' ', ' ' },
            new[] { LetterState.Yellow, LetterState.None, LetterState.None, LetterState.None, LetterState.None }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("crane", guess)); // has 'e' at position 4
        Assert.False(_service.MatchesGuess("erase", guess)); // has 'e' but at position 0 (same as guess)
        Assert.False(_service.MatchesGuess("track", guess)); // doesn't have 'e'
    }

    [Fact]
    public void MatchesGuess_WhiteLetter_MustNotBeInWord()
    {
        // Arrange
        var guess = new Guess(
            new[] { 'x', ' ', ' ', ' ', ' ' },
            new[] { LetterState.White, LetterState.None, LetterState.None, LetterState.None, LetterState.None }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("crane", guess)); // doesn't have 'x'
        Assert.False(_service.MatchesGuess("taxed", guess)); // has 'x'
    }

    [Fact]
    public void MatchesGuess_TwoYellowSameLetter_RequiresAtLeastTwo()
    {
        // Arrange - two yellow E's
        var guess = new Guess(
            new[] { 'e', 'e', ' ', ' ', ' ' },
            new[] { LetterState.Yellow, LetterState.Yellow, LetterState.None, LetterState.None, LetterState.None }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("creep", guess)); // has 2 E's (satisfies "at least 2")
        Assert.True(_service.MatchesGuess("siege", guess)); // has exactly 2 E's
        Assert.False(_service.MatchesGuess("crane", guess)); // has only 1 E
    }

    [Fact]
    public void MatchesGuess_OneYellowOneGreen_RequiresAtLeastTwo()
    {
        // Arrange - yellow E at position 0, green E at position 4
        var guess = new Guess(
            new[] { 'e', ' ', ' ', ' ', 'e' },
            new[] { LetterState.Yellow, LetterState.None, LetterState.None, LetterState.None, LetterState.Green }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("crepe", guess)); // has 2 E's, one at position 4
        Assert.False(_service.MatchesGuess("broke", guess)); // has only 1 E at position 4
        Assert.False(_service.MatchesGuess("erase", guess)); // has E at position 0 (conflicts with yellow constraint)
    }

    [Fact]
    public void MatchesGuess_OneYellowOneWhite_RequiresExactlyOne()
    {
        // Arrange - yellow E at position 0, white E at position 1
        var guess = new Guess(
            new[] { 'e', 'e', ' ', ' ', ' ' },
            new[] { LetterState.Yellow, LetterState.White, LetterState.None, LetterState.None, LetterState.None }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("brake", guess)); // has exactly 1 E at position 4
        Assert.False(_service.MatchesGuess("creep", guess)); // has 2 E's (too many)
        Assert.False(_service.MatchesGuess("track", guess)); // has 0 E's (too few)
    }

    [Fact]
    public void MatchesGuess_TwoWhiteSameLetter_RequiresZero()
    {
        // Arrange - two white E's
        var guess = new Guess(
            new[] { 'e', 'e', ' ', ' ', ' ' },
            new[] { LetterState.White, LetterState.White, LetterState.None, LetterState.None, LetterState.None }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("track", guess)); // has 0 E's
        Assert.False(_service.MatchesGuess("crane", guess)); // has 1 E
    }

    [Fact]
    public void MatchesGuess_OneGreenOneYellowOneWhite_RequiresExactlyTwo()
    {
        // Arrange - green E at position 2, yellow E at position 0, white E at position 4
        var guess = new Guess(
            new[] { 'e', ' ', 'e', ' ', 'e' },
            new[] { LetterState.Yellow, LetterState.None, LetterState.Green, LetterState.None, LetterState.White }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("sweet", guess)); // has exactly 2 E's (positions 2 and 3), not at position 0
        Assert.False(_service.MatchesGuess("eerie", guess)); // has 3 E's (too many) and E at position 0
        Assert.False(_service.MatchesGuess("exude", guess)); // has E at position 0 (violates yellow) and no E at position 2 (violates green)
    }

    [Fact]
    public void FilterWords_MultipleGuesses_AppliesAllConstraints()
    {
        // Arrange
        var allWords = new[] { "crane", "green", "shine", "track", "brake" };
        var guesses = new List<Guess>
        {
            new Guess(
                new[] { 'c', 'r', 'a', 'n', 'e' },
                new[] { LetterState.White, LetterState.Green, LetterState.White, LetterState.Yellow, LetterState.Yellow }
            )
        };

        // Act
        var result = _service.FilterWords(allWords, guesses).ToList();

        // Assert
        // Must have: 'r' at position 1, 'n' in word but not at position 3, 'e' in word but not at position 4
        // Must not have: 'c', 'a'
        Assert.Contains("green", result); // r at pos 1, has n at pos 4 and e at pos 2/3, no c or a
        Assert.DoesNotContain("crane", result); // has 'c' and 'a'
        Assert.DoesNotContain("track", result); // has 'c' and 'a'
        Assert.DoesNotContain("shine", result); // no 'r' at position 1
    }

    [Fact]
    public void MatchesGuess_CaseInsensitive()
    {
        // Arrange
        var guess = new Guess(
            new[] { 'C', 'R', 'A', 'N', 'E' },
            new[] { LetterState.Green, LetterState.Green, LetterState.Green, LetterState.Green, LetterState.Green }
        );

        // Act & Assert
        Assert.True(_service.MatchesGuess("crane", guess)); // lowercase word matches uppercase guess
    }
}
