import { readFileSync, writeFileSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const WORDLE_START_DATE = new Date('2021-06-19'); // Game #1 started on June 19, 2021
const WORD_REUSE_START_GAME = 1691; // Game number when word reuse became allowed

function calculateGameNumber(date) {
    const daysDiff = Math.floor((date - WORDLE_START_DATE) / (1000 * 60 * 60 * 24));
    return daysDiff;
}

function parseDate(dateStr) {
    // Support multiple formats: YYYY-MM-DD or MM/DD/YYYY
    dateStr = dateStr.trim();

    // YYYY-MM-DD format
    if (/^\d{4}-\d{1,2}-\d{1,2}$/.test(dateStr)) {
        const [year, month, day] = dateStr.split('-').map(Number);
        return new Date(year, month - 1, day);
    }

    // MM/DD/YYYY format
    if (/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(dateStr)) {
        const [month, day, year] = dateStr.split('/').map(Number);
        return new Date(year, month - 1, day);
    }

    throw new Error(`Invalid date format: "${dateStr}". Use YYYY-MM-DD or MM/DD/YYYY`);
}

function loadExistingCurrentGames() {
    const currentGamesPath = join(__dirname, '../wwwroot/current-games.json');

    if (!existsSync(currentGamesPath)) {
        return { games: [], recentUsedWords: [] };
    }

    try {
        const content = readFileSync(currentGamesPath, 'utf-8');
        return JSON.parse(content);
    } catch (error) {
        console.log(`Warning: Could not parse existing current-games.json: ${error.message}`);
        return { games: [], recentUsedWords: [] };
    }
}

function findWordHints(word) {
    const hintsPath = join(__dirname, '../wwwroot/3158_wordle_hints.csv');

    if (!existsSync(hintsPath)) {
        console.log('Warning: 3158_wordle_hints.csv not found');
        return { synonym: '', haiku: '' };
    }

    try {
        const content = readFileSync(hintsPath, 'utf-8');
        const lines = content.split('\n');

        for (const line of lines) {
            if (line.startsWith('word,')) continue; // Skip header

            const parts = line.split(',', 3);
            if (parts.length >= 3) {
                const hintWord = parts[0].trim().toLowerCase();
                if (hintWord === word.toLowerCase()) {
                    return {
                        synonym: parts[1].trim(),
                        haiku: parts[2].trim().replace(/^"|"$/g, '') // Remove surrounding quotes
                    };
                }
            }
        }
    } catch (error) {
        console.log(`Warning: Could not read hints file: ${error.message}`);
    }

    return { synonym: '', haiku: '' };
}

function ensureWordInAnswers(word) {
    const wordLower = word.toLowerCase();
    let addedToWords = false;
    let removedFromGuessOnly = false;

    // Check words.txt (potential answers)
    try {
        const wordsTxtPath = join(__dirname, '../wwwroot/words.txt');
        const wordsTxtContent = readFileSync(wordsTxtPath, 'utf-8');
        const wordsArray = wordsTxtContent.trim().split('\n').map(w => w.trim().toLowerCase());

        if (!wordsArray.includes(wordLower)) {
            wordsArray.push(wordLower);
            wordsArray.sort();
            writeFileSync(wordsTxtPath, wordsArray.join('\n') + '\n', 'utf-8');
            console.log(`✓ Added "${word}" to words.txt (potential answers)`);
            addedToWords = true;
        } else {
            console.log(`  "${word}" already in words.txt`);
        }
    } catch (error) {
        console.log(`⚠️  Could not update words.txt: ${error.message}`);
    }

    // Check guess-only-words.txt and remove if present
    try {
        const guessOnlyPath = join(__dirname, '../wwwroot/guess-only-words.txt');
        if (existsSync(guessOnlyPath)) {
            const guessContent = readFileSync(guessOnlyPath, 'utf-8');
            const guessArray = guessContent.trim().split('\n').map(w => w.trim().toLowerCase());

            if (guessArray.includes(wordLower)) {
                const filtered = guessArray.filter(w => w !== wordLower);
                writeFileSync(guessOnlyPath, filtered.join('\n') + '\n', 'utf-8');
                console.log(`✓ Removed "${word}" from guess-only-words.txt`);
                removedFromGuessOnly = true;
            }
        }
    } catch (error) {
        console.log(`⚠️  Could not update guess-only-words.txt: ${error.message}`);
    }

    return { addedToWords, removedFromGuessOnly };
}

function ensureWordInHints(word, hints) {
    let addedToUnifiedHints = false;
    const hintsAreMissing = !hints.synonym && !hints.haiku;

    if (hintsAreMissing) {
        console.log(`\n⚠️  ============================================`);
        console.log(`⚠️  ALERT: No hints found for "${word}"`);
        console.log(`⚠️  You must manually add a synonym and haiku to:`);
        console.log(`⚠️    - wwwroot/3158_wordle_hints.csv`);
        console.log(`⚠️    - wwwroot/historical_hints.csv`);
        console.log(`⚠️  ============================================\n`);
        return { addedToUnifiedHints, hintsAreMissing };
    }

    // Hints found — ensure they're in historical_hints.csv
    try {
        const unifiedPath = join(__dirname, '../wwwroot/historical_hints.csv');
        if (existsSync(unifiedPath)) {
            const content = readFileSync(unifiedPath, 'utf-8');
            const wordUpper = word.toUpperCase();

            // Check if word already exists in historical_hints.csv
            const lines = content.split('\n');
            const alreadyExists = lines.some(line => line.startsWith(wordUpper + ','));

            if (!alreadyExists) {
                const entry = `${wordUpper},${hints.synonym},"${hints.haiku}"\n`;
                writeFileSync(unifiedPath, content.trimEnd() + '\n' + entry, 'utf-8');
                console.log(`✓ Added "${word}" hints to historical_hints.csv`);
                addedToUnifiedHints = true;
            } else {
                console.log(`  "${word}" hints already in historical_hints.csv`);
            }
        }
    } catch (error) {
        console.log(`⚠️  Could not update historical_hints.csv: ${error.message}`);
    }

    return { addedToUnifiedHints, hintsAreMissing };
}

async function addWord(word, dateStr = null) {
    // Validate word
    word = word.trim().toUpperCase();
    if (word.length !== 5) {
        throw new Error(`Word must be exactly 5 letters. Got: ${word} (${word.length} letters)`);
    }

    if (!/^[A-Z]{5}$/.test(word)) {
        throw new Error(`Word must contain only letters. Got: ${word}`);
    }

    // Load existing current games data
    const existingData = loadExistingCurrentGames();

    // Determine date and game number
    let targetDate;
    let gameNumber;

    if (dateStr) {
        // User specified a date
        targetDate = parseDate(dateStr);
        gameNumber = calculateGameNumber(targetDate);
    } else {
        // Default to next game number after existing data or today
        if (existingData.games && existingData.games.length > 0) {
            const lastGame = existingData.games[existingData.games.length - 1];
            gameNumber = lastGame.gameNumber + 1;
            console.log(`Defaulting to next game after #${lastGame.gameNumber}`);
        } else {
            // No existing data, default to today
            targetDate = new Date();
            gameNumber = calculateGameNumber(targetDate);
            console.log(`No existing data, defaulting to today`);
        }

        // Calculate date from game number
        targetDate = new Date(WORDLE_START_DATE);
        targetDate.setDate(targetDate.getDate() + gameNumber);
    }

    // Format date consistently
    const month = targetDate.getMonth() + 1;
    const day = targetDate.getDate();
    const year = targetDate.getFullYear();
    const dateFormatted = `${month}/${day}/${year}`;

    console.log(`\\nGenerating current-games.json:`);
    console.log(`  Word: ${word}`);
    console.log(`  Date: ${dateFormatted}`);
    console.log(`  Game: #${gameNumber}\\n`);

    // Check if this word already exists for this game number
    const existingGame = existingData.games?.find(game => game.gameNumber === gameNumber);
    if (existingGame && existingGame.word === word.toUpperCase()) {
        console.log(`✓ Word "${word}" already exists for game #${gameNumber}`);
        console.log(`  No changes made.`);
        return false;
    }

    // Ensure word is in potential answers list and not in guess-only list
    const wordListResult = ensureWordInAnswers(word);

    // Find word hints
    const hints = findWordHints(word);

    if (hints.synonym && hints.haiku) {
        console.log(`📝 Found hints for "${word}":`);
        console.log(`   Synonym: ${hints.synonym}`);
        console.log(`   Haiku:   ${hints.haiku}`);
    } else {
        console.log(`⚠️  No hints found for "${word}"`);
    }

    // Ensure hints are in historical_hints.csv (or alert if missing)
    const hintsResult = ensureWordInHints(word, hints);

    // Build recent used words list
    let recentUsedWords = existingData.recentUsedWords || [];
    if (gameNumber >= WORD_REUSE_START_GAME) {
        // For games 1691+: Add current word if not already in the list
        if (!recentUsedWords.includes(word)) {
            recentUsedWords.push(word);
        }
    }

    // Create new game entry
    const newGame = {
        gameNumber: gameNumber,
        word: word,
        date: dateFormatted,
        hints: {
            synonym: hints.synonym,
            haiku: hints.haiku
        }
    };

    // Update games array - implement sliding window with historical archiving
    let games = existingData.games || [];

    // Remove any existing entry for this game number
    games = games.filter(game => game.gameNumber !== gameNumber);

    // Add the new game
    games.push(newGame);

    // Sort by game number
    games.sort((a, b) => a.gameNumber - b.gameNumber);

    // Handle sliding window: move oldest game to historical-words.csv if we have more than 2 games
    let archivedWord = false;
    if (games.length > 2) {
        const oldestGame = games[0];
        console.log(`📚 Moving game #${oldestGame.gameNumber} (${oldestGame.word}) to historical archive`);

        try {
            // Add to historical-words.csv
            const historicalPath = join(__dirname, '../wwwroot/historical-words.csv');
            const historicalEntry = `${oldestGame.word.toUpperCase()},${oldestGame.gameNumber},${oldestGame.date}\n`;

            if (existsSync(historicalPath)) {
                const existingHistorical = readFileSync(historicalPath, 'utf-8');
                writeFileSync(historicalPath, existingHistorical + historicalEntry, 'utf-8');
            } else {
                writeFileSync(historicalPath, historicalEntry, 'utf-8');
            }

            console.log(`✓ Added to historical-words.csv: ${oldestGame.word} (game #${oldestGame.gameNumber})`);

            // Add to used-words.csv (track recently used words in new era)
            const usedWordsPath = join(__dirname, '../wwwroot/used-words.csv');
            const usedWordEntry = `${oldestGame.word.toUpperCase()},${oldestGame.gameNumber},${oldestGame.date}\n`;

            if (existsSync(usedWordsPath)) {
                const existingUsed = readFileSync(usedWordsPath, 'utf-8');
                writeFileSync(usedWordsPath, existingUsed + usedWordEntry, 'utf-8');
            } else {
                writeFileSync(usedWordsPath, usedWordEntry, 'utf-8');
            }

            console.log(`✓ Added to used-words.csv: ${oldestGame.word} (marked as recently used)`);

            // Note: Hints remain available in master file (3158_wordle_hints.csv)
            // No need to duplicate them elsewhere - lazy loaded when needed

            archivedWord = true;

            // Remove from current games array
            games = games.slice(1);
        } catch (error) {
            console.log(`⚠️  Could not update word archives: ${error.message}`);
        }
    }

    // Keep only the two most recent games for timezone coverage
    games = games.slice(-2);

    // Create current games JSON object
    const currentGamesData = {
        games: games,
        recentUsedWords: recentUsedWords
    };

    // Write current-games.json
    const currentGamesPath = join(__dirname, '../wwwroot/current-games.json');
    writeFileSync(currentGamesPath, JSON.stringify(currentGamesData, null, 2), 'utf-8');
    console.log(`✓ Generated current-games.json`);

    // Show summary
    console.log(`\\n${'='.repeat(60)}`);
    console.log(`📋 SUMMARY`);
    console.log(`${'='.repeat(60)}`);
    console.log(`Word:        ${word}`);
    console.log(`Date:        ${dateFormatted}`);
    console.log(`Game:        #${gameNumber}`);
    console.log(`Word Reuse:  ${gameNumber >= WORD_REUSE_START_GAME ? 'ENABLED' : 'DISABLED'}`);
    const updatedFiles = ['current-games.json'];
    if (wordListResult.addedToWords) updatedFiles.push('words.txt');
    if (wordListResult.removedFromGuessOnly) updatedFiles.push('guess-only-words.txt');
    if (hintsResult.addedToUnifiedHints) updatedFiles.push('historical_hints.csv');
    if (archivedWord) { updatedFiles.push('historical-words.csv'); updatedFiles.push('used-words.csv'); }
    console.log(`Updated:     ${updatedFiles.join(', ')}`);
    if (hints.synonym && hints.haiku) {
        console.log(`Synonym:     ${hints.synonym}`);
        console.log(`Haiku:       ${hints.haiku}`);
    } else {
        console.log(`Hints:       (not found in historical_hints.csv)`);
    }
    console.log(`Recent Used: [${recentUsedWords.join(', ')}]`);
    console.log(`Games File:  ${games.length} games (timezone coverage)`);
    console.log(`${'='.repeat(60)}\\n`);

    // Always commit
    try {
        console.log(`Committing changes...`);

        // Stage the files we've modified
        const filesToCommit = ['wwwroot/current-games.json'];
        if (wordListResult.addedToWords) {
            filesToCommit.push('wwwroot/words.txt');
        }
        if (wordListResult.removedFromGuessOnly) {
            filesToCommit.push('wwwroot/guess-only-words.txt');
        }
        if (hintsResult.addedToUnifiedHints) {
            filesToCommit.push('wwwroot/historical_hints.csv');
        }
        if (archivedWord) {
            filesToCommit.push('wwwroot/historical-words.csv');
            filesToCommit.push('wwwroot/used-words.csv');
        }

        execSync(`git add ${filesToCommit.join(' ')}`, { cwd: join(__dirname, '..'), stdio: 'inherit' });

        const commitMessage = `Add Wordle word: ${word} (game #${gameNumber}, ${dateFormatted})`;
        execSync(`git commit -m "${commitMessage}"`, { cwd: join(__dirname, '..'), stdio: 'inherit' });

        console.log(`\\nPushing to remote...`);
        execSync('git push', { cwd: join(__dirname, '..'), stdio: 'inherit' });

        console.log(`\\n✅ SUCCESS! Changes committed and pushed\\n`);
    } catch (error) {
        console.error(`\\n❌ Error during git operations:`, error.message);
        console.log(`   Changes saved to files but not committed\\n`);
        return true;
    }

    return true;
}

// Main execution
async function main() {
    const args = process.argv.slice(2);

    if (args.length === 0 || args.includes('--help') || args.includes('-h')) {
        console.log(`
Wordle Word Adder - Generate current-games.json for the new word reuse system

Usage:
  node add-word.mjs <word> [date]

Arguments:
  word         5-letter word to add (required)
  date         Date in YYYY-MM-DD or MM/DD/YYYY format (optional)
               If omitted, automatically uses next sequential game number

Examples:
  node add-word.mjs TRUCK
    Updates current-games.json with next game number

  node add-word.mjs TRUCK 2026-02-03
    Updates current-games.json for February 3, 2026

  npm run add TRUCK
    Same as above, but easier to remember

Important Changes (Feb 2, 2026):
  - Words can now be reused starting with game #${WORD_REUSE_START_GAME}
  - Generates current-games.json with two words for timezone coverage
  - Tracks recentUsedWords array for the new era
  - Maintains hint lookup from historical_hints.csv

Note: Changes are automatically committed and pushed to git.
`);
        process.exit(0);
    }

    const word = args[0];
    let date = null;

    // Parse remaining arguments (just the date if provided)
    if (args.length > 1) {
        date = args[1];
    }

    try {
        await addWord(word, date);
        process.exit(0);
    } catch (error) {
        console.error(`\\n❌ Error: ${error.message}\\n`);
        process.exit(1);
    }
}

main();