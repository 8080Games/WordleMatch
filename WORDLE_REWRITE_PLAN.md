# Wordle System Rewrite - Implementation Plan

**Date Created**: January 30, 2026
**Implementation Date**: Sunday, February 1, 2026
**Go-Live Date**: Monday, February 2, 2026

## Executive Summary

The NY Times is dropping the restriction on word reuse starting February 2nd, 2026 (game #1691). This requires a complete system rewrite to separate historical word tracking (games 1-1690) from current word management (games 1691+), enabling word reuse while maintaining historical accuracy.

**Approach**: Complete system rewrite on Sunday Feb 1st - no conditional logic or backward compatibility layers.

---

## Current System Analysis

### Word Data Files (wwwroot/)
- **`words.txt`** (14KB, 2,341 words) - Valid answer words, alphabetically sorted
- **`guess-only-words.txt`** (63KB, ~10,000 words) - Valid guesses but never solutions
- **`used-words.csv`** (34KB, 1,686 entries) - Historical puzzle tracking (WORD,GAME_NUMBER,DATE)
- **`word-hints.csv`** (4.1KB) - Recent word hints (word,synonym,haiku)
- **`word_hints_historical.csv`** (143KB) - Comprehensive historical hints
- **`55ee0527d71c36d8-wordle-hints.csv`** - Source hints file for add-word.mjs
- **`word-synonyms.csv`** - Additional word-synonym mappings
- **Performance cache files** - starting-words.json, high-quality-words.json, second-word-cache.json (1.3MB total)

### Core Code Files
- **`Pages/Home.razor`** - LoadWordLists() method (lines 342-404), LoadHints() method
- **`Services/WordleStrategyService.cs`** - Initialize() method, LoadUsedWords() method (lines 215-246)
- **`Services/WordleFilterService.cs`** - Pattern matching and validation
- **`scripts/add-word.mjs`** - Daily word addition automation
- **`scripts/fetch-and-add-word.py`** - Daily fetching script (7:01 AM EST)
- **`.github/workflows/daily-wordle-update.yml`** - GitHub Actions automation

### Current Data Flow
1. Daily automation fetches word at 7:01 AM EST
2. `add-word.mjs` updates used-words.csv, words.txt, word-hints.csv
3. `Home.razor` loads word lists via HTTP.GetStringAsync()
4. `WordleStrategyService` processes into WordEntry objects with IsPossibleAnswer flags
5. LoadUsedWords() marks historical words as no longer possible answers
6. Performance caches provide instant recommendations

---

## Requirements & Changes

### NY Times Rule Change
- **What**: Dropping restriction on re-use of previously used words
- **When**: Monday, February 2nd, 2026 (game #1691)
- **Impact**: Words can now repeat across different game numbers

### System Requirements
- **Historical Accuracy**: Games 1-1690 must work exactly as originally played
- **Word Reuse**: Games 1691+ can repeat any words from the valid word lists
- **Performance**: Maintain current speed (<50ms recommendations)
- **Automation**: Daily word fetching must continue seamlessly
- **Clean Architecture**: No conditional date logic or compatibility layers

---

## New System Architecture

### File Structure (Post-Rewrite)
```
wwwroot/
├── historical-words.csv     # Games 1-1690 (immutable archive)
├── unified-hints.csv        # All word hints (consolidated from all sources)
├── todays-word.json        # Current game data (generated daily)
├── words.txt               # Valid answer words (unchanged)
├── guess-only-words.txt    # Valid guess words (unchanged)
└── [cache files remain]    # Performance caches (unchanged)
```

### todays-word.json Structure
```json
{
  "gameNumber": 1691,
  "word": "TRUCK",
  "date": "2026-02-03",
  "hints": {
    "synonym": "vehicle",
    "haiku": "Heavy wheels rolling / Through streets and highways it goes / Cargo in its bed"
  },
  "recentUsedWords": ["TRUCK"]  // Fresh tracking starting Feb 2nd
}
```

### Game Logic Separation
- **Historical games (≤1690)**: Use `historical-words.csv` + `unified-hints.csv`
- **Current games (≥1691)**: Use `todays-word.json` + `unified-hints.csv`
- **Word reuse**: Allowed for games 1691+ as per NY Times rule change

---

## Implementation Tasks

### Task 1: Create historical-words.csv
**Current Status**: Games 1-1686 in used-words.csv (through JUMBO, 1/30/2026)
**Action**: Copy `used-words.csv` → `historical-words.csv`
**Notes**: File handles games 1-1690, currently goes to 1686, space for 4 more games before cutoff

### Task 2: Consolidate hint files into unified-hints.csv
**Sources to merge**:
1. `word_hints_historical.csv` (143KB) - Primary comprehensive source
2. `word-hints.csv` (4.1KB) - Recent/curated additions
3. `55ee0527d71c36d8-wordle-hints.csv` - Source for new words
4. `word-synonyms.csv` - Additional mappings

**Logic**: Start with historical file, add missing entries from others, create single CSV

### Task 3: Create initial todays-word.json structure
**Purpose**: Fast-loading daily word data for games 1691+
**Content**: gameNumber, word, date, hints object, recentUsedWords array
**Generation**: Will be created by rewritten add-word.mjs

### Task 4: Rewrite WordleStrategyService.cs
**Changes**:
- Remove old CSV parsing logic for used words
- Add `LoadHistoricalWords()` method for games ≤1690
- Add `LoadTodaysWord()` method for current games
- Simplify hint loading to use unified-hints.csv only
- Remove conditional date/game number checking
- Maintain WordEntry classification system

### Task 5: Rewrite Home.razor LoadWordLists()
**Changes**:
- Remove old used-words.csv loading
- Add logic to load appropriate data source based on game number:
  - Historical games: Load historical-words.csv
  - Current games: Load todays-word.json
- Simplify hint loading to unified-hints.csv only
- Remove conditional parsing logic

### Task 6: Rewrite add-word.mjs automation
**Changes**:
- Remove used-words.csv update logic
- Add todays-word.json generation
- Implement fresh recentUsedWords tracking for games 1691+
- Continue Git integration for automated commits
- Maintain hint lookup from unified source

### Task 7: Test complete system
**Historical game testing**:
- Verify games 1-1690 load correct words from historical-words.csv
- Confirm hints work from unified-hints.csv
- Test random historical games for accuracy

**Current game testing**:
- Verify todays-word.json loads properly
- Test word reuse functionality
- Confirm hint lookups work
- Validate performance is maintained

---

## Implementation Steps (Sunday Feb 1st)

### Step 1: File Preparation
1. **Backup current state**: Create backup of entire wwwroot directory
2. **Create historical-words.csv**: Copy current used-words.csv
3. **Consolidate hints**: Merge all hint files into unified-hints.csv
4. **Create sample todays-word.json**: For testing purposes

### Step 2: Code Rewrite
1. **WordleStrategyService.cs**: Complete rewrite for new file structure
2. **Home.razor**: Rewrite LoadWordLists() method
3. **add-word.mjs**: Complete rewrite for JSON generation

### Step 3: Testing & Validation
1. **Historical accuracy**: Test random games 1-1690
2. **Current functionality**: Test with sample todays-word.json
3. **Performance**: Ensure <50ms recommendation times
4. **Automation**: Test add-word.mjs JSON generation

### Step 4: Deployment Preparation
1. **Clear used-words.csv**: Remove old tracking data
2. **Final testing**: End-to-end system validation
3. **Monitor readiness**: Prepare for Monday automation

---

## Benefits of New Architecture

### Performance Benefits
- **Fast Loading**: JSON provides instant access to current game data
- **Reduced Parsing**: No large CSV processing for current games
- **Single Hint Source**: Eliminates multiple file lookups

### Maintenance Benefits
- **Clean Separation**: Historical vs current logic clearly separated
- **Simpler Code**: No conditional date/game logic
- **Immutable History**: Historical games use fixed data files
- **Future-Proof**: Easy to extend JSON with additional daily data

### Operational Benefits
- **Seamless Automation**: Daily scripts continue working
- **Easy Rollback**: Can restore original files if needed
- **Word Reuse**: Enables NY Times rule change
- **Historical Accuracy**: Past puzzles work exactly as originally played

---

## Risk Mitigation

### High Risk Items
- **Daily automation failure**: Have manual backup process documented
- **Historical game breakage**: Comprehensive regression testing planned
- **Performance issues**: Pre-generated caches remain unchanged

### Rollback Plan
- **Complete backup**: Full wwwroot directory backed up before changes
- **Git revert**: Can revert all code changes quickly
- **File restoration**: Can restore original used-words.csv and remove new files

### Success Metrics
- **Zero downtime** during transition
- **100% historical accuracy** for games 1-1690
- **<50ms recommendations** maintained
- **Daily automation** continues working on Monday

---

## Monday Feb 2nd - Go Live

### Expected Workflow
1. **7:01 AM EST**: Daily automation runs as usual
2. **Game #1691**: First game allowing word reuse
3. **todays-word.json**: Generated with first entry in recentUsedWords
4. **Monitoring**: Verify new system works correctly
5. **Word reuse**: Functionality enabled as per NY Times rules

### Key Files After Implementation
- **`historical-words.csv`** - Permanent archive (games 1-1690)
- **`unified-hints.csv`** - Single comprehensive hint source
- **`todays-word.json`** - Daily generated current game data
- **`used-words.csv`** - Cleared/minimal for fresh start
- **All other files** - Unchanged (words.txt, guess-only-words.txt, caches)

---

## Next Steps for Sunday Session

1. **Resume this conversation** and work through Task 1-7 in order
2. **Start with file preparation** (Tasks 1-3)
3. **Proceed to code rewrite** (Tasks 4-6)
4. **Complete with testing** (Task 7)
5. **Deploy before Monday** for seamless transition

This plan provides the complete roadmap for the Wordle system rewrite to enable word reuse while preserving historical accuracy.