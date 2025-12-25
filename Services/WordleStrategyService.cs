using static WordleHelper.Services.WordleFilterService;

namespace WordleHelper.Services;

public class WordleStrategyService
{
    private List<WordEntry> _allWords;
    private readonly WordleFilterService _filterService;
    private List<Recommendation>? _cachedStartingWordsNormal;
    private List<Recommendation>? _cachedStartingWordsHard;
    private Dictionary<string, (int gameNumber, string date)> _usedWords = new();
    private List<string> _highQualityCandidates = new();
    private HashSet<string> _topGuessOnlyWords = new();
    private Dictionary<string, Dictionary<string, List<Recommendation>>>? _secondWordCache;

    public class WordEntry
    {
        public string Word { get; set; }
        public bool IsPossibleAnswer { get; set; }

        public WordEntry(string word, bool isPossibleAnswer)
        {
            Word = word;
            IsPossibleAnswer = isPossibleAnswer;
        }
    }

    public class Recommendation
    {
        public string Word { get; set; }
        public double Score { get; set; }
        public int RemainingAnswers { get; set; }
        public bool IsPossibleAnswer { get; set; }

        public Recommendation(string word, double score, int remainingAnswers, bool isPossibleAnswer)
        {
            Word = word;
            Score = score;
            RemainingAnswers = remainingAnswers;
            IsPossibleAnswer = isPossibleAnswer;
        }
    }

