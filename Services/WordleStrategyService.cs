using static WordleHelper.Services.WordleFilterService;
using System.Text.Json;

namespace WordleHelper.Services;

public class WordleStrategyService
{
    private List<WordEntry> _allWords;
    private readonly WordleFilterService _filterService;
    private List<Recommendation>? _cachedStartingWordsNormal;
    private List<Recommendation>? _cachedStartingWordsHard;
    private Dictionary<string, (int gameNumber, string date)> _historicalWords = new();
    private Dictionary<string, object> _todaysWordData = new();
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
    /// Load historical word data for games 1-1690 (immutable archive)
    /// </summary>
    public void LoadHistoricalWords(Dictionary<string, (int gameNumber, string date)> historicalWords)
    {
        _historicalWords = historicalWords;

        // For historical games, mark used words as no longer possible answers
        foreach (var wordEntry in _allWords.Where(w => w.IsPossibleAnswer))
        {
            if (_historicalWords.ContainsKey(wordEntry.Word))
            {
                wordEntry.IsPossibleAnswer = false; // Used in historical games
            }
        }
    }

    /// <summary>
    /// Load today's word data from JSON (for current games 1691+)
    /// </summary>
    public void LoadTodaysWord(JsonElement todaysWordData)
    {
        // Parse today's word JSON
        var gameNumber = todaysWordData.GetProperty("gameNumber").GetInt32();
        var word = todaysWordData.GetProperty("word").GetString()?.ToLower() ?? "";
        var recentUsedWords = new HashSet<string>();

        if (todaysWordData.TryGetProperty("recentUsedWords", out var recentArray))
        {
            foreach (var recentWord in recentArray.EnumerateArray())
            {
                var recentWordStr = recentWord.GetString()?.ToLower();
                if (!string.IsNullOrEmpty(recentWordStr))
                {
                    recentUsedWords.Add(recentWordStr);
                }
            }
        }

        // For current games (1691+), words can be reused
        // Only remove today's specific word if it's the current game
        // Note: Today's word should remain IsPossibleAnswer = true
        // Only words from previous dates should be marked as false
        // The UI will handle showing today's word as green vs previous words as yellow
    }

