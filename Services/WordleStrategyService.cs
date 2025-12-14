using static WordleHelper.Services.WordleFilterService;

namespace WordleHelper.Services;

public class WordleStrategyService
{
    private List<WordEntry> _allWords;
    private readonly WordleFilterService _filterService;
    private List<Recommendation>? _cachedStartingWordsNormal;
    private List<Recommendation>? _cachedStartingWordsHard;
    private Dictionary<string, (int gameNumber, string date)> _usedWords = new();

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
    public void LoadUsedWords(Dictionary<string, (int gameNumber, string date)> usedWords)
    {
        _usedWords = usedWords;

        // Reclassify any used words that were marked as possible answers
        foreach (var wordEntry in _allWords)
        {
            if (wordEntry.IsPossibleAnswer && _usedWords.ContainsKey(wordEntry.Word))
            {
                wordEntry.IsPossibleAnswer = false;
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

        // Get valid guess words (apply hard mode constraints if needed)
        // All words (including used words) can be used as guesses
        var validGuesses = hardMode
            ? GetHardModeValidGuesses(guesses)
            : _allWords.Select(w => w.Word).ToList();

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
    /// Calculates expected information gain (entropy) for a guess
    /// Higher score = more information gained on average
    /// </summary>
    private double CalculateExpectedInformation(string guess, List<string> possibleAnswers)
    {
        // Group possible answers by the pattern they would produce
        var patternGroups = possibleAnswers
            .GroupBy(answer => GetPattern(guess, answer))
            .ToList();

        // Calculate entropy: -Σ(p * log2(p))
        double entropy = 0;
        int total = possibleAnswers.Count;

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
}