    public WordleStrategyService(WordleFilterService filterService)
    {
        _filterService = filterService;
        _allWords = new List<WordEntry>();

        // Try to load from file system (works for unit tests)
        try
        {
            LoadAllWordsFromFileSystem();
        }
        catch (Exception ex)
        {
            // In Blazor WebAssembly, file system access fails
            // Words will be loaded via InitializeAsync instead
            System.Diagnostics.Debug.WriteLine($"Failed to load words from file system: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialize the service with separate word lists (for Blazor WebAssembly)
    /// </summary>
    public void Initialize(string answerWordsContent, string guessOnlyWordsContent)
    {
        _allWords = new List<WordEntry>();

        // Parse answer words (possible solutions)
        var answerWords = answerWordsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLower())
            .Where(w => w.Length == 5);

        foreach (var word in answerWords)
        {
            _allWords.Add(new WordEntry(word, isPossibleAnswer: true));
        }

        // Parse guess-only words (valid guesses but not solutions)
        var guessOnlyWords = guessOnlyWordsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLower())
            .Where(w => w.Length == 5);

        foreach (var word in guessOnlyWords)
        {
            _allWords.Add(new WordEntry(word, isPossibleAnswer: false));
        }
    }

    /// <summary>
    /// Load pre-calculated starting words from JSON
    /// </summary>
    public void LoadStartingWords(string jsonContent)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(jsonContent);

            _cachedStartingWordsNormal = ParseRecommendations(doc.RootElement.GetProperty("normal"));
            _cachedStartingWordsHard = ParseRecommendations(doc.RootElement.GetProperty("hard"));
        }
        catch
        {
            // If loading fails, starting words will be calculated on demand
            _cachedStartingWordsNormal = null;
            _cachedStartingWordsHard = null;
        }
    }

    /// <summary>
    /// Load pre-filtered high-quality word lists for Normal mode optimization
    /// </summary>
    public void LoadHighQualityWords(string jsonContent)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(jsonContent);

            // Load high-quality candidates for early game
            if (doc.RootElement.TryGetProperty("highQuality", out var highQualityElement))
            {
                _highQualityCandidates = highQualityElement.EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToList();
            }

            // Load top guess-only words for mid game
            if (doc.RootElement.TryGetProperty("topGuessOnly", out var topGuessElement))
            {
                _topGuessOnlyWords = topGuessElement.EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToHashSet();
            }
        }
        catch
        {
            // If loading fails, fall back to using all words (slower but functional)
            _highQualityCandidates = new List<string>();
            _topGuessOnlyWords = new HashSet<string>();
        }
    }

    /// <summary>
    /// Load pre-computed second-word cache for instant second-guess recommendations
    /// </summary>
    public void LoadSecondWordCache(string jsonContent)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(jsonContent);
            _secondWordCache = new Dictionary<string, Dictionary<string, List<Recommendation>>>();

            // Parse each starting word's patterns
            foreach (var startWord in doc.RootElement.EnumerateObject())
            {
                var patternMap = new Dictionary<string, List<Recommendation>>();

                foreach (var pattern in startWord.Value.EnumerateObject())
                {
                    var recommendations = new List<Recommendation>();

                    foreach (var rec in pattern.Value.EnumerateArray())
                    {
                        var word = rec.GetProperty("Word").GetString() ?? "";
                        var score = rec.GetProperty("Score").GetDouble();
                        var isPossibleAnswer = rec.GetProperty("IsPossibleAnswer").GetBoolean();

                        // Use 0 for remaining answers since it will be recalculated
                        recommendations.Add(new Recommendation(word, score, 0, isPossibleAnswer));
                    }

                    patternMap[pattern.Name] = recommendations;
                }

                _secondWordCache[startWord.Name] = patternMap;
            }
        }
        catch
        {
            // If loading fails, fall back to live calculation
            _secondWordCache = null;
        }
    }

    private List<Recommendation> ParseRecommendations(System.Text.Json.JsonElement element)
    {
        var recommendations = new List<Recommendation>();

        foreach (var item in element.EnumerateArray())
        {
            var word = item.GetProperty("word").GetString() ?? "";
            var score = item.GetProperty("score").GetDouble();
            var isPossibleAnswer = item.GetProperty("isPossibleAnswer").GetBoolean();

            // Get count of all possible answers for the remaining answers field
            var totalAnswers = _allWords.Count(w => w.IsPossibleAnswer);

            recommendations.Add(new Recommendation(word, score, totalAnswers, isPossibleAnswer));
        }

        return recommendations;
    }

    /// <summary>
    /// Check if the service has been initialized with word data
    /// </summary>
    public bool IsInitialized => _allWords.Any();

    /// <summary>
    /// Load used words and reclassify them as guess-only words
    /// Used words can still be valid guesses but are no longer possible answers
    /// </summary>
    public void LoadUsedWords(Dictionary<string, (int gameNumber, string date)> usedWords, int? cutoffGameNumber = null)
    {
        _usedWords = usedWords;

        // First, reset all words in the usedWords dictionary back to their original state (possible answers)
        // This allows re-loading with a different cutoff (e.g., when switching between historical puzzles)
        foreach (var wordEntry in _allWords)
        {
            if (_usedWords.ContainsKey(wordEntry.Word))
            {
                // Reset to original state: words from answer-words.txt should be possible answers
                // We can identify these as words that are in _usedWords (which only contains answer words)
                wordEntry.IsPossibleAnswer = true;
            }
        }

        // Now apply the filtering logic based on cutoff
        foreach (var wordEntry in _allWords)
        {
            if (wordEntry.IsPossibleAnswer && _usedWords.ContainsKey(wordEntry.Word))
            {
                var gameNumber = _usedWords[wordEntry.Word].gameNumber;

                // If no cutoff specified, treat all used words as unavailable (original behavior)
                // If cutoff specified, only treat words used BEFORE the cutoff as unavailable
                if (cutoffGameNumber == null || gameNumber < cutoffGameNumber)
                {
                    wordEntry.IsPossibleAnswer = false;
                }
            }
        }
    }

    private void LoadAllWordsFromFileSystem()
    {
        // Try multiple paths to find the word files (for unit tests in different working directories)
        var possibleBasePaths = new[]
        {
            "wwwroot",
            Path.Combine("..", "wwwroot"),
            Path.Combine("..", "..", "wwwroot"),
            Path.Combine("..", "..", "..", "wwwroot"),
            Path.Combine("..", "..", "..", "..", "wwwroot")
        };

        string? basePath = null;
        foreach (var path in possibleBasePaths)
        {
            if (File.Exists(Path.Combine(path, "words.txt")) &&
                File.Exists(Path.Combine(path, "guess-only-words.txt")))
            {
                basePath = path;
                break;
            }
        }

        if (basePath == null)
        {
            throw new FileNotFoundException(
                $"Word list files not found. Tried base paths: {string.Join(", ", possibleBasePaths)}");
        }

        var answerWordsContent = File.ReadAllText(Path.Combine(basePath, "words.txt"));
        var guessOnlyWordsContent = File.ReadAllText(Path.Combine(basePath, "guess-only-words.txt"));

        Initialize(answerWordsContent, guessOnlyWordsContent);
    }


    /// <summary>
    /// Gets word recommendations based on current game state
    /// Used words are automatically treated as guess-only words (not possible answers)
    /// </summary>
    /// <param name="guesses">List of previous guesses with their letter states</param>
    /// <param name="hardMode">If true, apply hard mode constraints</param>
    /// <param name="topN">Number of recommendations to return</param>
    /// <returns>List of recommended words with their scores</returns>
    public List<Recommendation> GetRecommendations(List<Guess> guesses, bool hardMode, int topN = 5)
    {
        // Use cached starting words if no guesses have been made
        // These are pre-calculated optimal words and should not be filtered
        if (guesses.Count == 0)
        {
            if (hardMode && _cachedStartingWordsHard != null)
            {
                return _cachedStartingWordsHard;
            }
            else if (!hardMode && _cachedStartingWordsNormal != null)
            {
                return _cachedStartingWordsNormal;
            }
        }

        // Check second-word cache for instant recommendations (Normal mode only)
        if (guesses.Count == 1 && !hardMode && _secondWordCache != null)
        {
            var firstGuess = new string(guesses[0].Letters).ToLower();
            var pattern = GetPatternString(guesses[0]);

            if (_secondWordCache.TryGetValue(firstGuess, out var patterns))
            {
                if (patterns.TryGetValue(pattern, out var cachedRecommendations))
                {
                    // Recalculate remaining answers count (cache stores 0)
                    var possibleAnswersForCount = _allWords
                        .Where(w => w.IsPossibleAnswer)
                        .Select(w => w.Word)
                        .Where(w => _filterService.MatchesPattern(w, guesses))
                        .Count();

                    // Update remaining answers count in cached recommendations
                    return cachedRecommendations
                        .Select(r => new Recommendation(r.Word, r.Score, possibleAnswersForCount, r.IsPossibleAnswer))
                        .ToList();
                }
            }
        }

        // Get remaining possible answers
        // Used words have already been reclassified as guess-only in LoadUsedWords()
        var possibleAnswers = _allWords
            .Where(w => w.IsPossibleAnswer)
            .Select(w => w.Word)
            .Where(w => _filterService.MatchesPattern(w, guesses))
            .ToList();

        // If only one answer remains, return it
        if (possibleAnswers.Count <= 1)
        {
            return possibleAnswers
                .Select(w => new Recommendation(w, 1.0, 1, true))
                .ToList();
        }

        // Get valid guess words (apply hard mode constraints or smart filtering)
        // Hard mode: only words matching current pattern
        // Normal mode: smart filtering based on game state for performance
        var validGuesses = hardMode
            ? GetHardModeValidGuesses(guesses)
            : GetSmartNormalModeCandidates(possibleAnswers);

        // Calculate score for each valid guess
        var recommendations = validGuesses
            .AsParallel()
            .Select(guess => new
            {
                Word = guess,
                Score = CalculateExpectedInformation(guess, possibleAnswers),
                IsPossibleAnswer = _allWords.First(w => w.Word == guess).IsPossibleAnswer
            })
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.IsPossibleAnswer) // Prefer possible answers as tie-breaker
            .Take(topN)
            .Select(r => new Recommendation(r.Word, r.Score, possibleAnswers.Count, r.IsPossibleAnswer))
            .ToList();

        return recommendations;
    }

    /// <summary>
    /// Gets quick recommendations by evaluating a smaller subset of candidates
    /// Used for Phase 3 progressive results - shows instant feedback
    /// </summary>
    public List<Recommendation> GetRecommendationsQuick(List<Guess> guesses, bool hardMode, int topN = 5)
    {
        // Use cached starting words if no guesses have been made
        if (guesses.Count == 0)
        {
            if (hardMode && _cachedStartingWordsHard != null)
                return _cachedStartingWordsHard;
            else if (!hardMode && _cachedStartingWordsNormal != null)
                return _cachedStartingWordsNormal;
        }

        // Check second-word cache (instant for cached patterns)
        if (guesses.Count == 1 && !hardMode && _secondWordCache != null)
        {
            var firstGuess = new string(guesses[0].Letters).ToLower();
            var pattern = GetPatternString(guesses[0]);

            if (_secondWordCache.TryGetValue(firstGuess, out var patterns))
            {
                if (patterns.TryGetValue(pattern, out var cachedRecommendations))
                {
                    var possibleAnswersForCount = _allWords
                        .Where(w => w.IsPossibleAnswer)
                        .Select(w => w.Word)
                        .Where(w => _filterService.MatchesPattern(w, guesses))
                        .Count();

                    return cachedRecommendations
                        .Select(r => new Recommendation(r.Word, r.Score, possibleAnswersForCount, r.IsPossibleAnswer))
                        .ToList();
                }
            }
        }

        // Get remaining possible answers
        var possibleAnswers = _allWords
            .Where(w => w.IsPossibleAnswer)
            .Select(w => w.Word)
            .Where(w => _filterService.MatchesPattern(w, guesses))
            .ToList();

        if (possibleAnswers.Count <= 1)
        {
            return possibleAnswers
                .Select(w => new Recommendation(w, 1.0, 1, true))
                .ToList();
        }

        // Get quick subset of candidates (limit to ~500 for instant results)
        var validGuesses = hardMode
            ? GetHardModeValidGuesses(guesses).Take(500).ToList()
            : GetQuickNormalModeCandidates(possibleAnswers);

        // Calculate scores for quick subset
        var recommendations = validGuesses
            .AsParallel()
            .Select(guess => new
            {
                Word = guess,
                Score = CalculateExpectedInformation(guess, possibleAnswers),
                IsPossibleAnswer = _allWords.First(w => w.Word == guess).IsPossibleAnswer
            })
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.IsPossibleAnswer)
            .Take(topN)
            .Select(r => new Recommendation(r.Word, r.Score, possibleAnswers.Count, r.IsPossibleAnswer))
            .ToList();

        return recommendations;
    }

    /// <summary>
    /// Gets full recommendations with complete evaluation
    /// Used for Phase 3 progressive results - refines quick results
    /// </summary>
    public List<Recommendation> GetRecommendationsFull(List<Guess> guesses, bool hardMode, int topN = 5)
    {
        // Just delegate to the main GetRecommendations method for full evaluation
        return GetRecommendations(guesses, hardMode, topN);
    }

    /// <summary>
    /// Gets quick subset of candidates for instant progressive results
    /// Returns ~500 best candidates for fast evaluation
    /// </summary>
    private List<string> GetQuickNormalModeCandidates(List<string> possibleAnswers)
    {
        int answerCount = possibleAnswers.Count;

        if (answerCount > 200)  // Early game
        {
            // Take first 500 high-quality candidates
            return _highQualityCandidates.Any()
                ? _highQualityCandidates.Take(500).ToList()
                : _allWords.Select(w => w.Word).Take(500).ToList();
        }
        else if (answerCount > 20)  // Mid game
        {
            // All possible answers + limited guess-only words
            return _allWords
                .Where(w => w.IsPossibleAnswer || _topGuessOnlyWords.Contains(w.Word))
                .Select(w => w.Word)
                .Take(500)
                .ToList();
        }
        else  // Late game
        {
            // Fast enough to check everything
            return _allWords.Select(w => w.Word).ToList();
        }
    }

    /// <summary>
    /// Gets words that satisfy hard mode constraints
    /// Hard mode requires: GREEN letters stay in position, YELLOW letters must be used
    /// </summary>
    private List<string> GetHardModeValidGuesses(List<Guess> guesses)
    {
        return _allWords
            .Select(w => w.Word)
            .Where(w => _filterService.MatchesPattern(w, guesses))
            .ToList();
    }

    /// <summary>
    /// Gets smart filtered candidates for Normal mode based on game state
    /// Early game: Only high-quality words
    /// Mid game: All possible answers + top guess-only words
    /// Late game: All words (already fast)
    /// </summary>
    private List<string> GetSmartNormalModeCandidates(List<string> possibleAnswers)
    {
        int answerCount = possibleAnswers.Count;

        if (answerCount > 200)  // Early game
        {
            // Only high-quality candidates (if loaded), otherwise all words
            return _highQualityCandidates.Any()
                ? _highQualityCandidates
                : _allWords.Select(w => w.Word).ToList();
        }
        else if (answerCount > 20)  // Mid game
        {
            // All possible answers + top guess-only words
            return _allWords
                .Where(w => w.IsPossibleAnswer || _topGuessOnlyWords.Contains(w.Word))
                .Select(w => w.Word)
                .ToList();
        }
        else  // Late game
        {
            // Fast enough to check everything
            return _allWords.Select(w => w.Word).ToList();
        }
    }

    /// <summary>
    /// Randomly samples a subset of items for faster entropy calculation
    /// Used when the number of possible answers is very large (>500)
    /// </summary>
    private List<string> SampleRandomly(List<string> items, int sampleSize)
    {
        if (items.Count <= sampleSize)
            return items;

        var random = new Random();
        return items.OrderBy(x => random.Next()).Take(sampleSize).ToList();
    }


    /// <summary>
    /// Calculates expected information gain (entropy) for a guess
    /// Higher score = more information gained on average
    /// Uses sampling for large answer sets to improve performance (>500 answers)
    /// </summary>
    private double CalculateExpectedInformation(string guess, List<string> possibleAnswers, bool useSampling = true)
    {
        // Sample if too many answers for better performance
        // Maintains ~95% accuracy while being 3-5x faster
        var answersToEvaluate = (useSampling && possibleAnswers.Count > 500)
            ? SampleRandomly(possibleAnswers, 500)
            : possibleAnswers;

        // Group possible answers by the pattern they would produce
        var patternGroups = answersToEvaluate
            .GroupBy(answer => GetPattern(guess, answer))
            .ToList();

        // Calculate entropy: -Σ(p * log2(p))
        double entropy = 0;
        int total = answersToEvaluate.Count;

        foreach (var group in patternGroups)
        {
            double probability = (double)group.Count() / total;
            if (probability > 0)
            {
                entropy -= probability * Math.Log2(probability);
            }
        }

        return entropy;
    }

    /// <summary>
    /// Simulates guessing 'guess' when the answer is 'answer'
    /// Returns a pattern string representing the feedback (e.g., "GYWWG")
    /// </summary>
    private string GetPattern(string guess, string answer)
    {
        var pattern = new char[5];
        var answerChars = answer.ToCharArray();
        var guessChars = guess.ToCharArray();
        var used = new bool[5];

        // First pass: mark greens
        for (int i = 0; i < 5; i++)
        {
            if (guessChars[i] == answerChars[i])
            {
                pattern[i] = 'G'; // Green
                used[i] = true;
            }
        }

        // Second pass: mark yellows and whites
        for (int i = 0; i < 5; i++)
        {
            if (pattern[i] == 'G')
                continue;

            bool found = false;
            for (int j = 0; j < 5; j++)
            {
                if (!used[j] && guessChars[i] == answerChars[j])
                {
                    pattern[i] = 'Y'; // Yellow
                    used[j] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                pattern[i] = 'W'; // White
            }
        }

        return new string(pattern);
    }

    /// <summary>
    /// Converts a Guess object to a pattern string (e.g., "GYWWG")
    /// Used for cache lookups
    /// </summary>
    private string GetPatternString(Guess guess)
    {
        return new string(guess.States.Select(state => state switch
        {
            LetterState.Green => 'G',
            LetterState.Yellow => 'Y',
            LetterState.White => 'W',
            _ => 'W'
        }).ToArray());
    }
}
