# Wordle System Rewrite - Task List

**Created**: January 30, 2026
**Implementation Date**: Sunday, February 1, 2026
**Status**: Ready for Sunday implementation session

## Task Overview

Complete system rewrite to enable NY Times word reuse rule (starting Feb 2nd, game #1691) while maintaining historical accuracy for games 1-1690.

---

## Task 1: Create historical-words.csv from used-words.csv
**Status**: Pending (ready for Sunday)
**Priority**: High - Foundation task
**Description**: Copy the current used-words.csv to create historical-words.csv for games 1-1690. This preserves all historical Wordle word assignments for backward compatibility with historical puzzles. The file should remain immutable after creation.

**Current State**: used-words.csv contains 1,686 entries through game #1686 (JUMBO, 1/30/2026)
**Action**: `cp used-words.csv historical-words.csv`
**Validation**: Verify file contains all games 1-1686, ready for games 1687-1690

---

## Task 2: Consolidate hint files into unified-hints.csv
**Status**: Pending
**Priority**: High - Required for hint system
**Description**: Merge all existing hint files (word_hints_historical.csv, word-hints.csv, 55ee0527d71c36d8-wordle-hints.csv) into a single unified-hints.csv. Prioritize word_hints_historical.csv as primary source, then add missing entries from other files. This creates one comprehensive hint source for all games.

**Source Files**:
- `word_hints_historical.csv` (143KB) - Primary comprehensive source
- `word-hints.csv` (4.1KB) - Recent/curated additions
- `55ee0527d71c36d8-wordle-hints.csv` - Source for new words
- `word-synonyms.csv` - Additional mappings

**Output**: Single `unified-hints.csv` with format: word,synonym,haiku
**Validation**: Verify all historical words have hints, no duplicates

---

## Task 3: Create initial todays-word.json structure
**Status**: Pending
**Priority**: Medium - Needed for new system
**Description**: Create the JSON structure for todays-word.json containing gameNumber, word, date, hints (synonym, haiku), and recentUsedWords array. This will be the fast-loading data source for current games starting with game 1691.

**JSON Structure**:
```json
{
  "gameNumber": 1691,
  "word": "TRUCK",
  "date": "2026-02-03",
  "hints": {
    "synonym": "vehicle",
    "haiku": "Heavy wheels rolling / Through streets and highways it goes / Cargo in its bed"
  },
  "recentUsedWords": ["TRUCK"]
}
```
**Purpose**: Fast loading for current games, enables word reuse tracking
**Validation**: JSON validates, contains required fields

---

## Task 4: Rewrite WordleStrategyService.cs for new file structure
**Status**: Pending
**Priority**: Critical - Core game logic
**Description**: Complete rewrite of the WordleStrategyService to use new file structure. Remove old CSV parsing logic, implement JSON-based loading for todays-word.json, add historical-words.csv loading for historical games, and simplify hint lookup to use unified-hints.csv only.

**Key Changes**:
- Remove old LoadUsedWords() CSV parsing logic
- Add LoadHistoricalWords() method for games ≤1690
- Add LoadTodaysWord() method for current games ≥1691
- Simplify hint loading to unified-hints.csv only
- Remove conditional date/game number checking
- Maintain WordEntry classification system

**Files**: `Services/WordleStrategyService.cs`
**Validation**: Historical games work, current games load JSON, hints work

---

## Task 5: Rewrite Home.razor LoadWordLists method
**Status**: Pending
**Priority**: Critical - UI integration
**Description**: Complete rewrite of the LoadWordLists() method in Home.razor to use new file structure. Implement logic to load todays-word.json for current games and historical-words.csv for historical games. Remove old conditional logic and CSV parsing.

**Key Changes**:
- Update LoadWordLists() method (lines 342-404)
- Remove old used-words.csv loading logic
- Add game number detection and appropriate file loading
- Simplify hint loading to unified-hints.csv only
- Remove conditional parsing logic

**Files**: `Pages/Home.razor`
**Validation**: UI loads correctly, word lists populate, hints display

---

## Task 6: Rewrite add-word.mjs automation script
**Status**: Pending
**Priority**: Critical - Daily automation
**Description**: Complete rewrite of add-word.mjs to generate todays-word.json instead of updating used-words.csv. Implement logic for fresh word reuse tracking starting Feb 2nd. Remove old CSV update logic and implement JSON generation with word, hints, and recentUsedWords tracking.

**Key Changes**:
- Remove used-words.csv update logic
- Add todays-word.json generation with complete structure
- Implement recentUsedWords array tracking (fresh start Feb 2nd)
- Continue Git integration for automated commits
- Maintain hint lookup from unified sources

**Files**: `scripts/add-word.mjs`
**Validation**: Generates valid JSON, Git commits work, hints included

---

## Task 7: Test complete system with historical and current games
**Status**: Pending
**Priority**: Critical - Quality assurance
**Description**: Comprehensive testing of the rewritten system. Verify historical games (≤1690) work correctly with historical-words.csv, test current games load todays-word.json properly, validate hint lookups from unified-hints.csv, and ensure word reuse functionality works as intended.

**Test Cases**:
- **Historical games**: Random games 1-1690 load correct words from historical-words.csv
- **Current games**: todays-word.json loads properly for games ≥1691
- **Hints**: All games can lookup hints from unified-hints.csv
- **Performance**: Recommendations generate in <50ms
- **Word reuse**: System allows previously used words for games ≥1691
- **Automation**: add-word.mjs generates valid todays-word.json

**Validation Criteria**:
- 100% historical accuracy for games 1-1690
- Fast loading for current games
- All words have hints available
- Word reuse functionality enabled
- Daily automation works correctly

---

## Implementation Order (Sunday Feb 1st)

### Phase 1: File Preparation (Tasks 1-3)
1. Create historical-words.csv (Task 1)
2. Consolidate hints into unified-hints.csv (Task 2)
3. Create sample todays-word.json structure (Task 3)

### Phase 2: Code Rewrite (Tasks 4-6)
4. Rewrite WordleStrategyService.cs (Task 4)
5. Rewrite Home.razor LoadWordLists (Task 5)
6. Rewrite add-word.mjs automation (Task 6)

### Phase 3: Testing & Validation (Task 7)
7. Comprehensive system testing (Task 7)

## Success Criteria

- **Zero downtime** during transition
- **100% historical accuracy** for games 1-1690
- **<50ms recommendation speed** maintained
- **Daily automation** works on Monday Feb 2nd
- **Word reuse** enabled for games ≥1691
- **Clean codebase** with no conditional logic

## Files for Reference

- **Plan Document**: `WORDLE_REWRITE_PLAN.md`
- **Current used-words**: `wwwroot/used-words.csv` (1,686 entries through game #1686)
- **Task List**: `WORDLE_REWRITE_TASKS.md` (this file)

Ready to resume implementation on Sunday, February 1st, 2026.