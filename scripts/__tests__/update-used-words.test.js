import { describe, it, expect } from 'vitest';
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import * as cheerio from 'cheerio';
import { parseWordleAnswerFromHtml } from '../update-used-words.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

describe('parseWordleAnswerFromHtml', () => {
    it('should parse answer from text pattern (Wordle #XXXX answer is WORD)', () => {
        const html = readFileSync(
            join(__dirname, 'fixtures', 'rps-with-text-pattern.html'),
            'utf-8'
        );

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result).toEqual({
            word: 'guess',
            gameNumber: 1636,
            date: '12/11/2025'
        });
    });

    it('should parse answer from table structure', () => {
        const html = readFileSync(
            join(__dirname, 'fixtures', 'rps-with-table.html'),
            'utf-8'
        );

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result).toEqual({
            word: 'guess',
            gameNumber: 1636,
            date: '12/11/2025'
        });
    });

    it('should return null when no answer is found', () => {
        const html = readFileSync(
            join(__dirname, 'fixtures', 'rps-no-answer.html'),
            'utf-8'
        );

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result).toBeNull();
    });

    it('should handle empty HTML', () => {
        const html = '';

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result).toBeNull();
    });

    it('should throw error when cheerioLoad is not provided', () => {
        const html = '<html></html>';

        expect(() => {
            parseWordleAnswerFromHtml(html, '12/11/2025', null);
        }).toThrow('cheerioLoad function is required');
    });

    it('should convert word to lowercase', () => {
        const html = '<html><body>The Wordle #1636 answer is HELLO</body></html>';

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result.word).toBe('hello');
    });

    it('should parse game number as integer', () => {
        const html = '<html><body>The Wordle #1636 answer is GUESS</body></html>';

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result.gameNumber).toBe(1636);
        expect(typeof result.gameNumber).toBe('number');
    });

    it('should preserve the target date', () => {
        const html = '<html><body>The Wordle #1636 answer is GUESS</body></html>';
        const targetDate = '1/15/2025';

        const result = parseWordleAnswerFromHtml(html, targetDate, cheerio.load);

        expect(result.date).toBe('1/15/2025');
    });

    it('should handle malformed table gracefully', () => {
        const html = `
            <html>
                <body>
                    <table>
                        <tr>
                            <td>No data here</td>
                        </tr>
                    </table>
                </body>
            </html>
        `;

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result).toBeNull();
    });

    it('should match pattern with normal spacing variations', () => {
        const html = '<html><body>The Wordle #1636 answer is GUESS today</body></html>';

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result).toEqual({
            word: 'guess',
            gameNumber: 1636,
            date: '12/11/2025'
        });
    });

    it('should match pattern without # symbol', () => {
        const html = '<html><body>Wordle 1636 answer is GUESS</body></html>';

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result).toEqual({
            word: 'guess',
            gameNumber: 1636,
            date: '12/11/2025'
        });
    });

    it('should extract word from table with extra whitespace', () => {
        const html = `
            <html>
                <body>
                    <table>
                        <tr><th>Number</th><th>Word</th></tr>
                        <tr>
                            <td>  1636  </td>
                            <td>  GUESS  </td>
                        </tr>
                    </table>
                </body>
            </html>
        `;

        const result = parseWordleAnswerFromHtml(html, '12/11/2025', cheerio.load);

        expect(result.word).toBe('guess');
    });
});
