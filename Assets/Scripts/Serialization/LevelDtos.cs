using System;

namespace GateRush.Serialization
{
    // ---------------------------------------------------------------------------
    // Wire schema — one worked example
    //
    // Read this before the nine types below. One example is faster to absorb
    // than nine class definitions.
    //
    // Every enum is a name, never an integer: "Red", not 3 — an inserted enum
    // value silently renumbers integer files, a name cannot be silently wrong.
    // Every nullable int is -1 when absent: JsonUtility cannot write int?, and
    // every such field in Core is non-negative when present. Any other negative
    // is a structural error, not a second spelling of "none". A nullable colour
    // ("requiredColor") is "" when absent. Elevator waves are wrapped one level
    // deep ("waves": [ { "blocks": [ ... ] } ]) because JsonUtility cannot write
    // a jagged array.
    //
    // A spawned block in an elevator wave carries a region-relative position:
    // "hasRegionOrigin": true with "regionOrigin": { "x": .., "y": .. }. An
    // explicit flag, not a -1 sentinel, because a region origin has no sign
    // convention to lean on. Generator output has no authored position, so it
    // writes "hasRegionOrigin": false and the "regionOrigin" object is ignored.
    // This position (and formatVersion 2) arrived with the Level Editor, which
    // needs it because a region usually admits several tilings.
    //
    // {
    //   "formatVersion": 2,
    //   "levelId": 42,
    //   "width": 5,
    //   "height": 5,
    //   "staticWalls": [ { "x": 0, "y": 4 } ],
    //   "blocks": [
    //     {
    //       "id": 1,
    //       "cells": [ { "x": 0, "y": 0 }, { "x": 0, "y": 1 } ],
    //       "colorStack": [ "Blue", "Yellow" ],
    //       "startOrigin": { "x": 2, "y": 1 },
    //       "axis": "VerticalOnly",
    //       "unfreezeAtClearCount": 3,          // set:   unfreezes at 3 clears
    //       "lockId": -1,                       // absent: this block has no lock
    //       "requiredKeyCount": 0,
    //       "keyTargetLockId": -1,              // absent: this block has no key
    //       "keyEffect": "UnlockMovement",
    //       "timeBonusSeconds": 0
    //     }
    //   ],
    //   "gates": [
    //     { "id": 1, "edge": "Bottom", "offset": 2, "width": 1,
    //       "color": "Blue", "openAtClearCount": -1 }
    //   ],
    //   "shutters": [
    //     { "id": 1, "min": { "x": 3, "y": 3 }, "max": { "x": 4, "y": 4 },
    //       "threshold": 2, "requiredColor": "Yellow" }   // colour-bound shutter
    //   ],
    //   "generators": [
    //     { "id": 1, "edge": "Top", "offset": 0,
    //       "queue": [ { "cells": [ { "x": 0, "y": 0 } ], "colorStack": [ "Red" ],
    //                    "axis": "Free", "unfreezeAtClearCount": -1, "lockId": -1,
    //                    "requiredKeyCount": 0, "keyTargetLockId": -1,
    //                    "keyEffect": "UnlockMovement", "timeBonusSeconds": 0,
    //                    "hasRegionOrigin": false, "regionOrigin": { "x": 0, "y": 0 } } ] }
    //   ],
    //   "elevators": [
    //     { "id": 1, "min": { "x": 0, "y": 0 }, "max": { "x": 1, "y": 0 },
    //       "waves": [
    //         // wave 1 tiles the 2x1 region with two 1x1 blocks
    //         { "blocks": [ { ...spawned, "hasRegionOrigin": true, "regionOrigin": { "x": 0, "y": 0 } },
    //                       { ...spawned, "hasRegionOrigin": true, "regionOrigin": { "x": 1, "y": 0 } } ] },
    //         // wave 2 tiles the same region with one 2x1 block — waves differ in
    //         // block count, never in the cells they cover
    //         { "blocks": [ { ...spawned, "cells": [ {"x":0,"y":0}, {"x":1,"y":0} ],
    //                         "hasRegionOrigin": true, "regionOrigin": { "x": 0, "y": 0 } } ] }
    //       ] }
    //   ],
    //   "suggestedTimeBudgetSeconds": 90,
    //   "goldReward": 250
    // }
    //
    // Nothing precomputed is written: SpecAt, MaxResolutionPasses, the lock/key
    // lookups and the occupancy map are all rebuilt by LevelContext's
    // constructor. Persisting them would only create a way for them to go stale.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Root of a serialized level. <see cref="formatVersion"/> is first and is
    /// checked before any other field is read: a file whose version this build
    /// does not understand is refused outright rather than misread.
    /// </summary>
    /// <remarks>
    /// Every DTO in this file is a data carrier, not an object: all fields are
    /// public, because <c>JsonUtility</c> ignores properties. This is the
    /// exception <c>CONVENTIONS.md</c> calls out to its "never public fields"
    /// rule.
    /// </remarks>
    [Serializable]
    public sealed class LevelDto
    {
        public int formatVersion;
        public int levelId;
        public int width;
        public int height;
        public CoordDto[] staticWalls;
        public BlockDto[] blocks;
        public GateDto[] gates;
        public ShutterDto[] shutters;
        public GeneratorDto[] generators;
        public ElevatorDto[] elevators;
        public int suggestedTimeBudgetSeconds;
        public int goldReward;
    }

