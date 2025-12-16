import { readFileSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const WORDLE_START_DATE = new Date('2021-06-19');

function calculateGameNumber(date) {
    const daysDiff = Math.floor((date - WORDLE_START_DATE) / (1000 * 60 * 60 * 24));
    return daysDiff;
}

function parseDate(dateStr) {
    // Validate format first
    const formats = [
        /^\d{4}-\d{2}-\d{2}$/,  // YYYY-MM-DD
        /^\d{1,2}\/\d{1,2}\/\d{4}$/  // M/D/YYYY or MM/DD/YYYY
    ];

    const matchesFormat = formats.some(format => format.test(dateStr));
    if (!matchesFormat) {
        throw new Error(`Invalid date format: "${dateStr}". Use YYYY-MM-DD or MM/DD/YYYY`);
    }

    // Try parsing
    let date = new Date(dateStr);
    if (isNaN(date.getTime())) {
        throw new Error(`Invalid date: "${dateStr}". Date does not exist.`);
    }

    // Check if date is reasonable for Wordle (after June 19, 2021)
    if (date < WORDLE_START_DATE) {
        throw new Error(`Date ${dateStr} is before Wordle started (June 19, 2021)`);
    }

    // Warn if date is too far in the future (more than 1 year)
    const oneYearFromNow = new Date();
    oneYearFromNow.setFullYear(oneYearFromNow.getFullYear() + 1);
    if (date > oneYearFromNow) {
        console.log(`⚠️  WARNING: Date ${dateStr} is more than 1 year in the future`);
    }

    return date;
}

async function addWord(word, dateStr = null, autoCommit = false) {
    // Validate word
    word = word.trim().toUpperCase();
    if (word.length !== 5) {
        throw new Error(`Word must be exactly 5 letters. Got: ${word} (${word.length} letters)`);
    }

    if (!/^[A-Z]{5}$/.test(word)) {
        throw new Error(`Word must contain only letters. Got: ${word}`);
    }

    // Read existing CSV first to determine default date
    const csvPath = join(__dirname, '../wwwroot/used-words.csv');
    const csvContent = readFileSync(csvPath, 'utf-8');
    const lines = csvContent.trim().split('\n');

    // Parse existing entries
    const entries = [];
    for (const line of lines) {
        const parts = line.split(',');
        if (parts.length >= 3) {
            const existingWord = parts[0].trim();
            const existingGameNumber = parseInt(parts[1].trim());
            const existingDate = parts[2].trim();
            entries.push({ word: existingWord, gameNumber: existingGameNumber, date: existingDate });
        }
    }

    // Determine date and game number
    let targetDate;
    let gameNumber;

    if (dateStr) {
        // User specified a date
        targetDate = parseDate(dateStr);
        gameNumber = calculateGameNumber(targetDate);
    } else {
        // Default to next game number after the last entry
        if (entries.length > 0) {
            const maxGameNumber = Math.max(...entries.map(e => e.gameNumber));
            gameNumber = maxGameNumber + 1;
            // Calculate date from game number
            targetDate = new Date(WORDLE_START_DATE);
            targetDate.setDate(targetDate.getDate() + gameNumber);
            console.log(`  Defaulting to next game after #${maxGameNumber}`);
        } else {
            // No entries yet, default to tomorrow
            targetDate = new Date();
            targetDate.setDate(targetDate.getDate() + 1);
            gameNumber = calculateGameNumber(targetDate);
            console.log(`  No entries found, defaulting to tomorrow`);
        }
    }

    const dateFormatted = targetDate.toLocaleDateString('en-US', {
        month: 'numeric',
        day: 'numeric',
        year: 'numeric'
    });

    console.log(`\nAdding word to used-words.csv:`);
    console.log(`  Word: ${word}`);
    console.log(`  Date: ${dateFormatted}`);
    console.log(`  Game: #${gameNumber}\n`);

    // Check for duplicates - be strict
    const duplicateByWord = entries.find(e => e.word === word);
    const duplicateByGame = entries.find(e => e.gameNumber === gameNumber);

    // Exact duplicate - already exists
    if (duplicateByWord && duplicateByGame && duplicateByWord.gameNumber === gameNumber) {
        console.log(`✓ Word "${word}" already exists for game #${gameNumber}`);
        console.log(`  No changes made.`);
        return false;
    }

    // Date/game conflict - different word for same game
    if (duplicateByGame && duplicateByGame.word !== word) {
        throw new Error(
            `Game #${gameNumber} (${dateFormatted}) already has word "${duplicateByGame.word}".\n` +
            `  Cannot add "${word}" for the same date.\n` +
            `  Each date/game can only have one word.`
        );
    }

    // Word already used for different date
    if (duplicateByWord && duplicateByWord.gameNumber !== gameNumber) {
        throw new Error(
            `Word "${word}" was already used in game #${duplicateByWord.gameNumber} (${duplicateByWord.date}).\n` +
            `  Cannot reuse the same word for game #${gameNumber} (${dateFormatted}).\n` +
            `  Each word can only be used once.`
        );
    }

    // Add new entry
    entries.push({ word, gameNumber, date: dateFormatted });

    // Sort by game number
    entries.sort((a, b) => a.gameNumber - b.gameNumber);

    // Write back to file
    const newContent = entries.map(e => `${e.word},${e.gameNumber},${e.date}`).join('\n') + '\n';
    writeFileSync(csvPath, newContent, 'utf-8');

    console.log(`✓ Added "${word}" to used-words.csv`);

    // Commit if requested
    if (autoCommit) {
        try {
            console.log(`\nCommitting changes...`);
            execSync('git add wwwroot/used-words.csv', { cwd: join(__dirname, '..'), stdio: 'inherit' });

            const commitMessage = `Add Wordle word: ${word} (game #${gameNumber}, ${dateFormatted})`;
            execSync(`git commit -m "${commitMessage}"`, { cwd: join(__dirname, '..'), stdio: 'inherit' });

            console.log(`\nPushing to remote...`);
            execSync('git push', { cwd: join(__dirname, '..'), stdio: 'inherit' });

            console.log(`✓ Changes committed and pushed`);
        } catch (error) {
            console.error(`\n❌ Error during git operations:`, error.message);
            console.log(`   Changes saved to file but not committed`);
            return true;
        }
    }

    return true;
}

// Main execution
async function main() {
    const args = process.argv.slice(2);

    if (args.length === 0 || args.includes('--help') || args.includes('-h')) {
        console.log(`
Wordle Word Adder - Manually add words to used-words.csv

Usage:
  node add-word.mjs <word> [date] [--commit]

Arguments:
  word         5-letter word to add (required)
  date         Date in YYYY-MM-DD or MM/DD/YYYY format (optional, defaults to tomorrow)
  --commit     Automatically commit and push changes (optional)

Examples:
  node add-word.mjs TRUCK
    Adds TRUCK for tomorrow's date

  node add-word.mjs TRUCK 2025-12-16
    Adds TRUCK for December 16, 2025

  node add-word.mjs TRUCK 12/16/2025 --commit
    Adds TRUCK for December 16, 2025 and commits/pushes changes

  node add-word.mjs TRUCK --commit
    Adds TRUCK for tomorrow and commits/pushes changes
`);
        process.exit(0);
    }

    const word = args[0];
    let date = null;
    let autoCommit = false;

    // Parse remaining arguments
    for (let i = 1; i < args.length; i++) {
        if (args[i] === '--commit') {
            autoCommit = true;
        } else if (!date) {
            date = args[i];
        }
    }

    try {
        const added = await addWord(word, date, autoCommit);

        if (added && !autoCommit) {
            console.log(`\nNext steps:`);
            console.log(`  git add wwwroot/used-words.csv`);
            console.log(`  git commit -m "Add Wordle word: ${word.toUpperCase()}"`);
            console.log(`  git push`);
            console.log(`\nOr run with --commit flag to do this automatically.`);
        }

        process.exit(0);
    } catch (error) {
        console.error(`\n❌ Error: ${error.message}\n`);
        process.exit(1);
    }
}

main();
