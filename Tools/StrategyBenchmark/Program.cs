using WordleHelper.Services;
using WordleHelper.Services.Strategies;
using static WordleHelper.Services.WordleFilterService;

namespace StrategyBenchmark;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: StrategyBenchmark <start_game_number> <end_game_number> [hard_mode] [--strategy <name>]");
            Console.WriteLine("Example: StrategyBenchmark 1 100");
            Console.WriteLine("Example: StrategyBenchmark 1 100 hard");
            Console.WriteLine("Example: StrategyBenchmark 1 100 hard --strategy minimax");
            Console.WriteLine();
            Console.WriteLine("Available strategies:");
            Console.WriteLine("  entropy    - Shannon Entropy (default)");
            Console.WriteLine("  minimax    - Minimax (minimize worst case)");
            Console.WriteLine("  lookahead  - Two-step lookahead");
            return;
        }

        int startGame = int.Parse(args[0]);
        int endGame = int.Parse(args[1]);
        bool hardMode = args.Length > 2 && args[2].ToLower() == "hard";

        // Parse strategy argument
        string strategyName = "entropy"; // Default
        string wordsFile = "used-words.csv"; // Default
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--strategy" && i + 1 < args.Length)
            {
                strategyName = args[i + 1].ToLower();
            }
            if (args[i] == "--words-file" && i + 1 < args.Length)
            {
                wordsFile = args[i + 1];
            }
        }

        // Initialize services
        var filterService = new WordleFilterService();
        var strategyService = new BenchmarkStrategyService(filterService);

        // Set strategy based on command-line argument
        IWordleStrategy strategy;
        switch (strategyName)
        {
            case "minimax":
                strategy = new MinimaxStrategy();
                break;
            case "lookahead":
                strategy = new LookaheadStrategy(filterService);
                break;
            case "entropy":
            default:
                strategy = new EntropyStrategy();
                break;
        }
        strategyService.SetStrategy(strategy);

        Console.WriteLine($"Benchmarking recommendation engine:");
        Console.WriteLine($"  Games: {startGame} to {endGame}");
        Console.WriteLine($"  Mode: {(hardMode ? "HARD" : "NORMAL")}");
        Console.WriteLine($"  Strategy: {strategy.GetStrategyName()}");
        Console.WriteLine();

        // Load word lists from wwwroot (search upward from current directory)
        var wwwrootPath = FindWwwrootPath();
        if (wwwrootPath == null)
        {
            Console.WriteLine("Error: Could not find wwwroot directory");
            return;
        }

        var answerWords = File.ReadAllText(Path.Combine(wwwrootPath, "words.txt"));
        var guessOnlyWords = File.ReadAllText(Path.Combine(wwwrootPath, "guess-only-words.txt"));
        strategyService.Initialize(answerWords, guessOnlyWords);

        // Load starting words
        var startingWordsJson = File.ReadAllText(Path.Combine(wwwrootPath, "starting-words.json"));
        strategyService.LoadStartingWords(startingWordsJson);

        // Load high-quality words for Normal mode
        var highQualityWordsJson = File.ReadAllText(Path.Combine(wwwrootPath, "high-quality-words.json"));
        strategyService.LoadHighQualityWords(highQualityWordsJson);

        // Load second-word cache
        try
        {
            var secondWordCacheJson = File.ReadAllText(Path.Combine(wwwrootPath, "second-word-cache.json"));
            strategyService.LoadSecondWordCache(secondWordCacheJson);
            Console.WriteLine("✓ Second-word cache loaded");
        }
        catch
        {
            Console.WriteLine("⚠ Second-word cache not found, using live calculation");
        }

        // Load used words to get game number -> answer mapping
        var usedWordsPath = Path.Combine(wwwrootPath, wordsFile);
        var usedWords = LoadUsedWords(usedWordsPath);

        Console.WriteLine($"✓ Loaded {usedWords.Count} used words");
        Console.WriteLine();

        // Run benchmarks
        var results = new List<GameResult>();
        int gamesPlayed = 0;
        int gamesWon = 0;
        int totalGuesses = 0;

        for (int gameNumber = startGame; gameNumber <= endGame; gameNumber++)
        {
            if (!usedWords.ContainsKey(gameNumber))
            {
                Console.WriteLine($"Game {gameNumber}: No answer word found, skipping");
                continue;
            }

            var answer = usedWords[gameNumber];
            var result = PlayGame(strategyService, filterService, answer, hardMode, gameNumber);
            results.Add(result);

            gamesPlayed++;
            if (result.Won)
            {
                gamesWon++;
                totalGuesses += result.GuessCount;
            }

            // Show progress
            if (gamesPlayed % 10 == 0)
            {
                Console.WriteLine($"Progress: {gamesPlayed}/{endGame - startGame + 1} games...");
            }
        }

        // Print results
        Console.WriteLine();
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("RESULTS");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();

        Console.WriteLine($"Games Played: {gamesPlayed}");
        Console.WriteLine($"Games Won: {gamesWon} ({(double)gamesWon / gamesPlayed * 100:F1}%)");
        Console.WriteLine($"Games Lost: {gamesPlayed - gamesWon}");
        Console.WriteLine($"Average Guesses (wins only): {(double)totalGuesses / gamesWon:F2}");
        Console.WriteLine();

        // Distribution
        Console.WriteLine("Guess Distribution:");
        for (int i = 1; i <= 6; i++)
        {
            int count = results.Count(r => r.GuessCount == i);
            double pct = (double)count / gamesPlayed * 100;
            string bar = new string('█', (int)(pct / 2));
            Console.WriteLine($"  {i}: {count,4} ({pct,5:F1}%) {bar}");
        }
        int losses = results.Count(r => !r.Won);
        if (losses > 0)
        {
            double lossPct = (double)losses / gamesPlayed * 100;
            string bar = new string('█', (int)(lossPct / 2));
            Console.WriteLine($"  X: {losses,4} ({lossPct,5:F1}%) {bar}");
        }

        Console.WriteLine();

        // Show failures
        var failures = results.Where(r => !r.Won).ToList();
        if (failures.Any())
        {
            Console.WriteLine("Failed Games:");
            foreach (var failure in failures)
            {
                Console.WriteLine($"  Game {failure.GameNumber}: {failure.Answer.ToUpper()}");
                Console.WriteLine($"    Guesses: {string.Join(" → ", failure.Guesses)}");
            }
            Console.WriteLine();
        }

        // Save detailed results to CSV
        var resultsPath = $"benchmark_results_{startGame}_{endGame}_{(hardMode ? "hard" : "normal")}_{strategyName}.csv";
        SaveResults(results, resultsPath);
        Console.WriteLine($"Detailed results saved to: {resultsPath}");
    }

    static GameResult PlayGame(
        BenchmarkStrategyService strategyService,
        WordleFilterService filterService,
        string answer,
        bool hardMode,
        int gameNumber)
    {
        var guesses = new List<Guess>();
        var guessWords = new List<string>();
        int maxGuesses = 6;

        for (int attempt = 1; attempt <= maxGuesses; attempt++)
        {
            // Get recommendations
            var recommendations = strategyService.GetRecommendations(guesses, hardMode, topN: 5);

            if (recommendations == null || recommendations.Count == 0)
            {
                // No recommendations available - should not happen
                return new GameResult
                {
                    GameNumber = gameNumber,
                    Answer = answer,
                    Won = false,
                    GuessCount = 0,
                    Guesses = guessWords
                };
            }

            // Use the top recommendation
            var guessWord = recommendations[0].Word;
            guessWords.Add(guessWord);

            // Check if we got it
            if (guessWord == answer)
            {
                return new GameResult
                {
                    GameNumber = gameNumber,
                    Answer = answer,
                    Won = true,
                    GuessCount = attempt,
                    Guesses = guessWords
                };
            }

            // Simulate the guess result
            var (letters, states) = SimulateGuess(guessWord, answer);
            guesses.Add(new Guess(letters, states));
        }

        // Failed to get it in 6 guesses
        return new GameResult
        {
            GameNumber = gameNumber,
            Answer = answer,
            Won = false,
            GuessCount = maxGuesses + 1,
            Guesses = guessWords
        };
    }

    static (char[] letters, LetterState[] states) SimulateGuess(string guess, string answer)
    {
        var letters = guess.ToCharArray();
        var states = new LetterState[5];
        var answerChars = answer.ToCharArray();
        var used = new bool[5];

        // First pass: mark greens
        for (int i = 0; i < 5; i++)
        {
            if (letters[i] == answerChars[i])
            {
                states[i] = LetterState.Green;
                used[i] = true;
            }
        }

        // Second pass: mark yellows and whites
        for (int i = 0; i < 5; i++)
        {
            if (states[i] == LetterState.Green)
                continue;

            bool found = false;
            for (int j = 0; j < 5; j++)
            {
                if (!used[j] && letters[i] == answerChars[j])
                {
                    states[i] = LetterState.Yellow;
                    used[j] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                states[i] = LetterState.White;
            }
        }

        return (letters, states);
    }

    static string? FindWwwrootPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        // Search upward for wwwroot directory
        while (dir != null)
        {
            var wwwrootPath = Path.Combine(dir.FullName, "wwwroot");
            if (Directory.Exists(wwwrootPath))
            {
                return wwwrootPath;
            }
            dir = dir.Parent;
        }

        return null;
    }

    static Dictionary<int, string> LoadUsedWords(string path)
    {
        var usedWords = new Dictionary<int, string>();
        var lines = File.ReadAllLines(path);

        foreach (var line in lines.Skip(1)) // Skip header
        {
            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                var word = parts[0].Trim().ToLower();
                if (int.TryParse(parts[1].Trim(), out int gameNumber))
                {
                    usedWords[gameNumber] = word;
                }
            }
        }

        return usedWords;
    }

    static void SaveResults(List<GameResult> results, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("GameNumber,Answer,Won,GuessCount,Guesses");

        foreach (var result in results)
        {
            writer.WriteLine($"{result.GameNumber},{result.Answer},{result.Won},{result.GuessCount},\"{string.Join(",", result.Guesses)}\"");
        }
    }
}

class GameResult
{
    public int GameNumber { get; set; }
    public string Answer { get; set; } = "";
    public bool Won { get; set; }
    public int GuessCount { get; set; }
    public List<string> Guesses { get; set; } = new();
}