    /// <summary>
    /// A grid coordinate as an object rather than parallel arrays, so a JSON
    /// reader sees <c>{ "x": 2, "y": 1 }</c> and a length mismatch between an
    /// x-list and a y-list is structurally impossible.
    /// </summary>
    [Serializable]
    public struct CoordDto
    {
        public int x;
        public int y;
    }

    /// <summary>A top-level block: like <see cref="SpawnedBlockDto"/> but with an id and a start position.</summary>
    [Serializable]
    public sealed class BlockDto
    {
        public int id;
        public CoordDto[] cells;
        public string[] colorStack;
        public CoordDto startOrigin;
        public string axis;
        public int unfreezeAtClearCount;
        public int lockId;
        public int requiredKeyCount;
        public int keyTargetLockId;
        public string keyEffect;
        public int timeBonusSeconds;
    }

    /// <summary>An edge opening. <see cref="openAtClearCount"/> is <c>-1</c> when the gate opens from the start.</summary>
    [Serializable]
    public sealed class GateDto
    {
        public int id;
        public string edge;
        public int offset;
        public int width;
        public string color;
        public int openAtClearCount;
    }

    /// <summary>
    /// A rectangular shutter region. <see cref="requiredColor"/> is <c>""</c> for
    /// a global shutter and a colour name for a colour-bound one.
    /// </summary>
    [Serializable]
    public sealed class ShutterDto
    {
        public int id;
        public CoordDto min;
        public CoordDto max;
        public int threshold;
        public string requiredColor;
    }

    /// <summary>A generator and its ordered output queue.</summary>
    [Serializable]
    public sealed class GeneratorDto
    {
        public int id;
        public string edge;
        public int offset;
        public SpawnedBlockDto[] queue;
    }

    /// <summary>An elevator and its ordered waves, each wave wrapped by a <see cref="WaveDto"/>.</summary>
    [Serializable]
    public sealed class ElevatorDto
    {
        public int id;
        public CoordDto min;
        public CoordDto max;
        public WaveDto[] waves;
    }

    /// <summary>
    /// One elevator wave. Exists only because <c>JsonUtility</c> cannot serialize
    /// a jagged collection: <c>ElevatorDto.waves</c> must be an array of objects,
    /// not an array of arrays.
    /// </summary>
    [Serializable]
    public sealed class WaveDto
    {
        public SpawnedBlockDto[] blocks;
    }

    /// <summary>
    /// A block awaiting delivery by a generator or elevator. Generator output
    /// derives its position from the edge and offset and sets
    /// <see cref="hasRegionOrigin"/> false. An elevator wave block sets it true
    /// and fills <see cref="regionOrigin"/> with the grid cell, relative to the
    /// region's <c>Min</c>, that its footprint's minimum corner occupies (M9) —
    /// a region usually admits several tilings, so the wave has to say which.
    /// </summary>
    [Serializable]
    public sealed class SpawnedBlockDto
    {
        public CoordDto[] cells;
        public string[] colorStack;
        public string axis;
        public int unfreezeAtClearCount;
        public int lockId;
        public int requiredKeyCount;
        public int keyTargetLockId;
        public string keyEffect;
        public int timeBonusSeconds;

        /// <summary>
        /// Whether <see cref="regionOrigin"/> carries a value. An explicit flag
        /// rather than a <c>-1</c> sentinel, because a region origin has no sign
        /// convention that <c>-1</c> could safely fall outside.
        /// </summary>
        public bool hasRegionOrigin;

        /// <summary>
        /// The region-relative position of an elevator wave block. Read only
        /// when <see cref="hasRegionOrigin"/> is true.
        /// </summary>
        public CoordDto regionOrigin;
    }
}
