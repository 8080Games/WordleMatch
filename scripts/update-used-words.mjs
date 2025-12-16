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
 * Fetches the latest Wordle answer from New York Times
 * @returns {Promise<Object>} - Object with {word, gameNumber, date}
 */
async function fetchLatestWordleAnswer() {
    try {
        const { chromium } = await import('playwright');

        // Get credentials from environment variables
        const nytEmail = process.env.NYT_EMAIL;
        const nytPassword = process.env.NYT_PASSWORD;

        if (!nytEmail || !nytPassword) {
            throw new Error('NYT_EMAIL and NYT_PASSWORD environment variables are required');
        }

        // Calculate tomorrow's game number
        // NYT posts the review article the day BEFORE the puzzle, but uses tomorrow's puzzle number
        const today = new Date();
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);

        const WORDLE_START_DATE = new Date('2021-06-19');
        const gameNumber = Math.floor((tomorrow - WORDLE_START_DATE) / (1000 * 60 * 60 * 24));

        const dateStr = tomorrow.toLocaleDateString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric' });

        // Construct NYT URL - uses TODAY's date but TOMORROW's game number
        const year = today.getFullYear();
        const month = String(today.getMonth() + 1).padStart(2, '0');
        const day = String(today.getDate()).padStart(2, '0');
        const url = `https://www.nytimes.com/${year}/${month}/${day}/crosswords/wordle-review-${gameNumber}.html`;

        console.log(`Fetching Wordle answer from New York Times...`);
        console.log(`  Date: ${dateStr} (tomorrow)`);
        console.log(`  Game: #${gameNumber}`);
        console.log(`  URL: ${url}`);
        console.log(`  Authenticating with NYT account...`);

        // Launch browser with stealth settings
        const browser = await chromium.launch({
            headless: true,
            args: [
                '--disable-blink-features=AutomationControlled',
                '--disable-dev-shm-usage',
                '--no-sandbox',
                '--disable-setuid-sandbox',
                '--disable-infobars',
                '--window-position=0,0',
                '--ignore-certifcate-errors',
                '--ignore-certifcate-errors-spki-list'
            ]
        });

        const context = await browser.newContext({
            userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36',
            viewport: { width: 1920, height: 1080 },
            locale: 'en-US',
            timezoneId: 'America/New_York',
            extraHTTPHeaders: {
                'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8',
                'Accept-Language': 'en-US,en;q=0.9',
                'Accept-Encoding': 'gzip, deflate, br',
                'DNT': '1',
                'Connection': 'keep-alive',
                'Upgrade-Insecure-Requests': '1',
                'Sec-Fetch-Dest': 'document',
                'Sec-Fetch-Mode': 'navigate',
                'Sec-Fetch-Site': 'none',
                'Sec-Fetch-User': '?1',
                'Cache-Control': 'max-age=0'
            }
        });

        const page = await context.newPage();

        // Hide webdriver property
        await page.addInitScript(() => {
            Object.defineProperty(navigator, 'webdriver', {
                get: () => false
            });
        });

        // Login to NYT
        console.log('  Logging in to NYT...');
        await page.goto('https://myaccount.nytimes.com/auth/login', {
            waitUntil: 'networkidle',
            timeout: 30000
        });

        // Wait longer for JavaScript to render the form
        console.log('  Waiting for login form to load...');
        await page.waitForTimeout(5000);

        // Wait a bit more for iframes to load
        await page.waitForTimeout(3000);

        // Get all frames
        const frames = page.frames();
        console.log(`  Found ${frames.length} frames on page`);

        // Log frame details for debugging
        for (let i = 0; i < frames.length; i++) {
            const frame = frames[i];
            const frameUrl = frame.url();
            const frameName = frame.name() || '(unnamed)';
            console.log(`    Frame ${i}: ${frameName} - ${frameUrl}`);
        }

        // Try to find email input in all frames
        const emailSelectors = [
            '#email',
            'input[type="email"]',
            'input[name="email"]',
            'input[id="email"]'
        ];

        let emailInput = null;
        let targetFrame = page;

        // Try each frame
        for (let i = 0; i < frames.length; i++) {
            if (emailInput) break;

            const frame = frames[i];
            console.log(`  Checking frame ${i} for email input...`);

            for (const selector of emailSelectors) {
                try {
                    emailInput = await frame.waitForSelector(selector, {
                        timeout: 2000,
                        state: 'visible'
                    });
                    if (emailInput) {
                        targetFrame = frame;
                        console.log(`  ✓ Found email input in frame ${i} with selector: ${selector}`);
                        break;
                    }
                } catch (e) {
                    // Try next selector
                }
            }
        }

        if (!emailInput) {
            // Take screenshot and save HTML for debugging
            await page.screenshot({ path: 'login-page-debug.png' });
            const html = await page.content();
            const fs = await import('fs');
            fs.writeFileSync('login-page-debug.html', html);

            // Log the page URL in case of redirect
            console.log(`  Current URL: ${page.url()}`);
            console.log(`  Page HTML saved to login-page-debug.html`);

            throw new Error('Could not find email input field. Debug files saved.');
        }

        // Fill in email using the correct frame
        await targetFrame.fill('#email', nytEmail);
        console.log('  Filled in email address');

        // Find and click submit button
        const submitSelectors = [
            'button[type="submit"]',
            'button:has-text("Continue")',
            'button:has-text("Log in")',
            'button:has-text("Next")'
        ];

        for (const selector of submitSelectors) {
            try {
                const button = await targetFrame.$(selector);
                if (button) {
                    await button.click();
                    console.log(`  Clicked submit with selector: ${selector}`);
                    break;
                }
            } catch (e) {
                // Try next selector
            }
        }

        // Wait for password field
        await page.waitForTimeout(3000);

        const passwordSelectors = [
            '#password',
            'input[type="password"]',
            'input[name="password"]',
            'input[id="password"]'
        ];

        let passwordInput = null;

        // Try main page first
        for (const selector of passwordSelectors) {
            try {
                passwordInput = await page.waitForSelector(selector, {
                    timeout: 3000,
                    state: 'visible'
                });
                if (passwordInput) {
                    targetFrame = page;
                    console.log(`  Found password input in main page with selector: ${selector}`);
                    break;
                }
            } catch (e) {
                // Try next selector
            }
        }

        // Try iframes if not found
        if (!passwordInput && frames.length > 1) {
            console.log('  Password not found in main page, checking iframes...');
            for (const frame of frames) {
                if (passwordInput) break;
                for (const selector of passwordSelectors) {
                    try {
                        passwordInput = await frame.waitForSelector(selector, {
                            timeout: 1000,
                            state: 'visible'
                        });
                        if (passwordInput) {
                            targetFrame = frame;
                            console.log(`  Found password input in iframe with selector: ${selector}`);
                            break;
                        }
                    } catch (e) {
                        // Try next selector
                    }
                }
            }
        }

        if (!passwordInput) {
            await page.screenshot({ path: 'password-page-debug.png' });
            throw new Error('Could not find password input field. Screenshot saved to password-page-debug.png');
        }

        // Fill in password using the correct frame
        await targetFrame.fill('#password', nytPassword);
        console.log('  Filled in password');

        // Click login button
        for (const selector of submitSelectors) {
            try {
                const button = await targetFrame.$(selector);
                if (button) {
                    await button.click();
                    console.log(`  Clicked login with selector: ${selector}`);
                    break;
                }
            } catch (e) {
                // Try next selector
            }
        }

        // Wait for login to complete
        await page.waitForTimeout(5000);
        console.log('  Login completed!');

        // Now navigate to the Wordle review page
        console.log('  Navigating to Wordle review page...');
        const response = await page.goto(url, {
            waitUntil: 'networkidle',
            timeout: 30000
        });

        if (response.status() !== 200) {
            await browser.close();
            throw new Error(`HTTP ${response.status()}: Failed to load page`);
        }

        // Wait for content to load
        await page.waitForTimeout(2000);

        // Get page text content
        const pageText = await page.textContent('body');

        await browser.close();

        // Look for patterns like "The answer to Wordle 1641 is TRUCK"
        const patterns = [
            /answer\s+to\s+Wordle\s+\d+\s+is\s+([A-Z]{5})/i,
            /answer\s+is\s+([A-Z]{5})/i,
            /solution\s+is\s+([A-Z]{5})/i,
            /today'?s?\s+word\s+is\s+([A-Z]{5})/i,
            /\bWordle\s+\d+\s+answer[:\s]+([A-Z]{5})/i
        ];

        for (const pattern of patterns) {
            const match = pageText.match(pattern);
            if (match) {
                const word = match[1].toLowerCase();
                console.log(`  Found word: ${word.toUpperCase()}`);

                return {
                    word,
                    gameNumber,
                    date: dateStr
                };
            }
        }

        throw new Error('Could not find answer on NYT page');

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
        console.log('');

        const latestAnswer = await fetchLatestWordleAnswer();

        if (!latestAnswer) {
            console.log('');
            console.log('❌ RESULT: FAILED - Could not find Wordle answer on website');
            console.log('');
            process.exit(1);
        }

        console.log('');
        const updated = await updateUsedWordsCsv(latestAnswer);

        console.log('');
        console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
        if (updated) {
            console.log('✅ RESULT: NEW WORD ADDED');
            console.log(`   Word:   ${latestAnswer.word.toUpperCase()}`);
            console.log(`   Number: #${latestAnswer.gameNumber}`);
            console.log(`   Date:   ${latestAnswer.date}`);
        } else {
            console.log('ℹ️  RESULT: NO UPDATE NEEDED');
            console.log(`   Word:   ${latestAnswer.word.toUpperCase()}`);
            console.log(`   Number: #${latestAnswer.gameNumber}`);
            console.log(`   Date:   ${latestAnswer.date}`);
            console.log(`   Reason: Word already exists in database`);
        }
        console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
        console.log('');

    } catch (error) {
        console.log('');
        console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
        console.log('❌ RESULT: ERROR');
        console.log(`   Error: ${error.message}`);
        console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
        console.log('');
        process.exit(1);
    }
}

main();
