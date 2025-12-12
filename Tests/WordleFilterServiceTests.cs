using WordleHelper.Services;
using static WordleHelper.Services.WordleFilterService;
using Xunit.Abstractions;

namespace WordleHelper.Tests;

public class WordleFilterServiceTests
{
    private readonly WordleFilterService _service;
    private readonly ITestOutputHelper _testOutputHelper;

    public WordleFilterServiceTests(ITestOutputHelper testOutputHelper)
    {
        _service = new WordleFilterService();
        _testOutputHelper = testOutputHelper;
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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act & Assert
        Assert.True(_service.MatchesGuess("crane", guess)); // doesn't have 'x'
        Assert.False(_service.MatchesGuess("taxed", guess)); // has 'x'

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: 2");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F6}s)");
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

    [Fact]
    public void MatchesGuess_ERASE_WithYellowGreenWhiteE_MatchesGUESS()
    {
        // Arrange - Guess "ERASE": yellow E, white R, white A, green S, white E
        var guess = new Guess(
            new[] { 'e', 'r', 'a', 's', 'e' },
            new[] { LetterState.Yellow, LetterState.White, LetterState.White, LetterState.Green, LetterState.White }
        );

        // Act & Assert
        // GUESS: has exactly 1 E (at position 2, not at position 0), S at position 3, no R or A
        Assert.True(_service.MatchesGuess("guess", guess));
    }

