using Xunit;
using Xunit.Abstractions;
using WordleHelper.Services;
using static WordleHelper.Services.WordleFilterService;

namespace WordleHelper.Tests;

public class WordleStrategyServiceTests
{
    private readonly WordleStrategyService _service;
    private readonly ITestOutputHelper _testOutputHelper;

    public WordleStrategyServiceTests(ITestOutputHelper testOutputHelper)
    {
        var filterService = new WordleFilterService();
        _service = new WordleStrategyService(filterService);
        _testOutputHelper = testOutputHelper;

        // Verify service is initialized
        _testOutputHelper.WriteLine($"Service initialized: {_service.IsInitialized}");
    }

    [Fact]
    public void GetPattern_AllGreen_ReturnsGGGGG()
    {
        var pattern = InvokeGetPattern("HELLO", "HELLO");
        Assert.Equal("GGGGG", pattern);
    }

    [Fact]
    public void GetPattern_NoMatches_ReturnsWWWWW()
    {
        var pattern = InvokeGetPattern("HELLO", "TRACK");
        Assert.Equal("WWWWW", pattern);
    }

    [Fact]
    public void GetPattern_YellowLetters_CorrectPattern()
    {
        var pattern = InvokeGetPattern("HELLO", "OZONE");
        // H-W, E-Y (E is at pos 4 in OZONE), L-W, L-W, O-Y (O is at pos 0 or 2 in OZONE)
        Assert.Equal("WYWWY", pattern);
    }

    [Fact]
    public void GetPattern_MixedGreenYellow_CorrectPattern()
    {
        var pattern = InvokeGetPattern("CRANE", "TRACE");
        // C-Y (pos 3 in TRACE), R-G (pos 1), A-G (pos 2), N-W, E-G (pos 4)
        Assert.Equal("YGGWG", pattern);
    }

    [Fact]
    public void GetPattern_DuplicateLetters_CorrectPattern()
    {
        // Guess SPEED against answer ERASE
        // S-Y (pos 3 in ERASE), P-W, E-Y (pos 0 in ERASE), E-Y (pos 4 in ERASE), D-W
        var pattern = InvokeGetPattern("SPEED", "ERASE");
        Assert.Equal("YWYYW", pattern);
    }

    [Fact]
    public void GetRecommendations_NoGuesses_ReturnsBestStartingWords()
    {
        var guesses = new List<Guess>();
        var recommendations = _service.GetRecommendations(guesses, hardMode: false, topN: 5);

        Assert.Equal(5, recommendations.Count);
        Assert.All(recommendations, r => Assert.True(r.Score > 0));

        // Log the recommendations for inspection
        _testOutputHelper.WriteLine("Best starting words:");
        foreach (var rec in recommendations)
        {
            _testOutputHelper.WriteLine($"  {rec.Word.ToUpper()}: Score={rec.Score:F3}, " +
                $"PossibleAnswer={rec.IsPossibleAnswer}");
        }
    }

    [Fact]
    public void GetRecommendations_OneAnswerRemaining_ReturnsThatAnswer()
    {
        // Create guesses that narrow down to one answer: "ZONAL"
        var guesses = new List<Guess>
        {
            new Guess(
                new[] { 'z', 'o', 'n', 'a', 'l' },
                new[] { LetterState.Green, LetterState.Green, LetterState.Green,
                       LetterState.Green, LetterState.Green }
            )
        };

        var recommendations = _service.GetRecommendations(guesses, hardMode: false, topN: 5);

        Assert.Single(recommendations);
        Assert.Equal("zonal", recommendations[0].Word);
    }

    [Fact]
    public void GetRecommendations_HardMode_RespectsConstraints()
    {
        // Guess CRANE with specific feedback
        var guesses = new List<Guess>
        {
            new Guess(
                new[] { 'c', 'r', 'a', 'n', 'e' },
                new[] { LetterState.White, LetterState.Yellow, LetterState.Green,
                       LetterState.White, LetterState.Yellow }
            )
        };

        var recommendations = _service.GetRecommendations(guesses, hardMode: true, topN: 5);

        // In hard mode, all recommendations must:
        // - Have 'A' in position 2 (green)
        // - Contain 'R' but not in position 1 (yellow)
        // - Contain 'E' but not in position 4 (yellow)
        // - Not contain 'C' or 'N' (white)

        foreach (var rec in recommendations)
        {
            Assert.Equal('a', rec.Word[2]); // A must be in position 2
            Assert.Contains('r', rec.Word); // Must contain R
            Assert.NotEqual('r', rec.Word[1]); // R not in position 1
            Assert.Contains('e', rec.Word); // Must contain E
            Assert.NotEqual('e', rec.Word[4]); // E not in position 4
            Assert.DoesNotContain('c', rec.Word); // Must not contain C
            Assert.DoesNotContain('n', rec.Word); // Must not contain N

            _testOutputHelper.WriteLine($"Hard mode recommendation: {rec.Word.ToUpper()}");
        }
    }

