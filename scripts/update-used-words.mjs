import { readFileSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

/**
 * Parses Wordle answer from HTML content
 * @param {string} html - The HTML content to parse
 * @param {string} targetDate - The target date in M/D/YYYY format
 * @param {Function} cheerioLoad - The cheerio.load function (for dependency injection in tests)
 * @returns {Object|null} - Object with {word, gameNumber, date} or null if not found
 */
export function parseWordleAnswerFromHtml(html, targetDate, cheerioLoad = null) {
    // If cheerioLoad is not provided, this will fail - but that's ok for testing
    // In production, it will be provided by fetchLatestWordleAnswer
    if (!cheerioLoad) {
        throw new Error('cheerioLoad function is required');
    }
    const $ = cheerioLoad(html);

    // Try to find the most recent answer
    let latestWord = null;
    let latestGameNumber = null;
    let latestDate = targetDate;

    // Look for patterns like "Wordle #1234" or "December 10, 2025"
    // This is a heuristic approach that may need refinement
    const text = $('body').text();

    console.log(`Looking for answer from ${targetDate}`);

    // Strategy 1: Parse the page for answer pattern
    // This is a simplified approach - you may need to adjust based on actual page structure
    const wordPattern = /Wordle\s*#?(\d+).*?answer.*?is\s*([A-Z]{5})/i;
    const match = text.match(wordPattern);

    if (match) {
        latestGameNumber = match[1];
        latestWord = match[2].toLowerCase();

        console.log(`Found: Word="${latestWord}", Game#${latestGameNumber}, Date=${latestDate}`);

        return {
            word: latestWord,
            gameNumber: parseInt(latestGameNumber),
            date: latestDate
        };
    }

    // Strategy 2: Try to find structured data in tables
    const tables = $('table');
    if (tables.length > 0) {
        // Look for the first row in a table (usually the most recent)
        const firstRow = tables.first().find('tr').eq(1);
        if (firstRow.length > 0) {
            const cells = firstRow.find('td');
            if (cells.length >= 2) {
                latestWord = cells.eq(1).text().trim().toLowerCase();
                const gameText = cells.eq(0).text().trim();
                const gameMatch = gameText.match(/(\d+)/);
                if (gameMatch) {
                    latestGameNumber = gameMatch[1];
                }

                console.log(`Found from table: Word="${latestWord}", Game#${latestGameNumber}, Date=${latestDate}`);

                return {
                    word: latestWord,
                    gameNumber: parseInt(latestGameNumber),
                    date: latestDate
                };
            }
        }
    }

    // Strategy 3: Try to find list items with format: <li><strong>Day Month (#XXXX):</strong> <a>WORD</a></li>
    // Find the item with the HIGHEST game number (most recent)
    const listItems = $('li');
    let highestGameNumber = 0;
    let highestWord = null;

    for (let i = 0; i < listItems.length; i++) {
        const li = $(listItems[i]);
        const strongText = li.find('strong').first().text();
        const linkText = li.find('a').first().text().trim();

        // Look for pattern like "Thursday 11 December (#1636):"
        const listPattern = /\(#(\d+)\)/;
        const match = strongText.match(listPattern);

        if (match && linkText && linkText.length === 5) {
            const gameNum = parseInt(match[1]);
            if (gameNum > highestGameNumber) {
                highestGameNumber = gameNum;
                highestWord = linkText.toLowerCase();
            }
        }
    }

    if (highestWord) {
        console.log(`Found from list: Word="${highestWord}", Game#${highestGameNumber}, Date=${latestDate}`);

        return {
            word: highestWord,
            gameNumber: highestGameNumber,
            date: latestDate
        };
    }

    return null;
}

/**
 * Fetches the latest Wordle answer from Rock Paper Shotgun
 * @returns {Promise<Object>} - Object with {word, gameNumber, date}
 */
async function fetchLatestWordleAnswer() {
    try {
        // Import dynamically to avoid issues
        const fetch = (await import('node-fetch')).default;
        const cheerio = await import('cheerio');

        const url = 'https://www.rockpapershotgun.com/wordle-past-answers';
        console.log('Fetching Wordle answers from Rock Paper Shotgun...');

        const response = await fetch(url);
        const html = await response.text();

        // Try to find today's or yesterday's date
        const yesterday = new Date();
        yesterday.setDate(yesterday.getDate() - 1);
        const dateStr = yesterday.toLocaleDateString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric' });

        const result = parseWordleAnswerFromHtml(html, dateStr, cheerio.load);

        if (!result) {
            throw new Error('Could not find latest Wordle answer on the page');
        }

        return result;

    } catch (error) {
        console.error('Error fetching Wordle answer:', error.message);
        throw error;
    }
}

async function updateUsedWordsCsv(newWord) {
    const csvPath = join(__dirname, '../wwwroot/used-words.csv');

    try {
        // Read existing CSV
        const csvContent = readFileSync(csvPath, 'utf-8');
        const lines = csvContent.trim().split('\n');

        // Check if word already exists
        const wordExists = lines.some(line => {
            const word = line.split(',')[0].trim().toLowerCase();
            return word === newWord.word;
        });

        if (wordExists) {
            console.log(`Word "${newWord.word}" already exists in used-words.csv`);
            return false;
        }

        // Add new word at the beginning (reverse chronological order)
        const newLine = `${newWord.word.toUpperCase()},${newWord.gameNumber},${newWord.date}`;
        lines.unshift(newLine);

        // Write back to file
        writeFileSync(csvPath, lines.join('\n') + '\n', 'utf-8');
        console.log(`Added "${newWord.word}" to used-words.csv`);
        return true;

    } catch (error) {
        console.error('Error updating CSV:', error.message);
        throw error;
    }
}

// Main execution
async function main() {
    try {
        console.log('Starting Wordle used words update...');

        const latestAnswer = await fetchLatestWordleAnswer();

        if (!latestAnswer) {
            console.error('Failed to fetch latest answer');
            process.exit(1);
        }

        const updated = await updateUsedWordsCsv(latestAnswer);

        if (updated) {
            console.log('Successfully updated used-words.csv');
        } else {
            console.log('No update needed');
        }

    } catch (error) {
        console.error('Failed to update used words:', error);
        process.exit(1);
    }
}

main();
