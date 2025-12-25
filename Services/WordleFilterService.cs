namespace WordleHelper.Services;

public class WordleFilterService
{
    public enum LetterState
    {
        None,
        White,
        Yellow,
        Green
    }

    public class Guess
    {
        public char[] Letters { get; set; }
        public LetterState[] States { get; set; }

        public Guess(char[] letters, LetterState[] states)
        {
            if (letters.Length != 5 || states.Length != 5)
                throw new ArgumentException("Letters and states must have exactly 5 elements");

            Letters = letters;
            States = states;
        }
    }

    public IEnumerable<string> FilterWords(IEnumerable<string> allWords, List<Guess> guesses)
    {
        return allWords.Where(word => MatchesPattern(word, guesses)).OrderBy(w => w);
    }

    public bool MatchesPattern(string word, List<Guess> guesses)
    {
        foreach (var guess in guesses)
        {
            if (!MatchesGuess(word, guess))
            {
                return false;
            }
        }
        return true;
    }

    public bool MatchesGuess(string word, Guess guess)
    {
        // Count green/yellow instances of each letter
        var greenYellowCount = new Dictionary<char, int>();
        var whiteLetters = new HashSet<char>();

        for (int i = 0; i < 5; i++)
        {
            if (guess.Letters[i] == ' ' || guess.States[i] == LetterState.None)
                continue;

            char letter = char.ToLower(guess.Letters[i]);

            if (guess.States[i] == LetterState.Green || guess.States[i] == LetterState.Yellow)
            {
                if (!greenYellowCount.ContainsKey(letter))
                    greenYellowCount[letter] = 0;
                greenYellowCount[letter]++;
            }
            else if (guess.States[i] == LetterState.White)
            {
                whiteLetters.Add(letter);
            }
        }

        // Check position constraints
        for (int i = 0; i < 5; i++)
        {
            if (guess.States[i] == LetterState.Green)
            {
                if (word[i] != char.ToLower(guess.Letters[i]))
                {
                    return false;
                }
            }
            else if (guess.States[i] == LetterState.Yellow)
            {
                char letter = char.ToLower(guess.Letters[i]);
                if (!word.Contains(letter))
                {
                    return false;
                }
                if (word[i] == letter)
                {
                    return false;
                }
            }
        }

        // Check frequency constraints for white letters
        foreach (char letter in whiteLetters)
        {
            int actualCount = word.Count(c => c == letter);

            if (greenYellowCount.ContainsKey(letter))
            {
                // There are green/yellow instances, so word should have exactly that many
                if (actualCount != greenYellowCount[letter])
                {
                    return false;
                }
            }
            else
            {
                // No green/yellow instances, so word should not contain this letter at all
                if (actualCount > 0)
                {
                    return false;
                }
            }
        }

        // Check minimum frequency for green/yellow letters without white instances
        foreach (var (letter, minCount) in greenYellowCount)
        {
            if (!whiteLetters.Contains(letter))
            {
                // No white instance means word should have AT LEAST this many
                int actualCount = word.Count(c => c == letter);
                if (actualCount < minCount)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
