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

function loadExistingTodaysWord() {
    const todaysWordPath = join(__dirname, '../wwwroot/todays-word.json');

    if (!existsSync(todaysWordPath)) {
        return null;
    }

    try {
        const content = readFileSync(todaysWordPath, 'utf-8');
        return JSON.parse(content);
    } catch (error) {
        console.log(`Warning: Could not parse existing todays-word.json: ${error.message}`);
        return null;
    }
}

function findWordHints(word) {
    const hintsPath = join(__dirname, '../wwwroot/unified-hints.csv');

    if (!existsSync(hintsPath)) {
        console.log('Warning: unified-hints.csv not found');
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

async function addWord(word, dateStr = null) {
    // Validate word
    word = word.trim().toUpperCase();
    if (word.length !== 5) {
        throw new Error(`Word must be exactly 5 letters. Got: ${word} (${word.length} letters)`);
    }

    if (!/^[A-Z]{5}$/.test(word)) {
        throw new Error(`Word must contain only letters. Got: ${word}`);
    }

    // Load existing today's word data
    const existingData = loadExistingTodaysWord();

    // Determine date and game number
    let targetDate;
    let gameNumber;

    if (dateStr) {
        // User specified a date
        targetDate = parseDate(dateStr);
        gameNumber = calculateGameNumber(targetDate);
    } else {
        // Default to next game number after existing data or today
        if (existingData) {
            gameNumber = existingData.gameNumber + 1;
            console.log(`Defaulting to next game after #${existingData.gameNumber}`);
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

    console.log(`\\nGenerating todays-word.json:`);
    console.log(`  Word: ${word}`);
    console.log(`  Date: ${dateFormatted}`);
    console.log(`  Game: #${gameNumber}\\n`);

    // Check if this is the same word for the same game (skip if so)
    if (existingData && existingData.gameNumber === gameNumber && existingData.word === word.toUpperCase()) {
        console.log(`✓ Word "${word}" already exists for game #${gameNumber}`);
        console.log(`  No changes made.`);
        return false;
    }

    // Find word hints
    const hints = findWordHints(word);

    if (hints.synonym && hints.haiku) {
        console.log(`📝 Found hints for "${word}":`);
        console.log(`   Synonym: ${hints.synonym}`);
        console.log(`   Haiku:   ${hints.haiku}`);
    } else {
        console.log(`⚠️  No hints found for "${word}"`);
    }

    // Determine recent used words list
    let recentUsedWords;

    if (gameNumber >= WORD_REUSE_START_GAME) {
        // For games 1691+: Build recentUsedWords list allowing reuse
        if (existingData && existingData.recentUsedWords) {
            recentUsedWords = [...existingData.recentUsedWords];
            // Add current word if not already in the list
            if (!recentUsedWords.includes(word)) {
                recentUsedWords.push(word);
            }
        } else {
            // First word of the reuse era
            recentUsedWords = [word];
        }
    } else {
        // For historical games (≤1690): Single word tracking
        recentUsedWords = [word];
    }

    // Create today's word JSON object
    const todaysWordData = {
        gameNumber: gameNumber,
        word: word,
        date: dateFormatted,
        hints: {
            synonym: hints.synonym,
            haiku: hints.haiku
        },
        recentUsedWords: recentUsedWords
    };

    // Write todays-word.json
    const todaysWordPath = join(__dirname, '../wwwroot/todays-word.json');
    writeFileSync(todaysWordPath, JSON.stringify(todaysWordData, null, 2), 'utf-8');
    console.log(`✓ Generated todays-word.json`);

    // Add word to words.txt if not already there
    let addedToWordsTxt = false;
    try {
        const wordsTxtPath = join(__dirname, '../wwwroot/words.txt');
        const wordsTxtContent = readFileSync(wordsTxtPath, 'utf-8');
        const wordsArray = wordsTxtContent.trim().split('\\n').map(w => w.trim().toLowerCase());

        // Check if word already exists
        if (!wordsArray.includes(word.toLowerCase())) {
            // Add and sort alphabetically
            wordsArray.push(word.toLowerCase());
            wordsArray.sort();

            // Write back to file
            writeFileSync(wordsTxtPath, wordsArray.join('\\n') + '\\n', 'utf-8');
            console.log(`✓ Added "${word}" to words.txt (was missing)`);
            addedToWordsTxt = true;
        } else {
            console.log(`  "${word}" already in words.txt`);
        }
    } catch (error) {
        console.log(`⚠️  Could not update words.txt: ${error.message}`);
    }

    // Show summary
    console.log(`\\n${'='.repeat(60)}`);
    console.log(`📋 SUMMARY`);
    console.log(`${'='.repeat(60)}`);
    console.log(`Word:        ${word}`);
    console.log(`Date:        ${dateFormatted}`);
    console.log(`Game:        #${gameNumber}`);
    console.log(`Word Reuse:  ${gameNumber >= WORD_REUSE_START_GAME ? 'ENABLED' : 'DISABLED'}`);
    console.log(`Generated:   todays-word.json${addedToWordsTxt ? ', updated words.txt' : ''}`);
    if (hints.synonym && hints.haiku) {
        console.log(`Synonym:     ${hints.synonym}`);
        console.log(`Haiku:       ${hints.haiku}`);
    } else {
        console.log(`Hints:       (not found in unified-hints.csv)`);
    }
    console.log(`Recent Used: [${recentUsedWords.join(', ')}]`);
    console.log(`${'='.repeat(60)}\\n`);

    // Always commit
    try {
        console.log(`Committing changes...`);

        // Stage the files we've modified
        const filesToCommit = ['wwwroot/todays-word.json'];
        if (addedToWordsTxt) {
            filesToCommit.push('wwwroot/words.txt');
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
Wordle Word Adder - Generate todays-word.json for the new word reuse system

Usage:
  node add-word.mjs <word> [date]

Arguments:
  word         5-letter word to add (required)
  date         Date in YYYY-MM-DD or MM/DD/YYYY format (optional)
               If omitted, automatically uses next sequential game number

Examples:
  node add-word.mjs TRUCK
    Generates todays-word.json for next game number

  node add-word.mjs TRUCK 2026-02-03
    Generates todays-word.json for February 3, 2026

  npm run add TRUCK
    Same as above, but easier to remember

Important Changes (Feb 2, 2026):
  - Words can now be reused starting with game #${WORD_REUSE_START_GAME}
  - Generates todays-word.json instead of updating used-words.csv
  - Tracks recentUsedWords array for the new era
  - Maintains hint lookup from unified-hints.csv

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