import { readFileSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';
import * as readline from 'readline';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const WORDLE_START_DATE = new Date('2021-06-19');

function calculateGameNumber(date) {
    const daysDiff = Math.floor((date - WORDLE_START_DATE) / (1000 * 60 * 60 * 24));
    return daysDiff;
}

function promptUser(question) {
    const rl = readline.createInterface({
        input: process.stdin,
        output: process.stdout
    });

    return new Promise((resolve) => {
        rl.question(question, (answer) => {
            rl.close();
            resolve(answer.trim());
        });
    });
}

async function addWordHints(word, synonym, haiku) {
    const hintsPath = join(__dirname, '../wwwroot/word-hints.csv');

    // Read existing hints
    let entries = [];
    let hasHeader = false;

    try {
        const content = readFileSync(hintsPath, 'utf-8');
        const lines = content.trim().split('\n');

        for (const line of lines) {
            if (line.startsWith('word,')) {
                hasHeader = true;
                continue; // Skip header
            }
            const parts = line.split(',', 3);
            if (parts.length >= 3) {
                entries.push({
                    word: parts[0].trim(),
                    synonym: parts[1].trim(),
                    haiku: parts[2].trim().replace(/^"|"$/g, '') // Remove surrounding quotes
                });
            }
        }
    } catch (e) {
        // File doesn't exist yet, will create it
        console.log('  Creating new word-hints.csv file');
    }

    // Check if hint already exists
    const existingIndex = entries.findIndex(e => e.word.toLowerCase() === word.toLowerCase());
    if (existingIndex >= 0) {
        console.log(`  Updating existing hint for ${word}`);
        entries[existingIndex] = { word, synonym, haiku };
    } else {
        entries.push({ word, synonym, haiku });
    }

    // Sort by word
    entries.sort((a, b) => a.word.localeCompare(b.word));

    // Write back to file
    const lines = [];
    if (!hasHeader) {
        lines.push('word,synonym,haiku');
    } else {
        lines.push('word,synonym,haiku');
    }

    for (const entry of entries) {
        // Escape haiku properly (it contains commas and slashes)
        const escapedHaiku = `"${entry.haiku.replace(/"/g, '""')}"`;
        lines.push(`${entry.word},${entry.synonym},${escapedHaiku}`);
    }

    writeFileSync(hintsPath, lines.join('\n') + '\n', 'utf-8');
    console.log(`✓ Added hints for "${word}" to word-hints.csv`);
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

async function addWord(word, dateStr = null) {
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

    // Auto-load hints from source file
    let hintsAdded = false;
    let synonym = '';
    let haiku = '';

    try {
        const hintsSourcePath = join(__dirname, '../wwwroot/55ee0527d71c36d8-wordle-hints.csv');
        const hintsSourceContent = readFileSync(hintsSourcePath, 'utf-8');
        const lines = hintsSourceContent.split('\n');

        for (const line of lines) {
            if (line.startsWith('word,')) continue; // Skip header

            const parts = line.split(',', 3);
            if (parts.length >= 3) {
                const hintWord = parts[0].trim().toLowerCase();
                if (hintWord === word.toLowerCase()) {
                    synonym = parts[1].trim();
                    haiku = parts[2].trim().replace(/^"|"$/g, ''); // Remove surrounding quotes

                    console.log(`\n📝 Found hints for "${word}" in source file`);
                    await addWordHints(word, synonym, haiku);
                    hintsAdded = true;
                    break;
                }
            }
        }

        if (!hintsAdded) {
            console.log(`\n⚠️  No hints found for "${word}" in source file`);
        }
    } catch (error) {
        console.log(`\n⚠️  Could not load hints source file: ${error.message}`);
    }

    // Show summary
    console.log(`\n${'='.repeat(60)}`);
    console.log(`📋 SUMMARY`);
    console.log(`${'='.repeat(60)}`);
    console.log(`Word:     ${word}`);
    console.log(`Date:     ${dateFormatted}`);
    console.log(`Game:     #${gameNumber}`);
    if (hintsAdded) {
        console.log(`Synonym:  ${synonym}`);
        console.log(`Haiku:    ${haiku}`);
    } else {
        console.log(`Hints:    (none)`);
    }
    console.log(`${'='.repeat(60)}\n`);

    // Always commit
    try {
        console.log(`Committing changes...`);
        execSync('git add wwwroot/used-words.csv wwwroot/word-hints.csv', { cwd: join(__dirname, '..'), stdio: 'inherit' });

        const commitMessage = `Add Wordle word: ${word} (game #${gameNumber}, ${dateFormatted})`;
        execSync(`git commit -m "${commitMessage}"`, { cwd: join(__dirname, '..'), stdio: 'inherit' });

        console.log(`\nPushing to remote...`);
        execSync('git push', { cwd: join(__dirname, '..'), stdio: 'inherit' });

        console.log(`\n✅ SUCCESS! Changes committed and pushed\n`);
    } catch (error) {
        console.error(`\n❌ Error during git operations:`, error.message);
        console.log(`   Changes saved to files but not committed\n`);
        return true;
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
  node add-word.mjs <word> [date]

Arguments:
  word         5-letter word to add (required)
  date         Date in YYYY-MM-DD or MM/DD/YYYY format (optional)
               If omitted, automatically uses next sequential game number

Examples:
  node add-word.mjs TRUCK
    Adds TRUCK for next game number (auto-detects from file)

  node add-word.mjs TRUCK 2025-12-16
    Adds TRUCK for December 16, 2025

  npm run add TRUCK
    Same as above, but easier to remember

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
        console.error(`\n❌ Error: ${error.message}\n`);
        process.exit(1);
    }
}

main();