    [Fact]
    public void MatchesGuess_WhiteLetter_MustNotBeInWord_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'x', ' ', ' ', ' ', ' ' },
            new[] { LetterState.White, LetterState.None, LetterState.None, LetterState.None, LetterState.None }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Test all words
        var wordsWithX = allWords.Where(w => w.Contains('x')).ToList();
        var wordsWithoutX = allWords.Where(w => !w.Contains('x')).ToList();

        // Assert
        foreach (var word in wordsWithoutX)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' without 'x' should match");
        }

        foreach (var word in wordsWithX)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' with 'x' should not match");
        }

        stopwatch.Stop();

        // Output performance metrics
        var totalWords = allWords.Length;
        var wordsWithXCount = wordsWithX.Count;
        var wordsWithoutXCount = wordsWithoutX.Count;

        _testOutputHelper?.WriteLine($"Total words tested: {totalWords}");
        _testOutputHelper?.WriteLine($"Words with 'x': {wordsWithXCount}");
        _testOutputHelper?.WriteLine($"Words without 'x': {wordsWithoutXCount}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void MatchesGuess_YellowLetter_MustBeInWordButNotAtPosition_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'e', ' ', ' ', ' ', ' ' },
            new[] { LetterState.Yellow, LetterState.None, LetterState.None, LetterState.None, LetterState.None }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Categorize words
        var wordsWithENotAtPos0 = allWords.Where(w => w.Contains('e') && w[0] != 'e').ToList();
        var wordsWithEAtPos0 = allWords.Where(w => w.Length > 0 && w[0] == 'e').ToList();
        var wordsWithoutE = allWords.Where(w => !w.Contains('e')).ToList();

        // Assert
        foreach (var word in wordsWithENotAtPos0)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' has 'e' not at position 0, should match");
        }

        foreach (var word in wordsWithEAtPos0)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has 'e' at position 0, should not match");
        }

        foreach (var word in wordsWithoutE)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' without 'e', should not match");
        }

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Words with 'e' not at position 0: {wordsWithENotAtPos0.Count}");
        _testOutputHelper?.WriteLine($"Words with 'e' at position 0: {wordsWithEAtPos0.Count}");
        _testOutputHelper?.WriteLine($"Words without 'e': {wordsWithoutE.Count}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void MatchesGuess_TwoYellowSameLetter_RequiresAtLeastTwo_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'e', 'e', ' ', ' ', ' ' },
            new[] { LetterState.Yellow, LetterState.Yellow, LetterState.None, LetterState.None, LetterState.None }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Categorize words
        var wordsWith2OrMoreE = allWords.Where(w => w.Count(c => c == 'e') >= 2 && w[0] != 'e' && w[1] != 'e').ToList();
        var wordsWith1E = allWords.Where(w => w.Count(c => c == 'e') == 1).ToList();
        var wordsWithEAtWrongPos = allWords.Where(w => w.Count(c => c == 'e') >= 2 && (w[0] == 'e' || w[1] == 'e')).ToList();

        // Assert
        foreach (var word in wordsWith2OrMoreE)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' has 2+ 'e's not at positions 0 or 1, should match");
        }

        foreach (var word in wordsWith1E)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has only 1 'e', should not match");
        }

        foreach (var word in wordsWithEAtWrongPos)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has 'e' at position 0 or 1, should not match");
        }

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Words with 2+ 'e's (valid positions): {wordsWith2OrMoreE.Count}");
        _testOutputHelper?.WriteLine($"Words with only 1 'e': {wordsWith1E.Count}");
        _testOutputHelper?.WriteLine($"Words with 'e' at wrong positions: {wordsWithEAtWrongPos.Count}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void MatchesGuess_TwoWhiteSameLetter_RequiresZero_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'e', 'e', ' ', ' ', ' ' },
            new[] { LetterState.White, LetterState.White, LetterState.None, LetterState.None, LetterState.None }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Categorize words
        var wordsWithoutE = allWords.Where(w => !w.Contains('e')).ToList();
        var wordsWithE = allWords.Where(w => w.Contains('e')).ToList();

        // Assert
        foreach (var word in wordsWithoutE)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' has 0 'e's, should match");
        }

        foreach (var word in wordsWithE)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has 'e', should not match");
        }

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Words without 'e': {wordsWithoutE.Count}");
        _testOutputHelper?.WriteLine($"Words with 'e': {wordsWithE.Count}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void MatchesGuess_OneYellowOneWhite_RequiresExactlyOne_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'e', 'e', ' ', ' ', ' ' },
            new[] { LetterState.Yellow, LetterState.White, LetterState.None, LetterState.None, LetterState.None }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Categorize words
        var wordsWithExactly1ENotAtPos0 = allWords.Where(w => w.Count(c => c == 'e') == 1 && w[0] != 'e').ToList();
        var wordsWithNoE = allWords.Where(w => w.Count(c => c == 'e') == 0).ToList();
        var wordsWith2OrMoreE = allWords.Where(w => w.Count(c => c == 'e') >= 2).ToList();
        var wordsWithEAtPos0 = allWords.Where(w => w.Count(c => c == 'e') == 1 && w[0] == 'e').ToList();

        // Assert
        foreach (var word in wordsWithExactly1ENotAtPos0)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' has exactly 1 'e' not at position 0, should match");
        }

        foreach (var word in wordsWithNoE)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has 0 'e's, should not match");
        }

        foreach (var word in wordsWith2OrMoreE)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has 2+ 'e's, should not match");
        }

        foreach (var word in wordsWithEAtPos0)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has 'e' at position 0, should not match");
        }

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Words with exactly 1 'e' (not at pos 0): {wordsWithExactly1ENotAtPos0.Count}");
        _testOutputHelper?.WriteLine($"Words with 0 'e's: {wordsWithNoE.Count}");
        _testOutputHelper?.WriteLine($"Words with 2+ 'e's: {wordsWith2OrMoreE.Count}");
        _testOutputHelper?.WriteLine($"Words with 'e' at position 0: {wordsWithEAtPos0.Count}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void MatchesGuess_OneYellowOneGreen_RequiresAtLeastTwo_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'e', ' ', ' ', ' ', 'e' },
            new[] { LetterState.Yellow, LetterState.None, LetterState.None, LetterState.None, LetterState.Green }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Categorize words
        var validWords = allWords.Where(w =>
            w.Length >= 5 &&
            w[4] == 'e' &&
            w.Count(c => c == 'e') >= 2 &&
            w[0] != 'e').ToList();

        var wordsWithOnly1E = allWords.Where(w => w.Count(c => c == 'e') == 1).ToList();
        var wordsWithoutEAtPos4 = allWords.Where(w => w.Length < 5 || w[4] != 'e').ToList();
        var wordsWithEAtPos0 = allWords.Where(w => w.Length >= 5 && w[4] == 'e' && w[0] == 'e').ToList();

        // Assert
        foreach (var word in validWords)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' has 2+ 'e's with one at position 4 and not at position 0, should match");
        }

        foreach (var word in wordsWithOnly1E)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has only 1 'e', should not match");
        }

        foreach (var word in wordsWithoutEAtPos4.Where(w => w.Count(c => c == 'e') >= 2))
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' doesn't have 'e' at position 4, should not match");
        }

        foreach (var word in wordsWithEAtPos0)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has 'e' at position 0, should not match");
        }

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Valid words: {validWords.Count}");
        _testOutputHelper?.WriteLine($"Words with only 1 'e': {wordsWithOnly1E.Count}");
        _testOutputHelper?.WriteLine($"Words without 'e' at position 4: {wordsWithoutEAtPos4.Count}");
        _testOutputHelper?.WriteLine($"Words with 'e' at position 0: {wordsWithEAtPos0.Count}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void MatchesGuess_OneGreenOneYellowOneWhite_RequiresExactlyTwo_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'e', ' ', 'e', ' ', 'e' },
            new[] { LetterState.Yellow, LetterState.None, LetterState.Green, LetterState.None, LetterState.White }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Categorize words
        var validWords = allWords.Where(w =>
            w.Length >= 5 &&
            w[2] == 'e' &&
            w.Count(c => c == 'e') == 2 &&
            w[0] != 'e').ToList();

        var wordsWithWrongECount = allWords.Where(w => w.Count(c => c == 'e') != 2).ToList();
        var wordsWithoutEAtPos2 = allWords.Where(w => w.Length < 3 || w[2] != 'e').ToList();
        var wordsWithEAtPos0 = allWords.Where(w => w.Length >= 5 && w[2] == 'e' && w.Count(c => c == 'e') == 2 && w[0] == 'e').ToList();

        // Assert
        foreach (var word in validWords)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' has exactly 2 'e's with one at position 2 and not at position 0, should match");
        }

        foreach (var word in wordsWithWrongECount)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' doesn't have exactly 2 'e's, should not match");
        }

        foreach (var word in wordsWithoutEAtPos2.Where(w => w.Count(c => c == 'e') == 2))
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' doesn't have 'e' at position 2, should not match");
        }

        foreach (var word in wordsWithEAtPos0)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' has 'e' at position 0, should not match");
        }

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Valid words: {validWords.Count}");
        _testOutputHelper?.WriteLine($"Words with wrong 'e' count: {wordsWithWrongECount.Count}");
        _testOutputHelper?.WriteLine($"Words without 'e' at position 2: {wordsWithoutEAtPos2.Count}");
        _testOutputHelper?.WriteLine($"Words with 'e' at position 0: {wordsWithEAtPos0.Count}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void MatchesGuess_GreenLetter_MustBeInExactPosition_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'c', 'r', 'a', 'n', 'e' },
            new[] { LetterState.None, LetterState.None, LetterState.Green, LetterState.None, LetterState.None }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Categorize words
        var wordsWithAAtPos2 = allWords.Where(w => w.Length >= 3 && w[2] == 'a').ToList();
        var wordsWithoutAAtPos2 = allWords.Where(w => w.Length < 3 || w[2] != 'a').ToList();

        // Assert
        foreach (var word in wordsWithAAtPos2)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' has 'a' at position 2, should match");
        }

        foreach (var word in wordsWithoutAAtPos2)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' doesn't have 'a' at position 2, should not match");
        }

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Words with 'a' at position 2: {wordsWithAAtPos2.Count}");
        _testOutputHelper?.WriteLine($"Words without 'a' at position 2: {wordsWithoutAAtPos2.Count}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void MatchesGuess_CaseInsensitive_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guess = new Guess(
            new[] { 'C', 'R', 'A', 'N', 'E' },
            new[] { LetterState.Green, LetterState.Green, LetterState.Green, LetterState.Green, LetterState.Green }
        );

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Categorize words
        var craneWords = allWords.Where(w => w.Equals("crane", StringComparison.OrdinalIgnoreCase)).ToList();
        var nonCraneWords = allWords.Where(w => !w.Equals("crane", StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert - Test all words with uppercase guess
        foreach (var word in craneWords)
        {
            Assert.True(_service.MatchesGuess(word, guess), $"Word '{word}' should match uppercase guess 'CRANE'");
        }

        foreach (var word in nonCraneWords)
        {
            Assert.False(_service.MatchesGuess(word, guess), $"Word '{word}' should not match guess 'CRANE'");
        }

        stopwatch.Stop();

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Words matching 'CRANE': {craneWords.Count}");
        _testOutputHelper?.WriteLine($"Words not matching: {nonCraneWords.Count}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }

    [Fact]
    public void FilterWords_MultipleGuesses_AppliesAllConstraints_AllWords()
    {
        // Arrange
        var allWords = File.ReadAllLines(Path.Combine("..", "..", "..", "..", "wwwroot", "words.txt"));
        var guesses = new List<Guess>
        {
            new Guess(
                new[] { 'c', 'r', 'a', 'n', 'e' },
                new[] { LetterState.White, LetterState.Green, LetterState.White, LetterState.Yellow, LetterState.Yellow }
            )
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = _service.FilterWords(allWords, guesses).ToList();

        // Expected constraints:
        // - Must have 'r' at position 1
        // - Must have 'n' in word but not at position 3
        // - Must have 'e' in word but not at position 4
        // - Must not have 'c'
        // - Must not have 'a'
        var expectedMatches = allWords.Where(w =>
            w.Length >= 5 &&
            w[1] == 'r' &&
            w.Contains('n') && w[3] != 'n' &&
            w.Contains('e') && w[4] != 'e' &&
            !w.Contains('c') &&
            !w.Contains('a')).ToList();

        stopwatch.Stop();

        // Assert - Verify the filtered results match our expectations
        Assert.Equal(expectedMatches.Count, result.Count);
        foreach (var word in expectedMatches)
        {
            Assert.Contains(word, result);
        }

        // Output performance metrics
        _testOutputHelper?.WriteLine($"Total words tested: {allWords.Length}");
        _testOutputHelper?.WriteLine($"Words matching all constraints: {result.Count}");
        _testOutputHelper?.WriteLine($"Sample matches: {string.Join(", ", result.Take(10))}");
        _testOutputHelper?.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
    }
}