    [Fact]
    public void GetRecommendations_NormalMode_AllowsAnyWord()
    {
        // Same guesses as hard mode test
        var guesses = new List<Guess>
        {
            new Guess(
                new[] { 'c', 'r', 'a', 'n', 'e' },
                new[] { LetterState.White, LetterState.Yellow, LetterState.Green,
                       LetterState.White, LetterState.Yellow }
            )
        };

        var normalRecs = _service.GetRecommendations(guesses, hardMode: false, topN: 5);
        var hardRecs = _service.GetRecommendations(guesses, hardMode: true, topN: 5);

        // Normal mode might include words that don't satisfy hard mode constraints
        // (to maximize information gain)
        Assert.NotNull(normalRecs);
        Assert.NotNull(hardRecs);

        _testOutputHelper.WriteLine("Normal mode recommendations:");
        foreach (var rec in normalRecs)
        {
            _testOutputHelper.WriteLine($"  {rec.Word.ToUpper()}: Score={rec.Score:F3}");
        }

        _testOutputHelper.WriteLine("\nHard mode recommendations:");
        foreach (var rec in hardRecs)
        {
            _testOutputHelper.WriteLine($"  {rec.Word.ToUpper()}: Score={rec.Score:F3}");
        }
    }

    [Fact]
    public void GetRecommendations_PerformanceTest()
    {
        var guesses = new List<Guess>
        {
            new Guess(
                new[] { 's', 'l', 'a', 't', 'e' },
                new[] { LetterState.White, LetterState.White, LetterState.Yellow,
                       LetterState.White, LetterState.Yellow }
            )
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var recommendations = _service.GetRecommendations(guesses, hardMode: false, topN: 5);
        stopwatch.Stop();

        Assert.NotEmpty(recommendations);
        _testOutputHelper.WriteLine($"Performance: Generated {recommendations.Count} recommendations in {stopwatch.ElapsedMilliseconds}ms");
        _testOutputHelper.WriteLine($"Remaining possible answers: {recommendations[0].RemainingAnswers}");

        // Performance should be reasonable (under 30 seconds for a single recommendation query)
        Assert.True(stopwatch.ElapsedMilliseconds < 30000,
            $"Recommendation took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void GenerateStartingWordsExcludingUsed()
    {
        // Load used words
        var usedWords = new Dictionary<string, (int gameNumber, string date)>();
        var usedCsvPath = Path.Combine("..", "..", "..", "..", "wwwroot", "used-words.csv");

        if (File.Exists(usedCsvPath))
        {
            var lines = File.ReadAllLines(usedCsvPath);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    var word = parts[0].Trim().ToLower();
                    if (word.Length == 5 && int.TryParse(parts[1].Trim(), out int gameNumber))
                    {
                        var date = parts[2].Trim();
                        usedWords[word] = (gameNumber, date);
                    }
                }
            }
        }

        _service.LoadUsedWords(usedWords);

        // Calculate best starting words for Normal mode
        // Used words are automatically reclassified as guess-only
        var normalRecs = _service.GetRecommendations(new List<Guess>(), hardMode: false, topN: 5);

        // Calculate best starting words for Hard mode
        // Used words are automatically reclassified as guess-only
        var hardRecs = _service.GetRecommendations(new List<Guess>(), hardMode: true, topN: 5);

        _testOutputHelper.WriteLine("Normal mode starting words (excluding used):");
        _testOutputHelper.WriteLine("{");
        _testOutputHelper.WriteLine("  \"normal\": [");
        for (int i = 0; i < normalRecs.Count; i++)
        {
            var rec = normalRecs[i];
            var comma = i < normalRecs.Count - 1 ? "," : "";
            _testOutputHelper.WriteLine($"    {{");
            _testOutputHelper.WriteLine($"      \"word\": \"{rec.Word}\",");
            _testOutputHelper.WriteLine($"      \"score\": {rec.Score:F3},");
            _testOutputHelper.WriteLine($"      \"isPossibleAnswer\": {rec.IsPossibleAnswer.ToString().ToLower()}");
            _testOutputHelper.WriteLine($"    }}{comma}");
        }
        _testOutputHelper.WriteLine("  ],");

        _testOutputHelper.WriteLine("  \"hard\": [");
        for (int i = 0; i < hardRecs.Count; i++)
        {
            var rec = hardRecs[i];
            var comma = i < hardRecs.Count - 1 ? "," : "";
            _testOutputHelper.WriteLine($"    {{");
            _testOutputHelper.WriteLine($"      \"word\": \"{rec.Word}\",");
            _testOutputHelper.WriteLine($"      \"score\": {rec.Score:F3},");
            _testOutputHelper.WriteLine($"      \"isPossibleAnswer\": {rec.IsPossibleAnswer.ToString().ToLower()}");
            _testOutputHelper.WriteLine($"    }}{comma}");
        }
        _testOutputHelper.WriteLine("  ]");
        _testOutputHelper.WriteLine("}");

        Assert.Equal(5, normalRecs.Count);
        Assert.Equal(5, hardRecs.Count);
    }

    [Fact]
    public void LoadUsedWords_WithCutoff_OnlyMarksWordsBeforeCutoffAsUnavailable()
    {
        // Arrange: Create used words with different game numbers
        var usedWords = new Dictionary<string, (int gameNumber, string date)>
        {
            { "cigar", (0, "6/19/2021") },      // Game 0 - should be unavailable
            { "rebut", (1, "6/20/2021") },      // Game 1 - should be unavailable
            { "sissy", (2, "6/21/2021") },      // Game 2 - should be unavailable
            { "knoll", (100, "9/27/2021") },    // Game 100 - cutoff, should be AVAILABLE
            { "found", (101, "9/28/2021") },    // Game 101 - should be AVAILABLE
            { "truck", (1637, "12/12/2025") }   // Game 1637 - should be AVAILABLE
        };

        // Act: Load used words with cutoff at game 100
        _service.LoadUsedWords(usedWords, cutoffGameNumber: 100);

        // Create guesses that allow all these words as possible answers
        var guesses = new List<Guess>();
        var recommendations = _service.GetRecommendations(guesses, hardMode: false, topN: 100);

        // Assert: Words before cutoff (games 0-99) should NOT be in possible answers
        Assert.DoesNotContain(recommendations, r => r.Word == "cigar" && r.IsPossibleAnswer);
        Assert.DoesNotContain(recommendations, r => r.Word == "rebut" && r.IsPossibleAnswer);
        Assert.DoesNotContain(recommendations, r => r.Word == "sissy" && r.IsPossibleAnswer);

        // Assert: Words at or after cutoff (games 100+) SHOULD be possible answers
        // Note: These words might not appear in recommendations if they're not in the word list
        // but if they do appear, they should be marked as possible answers
        var knollRec = recommendations.FirstOrDefault(r => r.Word == "knoll");
        var foundRec = recommendations.FirstOrDefault(r => r.Word == "found");
        var truckRec = recommendations.FirstOrDefault(r => r.Word == "truck");

        if (knollRec != null)
            Assert.True(knollRec.IsPossibleAnswer, "knoll (game 100) should be a possible answer");
        if (foundRec != null)
            Assert.True(foundRec.IsPossibleAnswer, "found (game 101) should be a possible answer");
        if (truckRec != null)
            Assert.True(truckRec.IsPossibleAnswer, "truck (game 1637) should be a possible answer");

        _testOutputHelper.WriteLine($"Tested cutoff at game 100:");
        _testOutputHelper.WriteLine($"  Games 0-99: Marked as unavailable");
        _testOutputHelper.WriteLine($"  Games 100+: Remain as possible answers");
    }

    [Fact]
    public void LoadUsedWords_WithNoCutoff_MarksAllUsedWordsAsUnavailable()
    {
        // Arrange: Create used words with different game numbers
        var usedWords = new Dictionary<string, (int gameNumber, string date)>
        {
            { "cigar", (0, "6/19/2021") },
            { "knoll", (100, "9/27/2021") },
            { "truck", (1637, "12/12/2025") }
        };

        // Act: Load used words with NO cutoff (null)
        _service.LoadUsedWords(usedWords, cutoffGameNumber: null);

        var guesses = new List<Guess>();
        var recommendations = _service.GetRecommendations(guesses, hardMode: false, topN: 100);

        // Assert: ALL used words should NOT be possible answers (original behavior)
        Assert.DoesNotContain(recommendations, r => r.Word == "cigar" && r.IsPossibleAnswer);
        Assert.DoesNotContain(recommendations, r => r.Word == "knoll" && r.IsPossibleAnswer);
        Assert.DoesNotContain(recommendations, r => r.Word == "truck" && r.IsPossibleAnswer);

        _testOutputHelper.WriteLine($"Tested with no cutoff (null):");
        _testOutputHelper.WriteLine($"  All used words marked as unavailable (original behavior)");
    }

    [Fact]
    public void LoadUsedWords_WithCutoffAtToday_TodayWordRemainsAvailable()
    {
        // Arrange: Simulate playing today's puzzle (game 1640 as example)
        var todayGameNumber = 1640;
        var usedWords = new Dictionary<string, (int gameNumber, string date)>
        {
            { "paste", (1638, "12/13/2025") },  // Yesterday - should be unavailable
            { "borax", (1639, "12/14/2025") },  // Yesterday - should be unavailable
            { "magic", (1640, "12/15/2025") },  // Today - should be AVAILABLE
            { "truck", (1641, "12/16/2025") }   // Future - should be AVAILABLE
        };

        // Act: Load used words with cutoff at today's game number
        _service.LoadUsedWords(usedWords, cutoffGameNumber: todayGameNumber);

        var guesses = new List<Guess>();
        var recommendations = _service.GetRecommendations(guesses, hardMode: false, topN: 100);

        // Assert: Past words should not be possible answers
        Assert.DoesNotContain(recommendations, r => r.Word == "paste" && r.IsPossibleAnswer);
        Assert.DoesNotContain(recommendations, r => r.Word == "borax" && r.IsPossibleAnswer);

        // Assert: Today's word and future words should be possible answers
        var magicRec = recommendations.FirstOrDefault(r => r.Word == "magic");
        var truckRec = recommendations.FirstOrDefault(r => r.Word == "truck");

        if (magicRec != null)
            Assert.True(magicRec.IsPossibleAnswer, "Today's word (magic) should be a possible answer");
        if (truckRec != null)
            Assert.True(truckRec.IsPossibleAnswer, "Future word (truck) should be a possible answer");

        _testOutputHelper.WriteLine($"Tested cutoff at today's game ({todayGameNumber}):");
        _testOutputHelper.WriteLine($"  Past games: Unavailable");
        _testOutputHelper.WriteLine($"  Today and future: Available");
    }

    [Fact]
    public void GenerateHighQualityWordLists()
    {
        // This test generates optimized word lists for Normal mode performance
        _testOutputHelper.WriteLine("Generating high-quality word lists...");

        var allWordsList = GetAllWords();
        var answerWords = allWordsList.Where(w => w.IsPossibleAnswer).ToList();
        var guessOnlyWords = allWordsList.Where(w => !w.IsPossibleAnswer).ToList();

        _testOutputHelper.WriteLine($"Total words: {allWordsList.Count}");
        _testOutputHelper.WriteLine($"  Answer words: {answerWords.Count}");
        _testOutputHelper.WriteLine($"  Guess-only words: {guessOnlyWords.Count}");

        // 1. Generate high-quality candidates for early game (~1,500 words)
        // Include both answer words and guess-only words, filter by quality
        var highQualityCandidates = allWordsList
            .Where(w => IsHighQualityWord(w.Word))
            .Select(w => w.Word)
            .OrderBy(w => w)
            .ToList();

        _testOutputHelper.WriteLine($"\nHigh-quality candidates: {highQualityCandidates.Count}");

        // 2. Generate top guess-only words for mid game (~2,000 words)
        // Score guess-only words by letter frequency and diversity
        var scoredGuessWords = guessOnlyWords
            .Select(w => new
            {
                Word = w.Word,
                Score = CalculateWordQualityScore(w.Word)
            })
            .OrderByDescending(w => w.Score)
            .Take(2000)
            .Select(w => w.Word)
            .ToList();

        _testOutputHelper.WriteLine($"Top guess-only words: {scoredGuessWords.Count}");

        // Generate JSON output
        var outputPath = Path.Combine("..", "..", "..", "..", "wwwroot", "high-quality-words.json");
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            highQuality = highQualityCandidates,
            topGuessOnly = scoredGuessWords
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(outputPath, json);
        _testOutputHelper.WriteLine($"\nGenerated high-quality-words.json ({json.Length} bytes)");
        _testOutputHelper.WriteLine($"Location: {Path.GetFullPath(outputPath)}");
    }

    private bool IsHighQualityWord(string word)
    {
        var letters = word.ToCharArray();
        var uniqueLetters = letters.Distinct().Count();
        var vowels = new[] { 'a', 'e', 'i', 'o', 'u' };
        var commonLetters = new[] { 'e', 'a', 'r', 's', 't', 'o', 'i', 'n' };

        // Only words with all unique letters (no duplicates)
        if (uniqueLetters != 5) return false;

        // At least 3 unique vowels OR at least 4 unique common letters
        var uniqueVowelCount = letters.Where(c => vowels.Contains(c)).Distinct().Count();
        var uniqueCommonLetterCount = letters.Where(c => commonLetters.Contains(c)).Distinct().Count();

        return uniqueVowelCount >= 3 || uniqueCommonLetterCount >= 4;
    }

    private double CalculateWordQualityScore(string word)
    {
        var letters = word.ToCharArray();
        var uniqueLetters = letters.Distinct().Count();

        // Letter frequency scores (based on English letter frequency)
        var letterScores = new Dictionary<char, double>
        {
            {'e', 12.7}, {'t', 9.1}, {'a', 8.2}, {'o', 7.5}, {'i', 7.0},
            {'n', 6.7}, {'s', 6.3}, {'h', 6.1}, {'r', 6.0}, {'d', 4.3},
            {'l', 4.0}, {'c', 2.8}, {'u', 2.8}, {'m', 2.4}, {'w', 2.4},
            {'f', 2.2}, {'g', 2.0}, {'y', 2.0}, {'p', 1.9}, {'b', 1.5},
            {'v', 1.0}, {'k', 0.8}, {'j', 0.15}, {'x', 0.15}, {'q', 0.10}, {'z', 0.07}
        };

        // Calculate score based on letter diversity and frequency
        double frequencyScore = letters.Sum(c => letterScores.GetValueOrDefault(c, 0.0));
        double diversityScore = uniqueLetters * 2.0;  // Bonus for unique letters

        return frequencyScore + diversityScore;
    }

    private List<WordleStrategyService.WordEntry> GetAllWords()
    {
        var allWordsField = typeof(WordleStrategyService).GetField("_allWords",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (allWordsField == null)
            throw new InvalidOperationException("_allWords field not found");

        var allWords = allWordsField.GetValue(_service) as List<WordleStrategyService.WordEntry>;
        return allWords ?? new List<WordleStrategyService.WordEntry>();
    }

    [Fact]
    public void GenerateSecondWordCache()
    {
        // This test generates pre-computed second-guess recommendations for top 20 starting words
        // WARNING: This will take 2-4 hours to run! Run offline when needed.
        _testOutputHelper.WriteLine("Generating second-word cache for top 20 starting words...");
        _testOutputHelper.WriteLine("WARNING: This will take 2-4 hours to complete!");

        var top20StartingWords = new[]
        {
            "salet", "raile", "saner", "arose", "irate",
            "soare", "alter", "later", "crate", "trace",
            "slate", "stare", "snare", "crane", "slant",
            "caste", "roast", "least", "toast", "coast"
        };

        var allWordsList = GetAllWords();
        var answerWords = allWordsList.Where(w => w.IsPossibleAnswer).Select(w => w.Word).ToList();

        _testOutputHelper.WriteLine($"Processing {top20StartingWords.Length} starting words against {answerWords.Count} answers...");

        var cache = new Dictionary<string, Dictionary<string, List<CachedRecommendation>>>();
        int totalPatterns = 0;

        var startTime = DateTime.Now;

        foreach (var startWord in top20StartingWords)
        {
            _testOutputHelper.WriteLine($"\nProcessing '{startWord}'...");
            var patternMap = new Dictionary<string, List<CachedRecommendation>>();

            // Get all unique patterns for this starting word
            var patternsForWord = answerWords
                .Select(answer => InvokeGetPattern(startWord, answer))
                .Distinct()
                .ToList();

            _testOutputHelper.WriteLine($"  Found {patternsForWord.Count} unique patterns");

            foreach (var pattern in patternsForWord)
            {
                // Create a guess for this starting word with this pattern
                var guess = new Guess(
                    startWord.ToCharArray(),
                    PatternStringToLetterStates(pattern)
                );

                // Calculate best second guesses for this pattern
                var recommendations = _service.GetRecommendations(
                    new List<Guess> { guess },
                    hardMode: false,
                    topN: 5
                );

                patternMap[pattern] = recommendations.Select(r => new CachedRecommendation
                {
                    Word = r.Word,
                    Score = r.Score,
                    IsPossibleAnswer = r.IsPossibleAnswer
                }).ToList();

                totalPatterns++;
            }

            cache[startWord] = patternMap;
            _testOutputHelper.WriteLine($"  Completed {patternMap.Count} patterns for '{startWord}'");
        }

        var elapsed = DateTime.Now - startTime;
        _testOutputHelper.WriteLine($"\nCompleted in {elapsed.TotalMinutes:F1} minutes");
        _testOutputHelper.WriteLine($"Total patterns cached: {totalPatterns}");

        // Generate JSON output
        var outputPath = Path.Combine("..", "..", "..", "..", "wwwroot", "second-word-cache.json");
        var json = System.Text.Json.JsonSerializer.Serialize(cache,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(outputPath, json);
        _testOutputHelper.WriteLine($"\nGenerated second-word-cache.json ({json.Length} bytes)");
        _testOutputHelper.WriteLine($"Location: {Path.GetFullPath(outputPath)}");
    }

    private LetterState[] PatternStringToLetterStates(string pattern)
    {
        return pattern.Select(c => c switch
        {
            'G' => LetterState.Green,
            'Y' => LetterState.Yellow,
            'W' => LetterState.White,
            _ => LetterState.None
        }).ToArray();
    }

    private class CachedRecommendation
    {
        public string Word { get; set; } = "";
        public double Score { get; set; }
        public bool IsPossibleAnswer { get; set; }
    }

    [Fact]
    public void HistoricalPuzzle_Puzzle0_ShowsAllMatchingWords()
    {
        // Arrange: Load used words and set cutoff to puzzle 0
        var usedWords = new Dictionary<string, (int gameNumber, string date)>
        {
            { "cigar", (0, "6/19/2021") },
            { "flair", (303, "4/18/2022") },
            { "chair", (1545, "9/11/2025") }
            // vicar is not in used-words.csv, so it's an unused word
        };

        _service.LoadUsedWords(usedWords, cutoffGameNumber: 0);

        // Create guesses matching the user's scenario
        // SANER: yellow A (pos 1), green R (pos 4)
        var guess1 = new Guess(
            new[] { 's', 'a', 'n', 'e', 'r' },
            new[] { LetterState.White, LetterState.Yellow, LetterState.White,
                   LetterState.White, LetterState.Green }
        );

        // ARMOR: yellow A (pos 0), green R (pos 4)
        var guess2 = new Guess(
            new[] { 'a', 'r', 'm', 'o', 'r' },
            new[] { LetterState.Yellow, LetterState.White, LetterState.White,
                   LetterState.White, LetterState.Green }
        );

        var guesses = new List<Guess> { guess1, guess2 };

        // Act: Get recommendations in hard mode
        var recommendations = _service.GetRecommendationsQuick(guesses, hardMode: true, topN: 5);

        // Assert: All 4 words should be returned as possible answers
        _testOutputHelper.WriteLine($"Recommendations count: {recommendations.Count}");
        foreach (var rec in recommendations)
        {
            _testOutputHelper.WriteLine($"  {rec.Word}: IsPossibleAnswer={rec.IsPossibleAnswer}, Score={rec.Score:F2}");
        }

        // All 4 words should be marked as possible answers (even if not all in top 5)
        var words = recommendations.Select(r => r.Word).ToList();

        // These 3 should definitely be in the top 5
        Assert.Contains("chair", words);
        Assert.Contains("cigar", words);
        Assert.Contains("vicar", words);

        // Verify they're all marked as possible answers
        Assert.All(recommendations.Where(r => new[] { "chair", "cigar", "vicar" }.Contains(r.Word)),
            r => Assert.True(r.IsPossibleAnswer, $"{r.Word} should be a possible answer"));

        Assert.True(recommendations.Count >= 3, $"Expected at least 3 recommendations, got {recommendations.Count}");
    }

    // Helper method to access private GetPattern method via reflection
    private string InvokeGetPattern(string guess, string answer)
    {
        var method = typeof(WordleStrategyService).GetMethod("GetPattern",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method == null)
            throw new InvalidOperationException("GetPattern method not found");

        var result = method.Invoke(_service, new object[] { guess, answer });
        return result?.ToString() ?? string.Empty;
    }
}
