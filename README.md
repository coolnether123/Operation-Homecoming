# Operation Homecoming - RimWorld 1.5 Mod

A comprehensive RimWorld mod that transforms kidnapping events into dynamic rescue missions with skill-based contact systems and emergent storytelling.

## Implemented Feature Scope

The 1.5 package contains the recovered `OH_*` incident, quest, site, and
letter definitions and the production lifecycle: kidnapped pawn tracking,
contact, rescue quest and camp creation, successful rescue, expiry, wounded
return, and return raid. A world component persists the lifecycle records and
queued returns across save/reload.

The repository does not contain a historical reward table, loyalty algorithm,
or a detailed prison-break design. Accordingly, successful rescue returns the
pawn and sends the documented success letter; no invented currency reward or
betrayal rule is claimed. `OH_PrisonBreak` and `OH_Investigation` are declared
extension seams and are not presented as richer gameplay than the recovered
source supports.

### Core Systems
- **CompKidnapContactTracker**: Automatically tracks kidnapped pawns and manages contact attempts
- **Skill-Based Contact**: Success chances based on pawn's highest skill (30% base + 10% per 4 skill levels, capped at 80%)
- **Configurable Settings**: Full mod settings menu with sliders for all timing and chance parameters
- **Signal System**: Event-driven architecture for handling contact success/failure

### Quest System
- **Rescue Quests**: Automatically generated when contact succeeds
- **Kidnap Camp Sites**: Procedurally generated fortified camps with guards
- **Investigation Sites**: Optional sites that provide intel bonuses
- **Quest Expiry**: Time-limited rescue windows (default 30 days)

### Contact Flavors (Skill-Based)
- **Animals**: Bonded animal courier
- **Social**: Caravan smuggled note  
- **Intellectual**: Hacked communications burst
- **Artistic**: Coded art trade
- **Crafting**: Knotted item on road
- **Construction**: Structural flaw note
- **Mining**: Ore chunk message
- **Cooking**: Mass food poisoning
- **Medicine**: Indebted prisoner
- **Combat**: Hijacked guard radio

### Post-Failure Paths
- **Return Raids**: Kidnapped pawn returns as part of attacking force (60% chance)
- **Wounded Return**: Pawn returns alone with injuries and skill penalties (40% chance)
- **Loyalty System**: Mid-combat betrayal chances for returned pawns

### Rumor System
- **Periodic Updates**: Rumors about missing pawns every 4 days
- **Investigation Sites**: Every 3rd failed contact spawns investigation opportunity
- **Mood Impact**: Provides hope or despair based on rumor content

## File Structure

```
/Core/
├── OperationHomecoming.cs          # Main mod class and incident workers
├── ModSettings_OperationHomecoming.cs  # Configurable settings
├── CompKidnapContactTracker.cs     # Pawn component for tracking
├── OHQuestUtility.cs              # Quest creation utilities
├── KidnapCampSiteComp.cs          # World object component for camps
├── SignalHandlers.cs              # Event handling system
├── SitePartWorkers.cs             # Map generation for sites
├── OHIncidentWorker_ReturnRaid.cs # Return raid mechanics
├── HarmonyPatches.cs              # Game integration patches
└── DefModExtensions.cs            # Data structure extensions

/Defs/
├── IncidentDefs/                  # Incident definitions
├── QuestScriptDefs/               # Quest script definitions
├── SitePartDefs/                  # Site part definitions
├── LetterDefs/                    # Custom letter types
├── ThingDefs/                     # Component definitions
└── DefModExtensions/              # Extension definitions

/Languages/English/Keyed/
└── OperationHomecoming.xml        # Translation keys
```

## Configuration Options

All settings are configurable in the mod options menu:

### Contact Timing
- First contact delay: 3-7 days (configurable 1-20 days)
- Contact attempt interval: 5 days (configurable 2-14 days)

### Success Chances  
- Base success chance: 30% (configurable 5-90%)
- Skill bonus per 4 levels: 10% (configurable 5-20%)
- Maximum success chance: 80% (configurable 30-100%)

### Quest Settings
- Rumor frequency: 4 days (configurable 1-10 days)
- Camp strength factor: 0.7x (configurable 0.4x-1.5x)
- Rescue window: 30 days (configurable 10-90 days)

### Features
- Prison break mini-game: Enabled (toggle)
- Return raid chance: 60% (configurable 0-100%)
- Illness severity multiplier: 1.0x (configurable 0-3x)

## Installation

1. Subscribe to the mod on Steam Workshop or download manually
2. Ensure Harmony is installed and loaded before this mod
3. Add to your mod list and start a new game or load existing save
4. Configure settings in Options > Mod Settings > Operation Homecoming

## Compatibility

- **RimWorld Version**: 1.5 (verified for the current package)
- **Dependencies**: Harmony (required)
- **Save Compatibility**: 1.5 save/reload and fresh-process continuation were
  verified with a rescued pawn and persisted Homecoming record
- **Mod Compatibility**: Only Harmony load order and Operation Homecoming's own
  Harmony ownership are verified; broader combinations are untested

## Technical Notes

- Uses Harmony patches for seamless game integration
- Event-driven architecture prevents performance issues
- Modular design allows easy expansion of features
- Comprehensive error handling and logging
- Follows RimWorld modding best practices

## Developer Fixture

`Developer/OperationHomecoming.TestFixture` is a separately loadable 1.5
developer mod. It is intentionally excluded from the shipping package and
contains the only debug actions used for deterministic lifecycle setup,
settings bounds, UI capture, queued-return processing, and expiry checks. The
shipping assembly contains no `DebugAction` methods.

The fixture is not a Better Work Tab dependency and Better Work Tab was not
loaded or run during verification.

## Future Expansion

The mod is designed for easy expansion with:
- Additional contact flavors for modded skills
- New post-failure outcome types
- Enhanced prison break mechanics
- Integration with other quest mods
- Faction-specific kidnapping behaviors

## Credits

- Design Document: Based on Operation Homecoming v0.8 specification
- Implementation: AI-assisted development
- RimWorld: Ludeon Studios
- Harmony: Andreas Pardeike