    /// <summary>
    /// Load pre-calculated starting words from JSON
    /// </summary>
    public void LoadStartingWords(string jsonContent)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonContent);
            _cachedStartingWordsNormal = ParseRecommendations(doc.RootElement.GetProperty("normal"));
            _cachedStartingWordsHard = ParseRecommendations(doc.RootElement.GetProperty("hard"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse starting words JSON: {ex.Message}");
            _cachedStartingWordsNormal = new List<Recommendation>();
            _cachedStartingWordsHard = new List<Recommendation>();
        }
    }

    /// <summary>
    /// Load high-quality word candidates from JSON for performance optimization
    /// </summary>
    public void LoadHighQualityWords(string jsonContent)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("highQuality", out var highQualityElement))
            {
                _highQualityCandidates = highQualityElement.EnumerateArray()
                    .Select(element => element.GetString()?.ToLower())
                    .Where(word => !string.IsNullOrEmpty(word))
                    .ToList()!;
            }

            if (doc.RootElement.TryGetProperty("topGuessOnly", out var topGuessOnlyElement))
            {
                _topGuessOnlyWords = topGuessOnlyElement.EnumerateArray()
                    .Select(element => element.GetString()?.ToLower())
                    .Where(word => !string.IsNullOrEmpty(word))
                    .ToHashSet()!;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse high-quality words JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Load second word cache from JSON for instant recommendations
    /// </summary>
    public void LoadSecondWordCache(string jsonContent)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonContent);
            _secondWordCache = new Dictionary<string, Dictionary<string, List<Recommendation>>>();

            foreach (var startWordProperty in doc.RootElement.EnumerateObject())
            {
                var startWord = startWordProperty.Name.ToLower();
                var patterns = new Dictionary<string, List<Recommendation>>();

                foreach (var patternProperty in startWordProperty.Value.EnumerateObject())
                {
                    var pattern = patternProperty.Name;
                    var recommendations = ParseRecommendations(patternProperty.Value);
                    patterns[pattern] = recommendations;
                }

                _secondWordCache[startWord] = patterns;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse second word cache JSON: {ex.Message}");
        }
    }

    private List<Recommendation> ParseRecommendations(JsonElement recommendationsElement)
    {
        var recommendations = new List<Recommendation>();

        foreach (var recommendation in recommendationsElement.EnumerateArray())
        {
            try
            {
                var word = recommendation.GetProperty("word").GetString()?.ToLower() ?? "";
                var score = recommendation.GetProperty("score").GetDouble();
                var isPossibleAnswer = recommendation.TryGetProperty("isPossibleAnswer", out var isPossibleAnswerElement)
                    ? isPossibleAnswerElement.GetBoolean()
                    : false;

                // Calculate remaining answers (approximation for cached data)
                var possibleAnswers = _allWords.Where(w => w.IsPossibleAnswer).ToList();
                var totalAnswers = possibleAnswers.Count;

                recommendations.Add(new Recommendation(word, score, totalAnswers, isPossibleAnswer));
            }
            catch
            {
                continue; // Skip malformed recommendations
            }
        }

        return recommendations;
    }

    /// <summary>
    /// Check if the service has been initialized with word data
    /// </summary>
    public bool IsInitialized => _allWords.Any();

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
    /// </summary>
    public List<Recommendation> GetRecommendations(List<Guess> guesses, bool hardMode, int topN = 5, bool useMinimax = false)
    {
        // Use cached starting words if no guesses have been made
        if (guesses.Count == 0)
        {
            var cachedStartingWords = hardMode ? _cachedStartingWordsHard : _cachedStartingWordsNormal;
            if (cachedStartingWords?.Any() == true)
            {
                return cachedStartingWords.Take(topN).ToList();
            }
        }

        // Use second word cache for instant recommendations (Normal mode only)
        if (!hardMode && guesses.Count == 1 && _secondWordCache != null)
        {
            var firstGuessWord = new string(guesses[0].Letters).ToLower();
            if (_secondWordCache.TryGetValue(firstGuessWord, out var patterns))
            {
                var pattern = GeneratePatternString(guesses[0]);
                if (patterns.TryGetValue(pattern, out var cachedRecommendations))
                {
                    return cachedRecommendations.Take(topN).ToList();
                }
            }
        }

        // Get possible answers after filtering by guesses
        var possibleAnswers = _filterService.FilterWords(
            _allWords.Where(w => w.IsPossibleAnswer).Select(w => w.Word),
            guesses).ToList();

        if (possibleAnswers.Count == 0)
        {
            return new List<Recommendation>();
        }

        // Get valid guess words (apply hard mode constraints if needed)
        var validGuesses = hardMode
            ? GetHardModeValidGuesses(guesses)
            : GetCandidateWords(possibleAnswers.Count);

        var candidates = validGuesses;

        // Calculate recommendations
        if (useMinimax)
        {
            return GetRecommendationsEntropy(guesses, hardMode, topN, candidates, possibleAnswers);
        }
        else
        {
            return GetRecommendationsNormalModeMinimax(guesses, hardMode, topN, candidates, possibleAnswers);
        }
    }

    private List<string> GetCandidateWords(int possibleAnswersCount)
    {
        var candidates = new List<string>();

        if (possibleAnswersCount > 200)
        {
            // Early game: only high-quality candidates for performance
            candidates.AddRange(_highQualityCandidates.Where(word =>
                _allWords.Any(w => w.Word == word)));
        }
        else if (possibleAnswersCount > 20)
        {
            // Mid game: possible answers + top guess-only words
            candidates.AddRange(_allWords.Where(w => w.IsPossibleAnswer).Select(w => w.Word));
            candidates.AddRange(_topGuessOnlyWords.Where(word =>
                _allWords.Any(w => w.Word == word)));
        }
        else
        {
            // Late game: all words
            candidates.AddRange(_allWords.Select(w => w.Word));
        }

        return candidates.Distinct().ToList();
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

    private string GeneratePatternString(Guess guess)
    {
        return string.Concat(guess.States.Select(state => state switch
        {
            LetterState.Green => "G",
            LetterState.Yellow => "Y",
            LetterState.White => "W",
            _ => "W"
        }));
    }

    // Rest of the methods (GetRecommendationsEntropy, GetRecommendationsNormalModeMinimax, etc.)
    // remain the same as they don't deal with word loading...

    /// <summary>
    /// Get recommendations using entropy-based scoring
    /// </summary>
    private List<Recommendation> GetRecommendationsEntropy(List<Guess> guesses, bool hardMode, int topN,
        List<string> candidates, List<string> possibleAnswers)
    {
        var recommendations = new List<Recommendation>();

        foreach (var word in candidates)
        {
            var entropy = CalculateEntropy(word, possibleAnswers);
            var isPossibleAnswer = _allWords.FirstOrDefault(w => w.Word == word)?.IsPossibleAnswer ?? false;
            recommendations.Add(new Recommendation(word, entropy, possibleAnswers.Count, isPossibleAnswer));
        }

        return recommendations.OrderByDescending(r => r.Score).Take(topN).ToList();
    }

    /// <summary>
    /// Get recommendations using minimax scoring (optimized for normal mode)
    /// </summary>
    public List<Recommendation> GetRecommendationsNormalModeMinimax(List<Guess> guesses, int topN = 5)
    {
        return GetRecommendationsNormalModeMinimax(guesses, false, topN, null, null);
    }

    private List<Recommendation> GetRecommendationsNormalModeMinimax(List<Guess> guesses, bool hardMode, int topN,
        List<string>? candidates = null, List<string>? possibleAnswers = null)
    {
        candidates ??= GetCandidateWords(possibleAnswers?.Count ?? _allWords.Count(w => w.IsPossibleAnswer));
        possibleAnswers ??= _filterService.FilterWords(
            _allWords.Where(w => w.IsPossibleAnswer).Select(w => w.Word), guesses).ToList();

        var recommendations = new List<Recommendation>();

        foreach (var word in candidates)
        {
            var score = CalculateMinimaxScore(word, possibleAnswers);
            var isPossibleAnswer = _allWords.FirstOrDefault(w => w.Word == word)?.IsPossibleAnswer ?? false;
            recommendations.Add(new Recommendation(word, score, possibleAnswers.Count, isPossibleAnswer));
        }

        return recommendations.OrderByDescending(r => r.Score).Take(topN).ToList();
    }

    private double CalculateEntropy(string guess, List<string> possibleAnswers)
    {
        var groups = possibleAnswers.GroupBy(answer => GetPattern(guess, answer));
        var totalCount = possibleAnswers.Count;

        return groups.Sum(group =>
        {
            var probability = (double)group.Count() / totalCount;
            return probability * Math.Log2(1.0 / probability);
        });
    }

    private double CalculateMinimaxScore(string guess, List<string> possibleAnswers)
    {
        if (!possibleAnswers.Any()) return 0;

        var groups = possibleAnswers.GroupBy(answer => GetPattern(guess, answer));
        var maxGroupSize = groups.Max(group => group.Count());

        return possibleAnswers.Count - maxGroupSize;
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
}