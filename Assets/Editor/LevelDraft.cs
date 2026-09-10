using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Serialization;

namespace GateRush.Editor
{
    /// <summary>
    /// The mutable mirror of a level being edited. <see cref="LevelContext"/> is
    /// immutable and validates on construction; a level under edit is almost
    /// always invalid — a block placed before its gate, a grid resized before
    /// what fell outside was moved — so the editor cannot hold its working state
    /// in a type that refuses to exist unless correct. <see cref="LevelDraft"/>
    /// holds anything, valid or not, in real types (<c>int?</c>, enums,
    /// <c>List&lt;T&gt;</c>) rather than the DTO layer's <c>-1</c> sentinels and
    /// enum names.
    /// </summary>
    /// <remarks>
    /// <para>Three conversions, each with a purpose (see Module 09):</para>
    /// <list type="bullet">
    /// <item><see cref="FromDto"/>: JSON is read to a <see cref="LevelDto"/> by
    /// <see cref="LevelSerializer.ParseDto"/>, then to a draft here. Stopping at
    /// the DTO lets a structurally readable but semantically broken level be
    /// opened and fixed. This conversion is lenient: an unparseable enum name
    /// becomes that enum's first member rather than throwing, so nothing about a
    /// malformed file keeps it from opening.</item>
    /// <item><see cref="ToDto"/>: the save half. Warnings never block saving.</item>
    /// <item><see cref="ToContext"/>: draft to <see cref="LevelContext"/>, where
    /// <c>Core</c>'s rules apply. It routes through the DTO and
    /// <see cref="LevelSerializer"/> so structural checking is not duplicated;
    /// a failure surfaces as an exception the editor catches and shows.</item>
    /// </list>
    /// <para><b>Keeping the conversion honest.</b> A field added to
    /// <see cref="LevelDto"/> and not to the matching draft type compiles fine
    /// and silently drops data on load and save. The guard is a round-trip test
    /// (<c>LevelDraftTests</c>) over <c>Corpus.EveryFieldLevel()</c> — the same
    /// every-field fixture the serialization round-trip uses — so a new field
    /// forces an obvious edit to a fixture that both tests read.</para>
    /// <para><b>Normalisation.</b> Draft cells and origins are stored exactly as
    /// authored — not shifted to a <c>(0,0)</c> minimum the way
    /// <see cref="BlockDefinition"/> normalises (D30). That normalisation happens
    /// only in <see cref="ToContext"/>, when <c>Core</c>'s constructors run.</para>
    /// </remarks>
    public sealed class LevelDraft
    {
        public int LevelId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<Coord> StaticWalls { get; set; } = new List<Coord>();
        public List<BlockDraft> Blocks { get; set; } = new List<BlockDraft>();
        public List<GateDraft> Gates { get; set; } = new List<GateDraft>();
        public List<ShutterDraft> Shutters { get; set; } = new List<ShutterDraft>();
        public List<GeneratorDraft> Generators { get; set; } = new List<GeneratorDraft>();
        public List<ElevatorDraft> Elevators { get; set; } = new List<ElevatorDraft>();
        public int SuggestedTimeBudgetSeconds { get; set; }
        public int GoldReward { get; set; }

        /// <summary>
        /// What <see cref="FromDto"/> could not read and had to substitute a
        /// default for — an unrecognised enum or colour name. The draft opens
        /// anyway (opening a broken level is the point), but <c>DraftValidator</c>
        /// reports each of these so the substitution is never silent and the
        /// designer knows the original value is being replaced.
        /// </summary>
        public List<DraftLoadIssue> LoadIssues { get; } = new List<DraftLoadIssue>();

        /// <summary>
        /// An empty draft of the given size — a grid and nothing in it. A starter
        /// template would encode today's taste and be wrong within a month
        /// (Module 09).
        /// </summary>
        public static LevelDraft NewEmpty(int width, int height) =>
            new LevelDraft { Width = width, Height = height };

        // -- DTO -> draft (lenient) --------------------------------------

        public static LevelDraft FromDto(LevelDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            var draft = new LevelDraft
            {
                LevelId = dto.levelId,
                Width = dto.width,
                Height = dto.height,
                SuggestedTimeBudgetSeconds = dto.suggestedTimeBudgetSeconds,
                GoldReward = dto.goldReward,
                StaticWalls = Coords(dto.staticWalls),
            };

            var loader = new Loader(draft.LoadIssues);

            foreach (var b in dto.blocks ?? Array.Empty<BlockDto>())
            {
                var label = $"Block {b.id}";
                draft.Blocks.Add(new BlockDraft
                {
                    Id = b.id,
                    Cells = Coords(b.cells),
                    ColorStack = loader.ColorList(b.colorStack, label),
                    StartOrigin = ToCoord(b.startOrigin),
                    Axis = loader.ParseEnum<MovementAxis>(b.axis, label, "an axis"),
                    UnfreezeAtClearCount = FromSentinel(b.unfreezeAtClearCount),
                    LockId = FromSentinel(b.lockId),
                    RequiredKeyCount = b.requiredKeyCount,
                    KeyTargetLockId = FromSentinel(b.keyTargetLockId),
                    KeyEffect = loader.ParseEnum<KeyEffect>(b.keyEffect, label, "a key effect"),
                    TimeBonusSeconds = b.timeBonusSeconds,
                });
            }

            foreach (var g in dto.gates ?? Array.Empty<GateDto>())
            {
                var label = $"Gate {g.id}";
                draft.Gates.Add(new GateDraft
                {
                    Id = g.id,
                    Edge = loader.ParseEnum<BoardEdge>(g.edge, label, "a board edge"),
                    Offset = g.offset,
                    Width = g.width,
                    Color = loader.ParseEnum<BlockColor>(g.color, label, "a colour"),
                    OpenAtClearCount = FromSentinel(g.openAtClearCount),
                });
            }

            foreach (var s in dto.shutters ?? Array.Empty<ShutterDto>())
            {
                draft.Shutters.Add(new ShutterDraft
                {
                    Id = s.id,
                    Min = ToCoord(s.min),
                    Max = ToCoord(s.max),
                    Threshold = s.threshold,
                    RequiredColor = loader.NullableColor(s.requiredColor, $"Shutter {s.id}"),
                });
            }

            foreach (var gen in dto.generators ?? Array.Empty<GeneratorDto>())
            {
                var label = $"Generator {gen.id}";
                var draftGen = new GeneratorDraft
                {
                    Id = gen.id,
                    Edge = loader.ParseEnum<BoardEdge>(gen.edge, label, "a board edge"),
                    Offset = gen.offset,
                };

                var queue = gen.queue ?? Array.Empty<SpawnedBlockDto>();
                for (var i = 0; i < queue.Length; i++)
                {
                    draftGen.Queue.Add(loader.Spawned(queue[i], $"{label} queue entry {i}"));
                }

                draft.Generators.Add(draftGen);
            }

            foreach (var el in dto.elevators ?? Array.Empty<ElevatorDto>())
            {
                var label = $"Elevator {el.id}";
                var draftEl = new ElevatorDraft
                {
                    Id = el.id,
                    Min = ToCoord(el.min),
                    Max = ToCoord(el.max),
                };

                var waves = el.waves ?? Array.Empty<WaveDto>();
                for (var w = 0; w < waves.Length; w++)
                {
                    var draftWave = new WaveDraft();
                    var blocks = waves[w]?.blocks ?? Array.Empty<SpawnedBlockDto>();
                    for (var i = 0; i < blocks.Length; i++)
                    {
                        draftWave.Blocks.Add(loader.Spawned(blocks[i], $"{label} wave {w} block {i}"));
                    }

                    draftEl.Waves.Add(draftWave);
                }

                draft.Elevators.Add(draftEl);
            }

            return draft;
        }

        /// <summary>
        /// Parses the enum-name and colour-name fields of a DTO into a draft,
        /// recording every value it could not read as a <see cref="DraftLoadIssue"/>
        /// rather than substituting a default silently.
        /// </summary>
        private sealed class Loader
        {
            private readonly List<DraftLoadIssue> issues;

            public Loader(List<DraftLoadIssue> issues) => this.issues = issues;

            public TEnum ParseEnum<TEnum>(string raw, string element, string whatKind) where TEnum : struct
            {
                if (string.IsNullOrEmpty(raw))
                {
                    // Absent, not wrong: treat as the default with no note.
                    return default;
                }

                if (Enum.TryParse<TEnum>(raw, out var value) && Enum.IsDefined(typeof(TEnum), value))
                {
                    return value;
                }

                var fallback = default(TEnum);
                issues.Add(new DraftLoadIssue(element, whatKind, raw, fallback.ToString()));
                return fallback;
            }

            public List<BlockColor> ColorList(string[] names, string element)
            {
                var list = new List<BlockColor>();
                var safe = names ?? Array.Empty<string>();
                for (var i = 0; i < safe.Length; i++)
                {
                    list.Add(ParseEnum<BlockColor>(safe[i], $"{element} colour {i + 1}", "a colour"));
                }

                return list;
            }

            public BlockColor? NullableColor(string raw, string element)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    return null;
                }

                if (Enum.TryParse<BlockColor>(raw, out var color) && Enum.IsDefined(typeof(BlockColor), color))
                {
                    return color;
                }

                issues.Add(new DraftLoadIssue(element, "a colour", raw, "no required colour"));
                return null;
            }

            public SpawnedBlockDraft Spawned(SpawnedBlockDto sb, string element) =>
                new SpawnedBlockDraft
                {
                    Cells = Coords(sb.cells),
                    ColorStack = ColorList(sb.colorStack, element),
                    Axis = ParseEnum<MovementAxis>(sb.axis, element, "an axis"),
                    UnfreezeAtClearCount = FromSentinel(sb.unfreezeAtClearCount),
                    LockId = FromSentinel(sb.lockId),
                    RequiredKeyCount = sb.requiredKeyCount,
                    KeyTargetLockId = FromSentinel(sb.keyTargetLockId),
                    KeyEffect = ParseEnum<KeyEffect>(sb.keyEffect, element, "a key effect"),
                    TimeBonusSeconds = sb.timeBonusSeconds,
                    RegionOrigin = sb.hasRegionOrigin ? ToCoord(sb.regionOrigin) : (Coord?)null,
                };
        }

        // -- draft -> DTO ----------------------------------------------

        public LevelDto ToDto()
        {
            return new LevelDto
            {
                formatVersion = LevelSerializer.FormatVersion,
                levelId = LevelId,
                width = Width,
                height = Height,
                suggestedTimeBudgetSeconds = SuggestedTimeBudgetSeconds,
                goldReward = GoldReward,
                staticWalls = CoordDtos(StaticWalls),
                blocks = Map(Blocks, b => new BlockDto
                {
                    id = b.Id,
                    cells = CoordDtos(b.Cells),
                    colorStack = ColorNames(b.ColorStack),
                    startOrigin = ToCoordDto(b.StartOrigin),
                    axis = b.Axis.ToString(),
                    unfreezeAtClearCount = ToSentinel(b.UnfreezeAtClearCount),
                    lockId = ToSentinel(b.LockId),
                    requiredKeyCount = b.RequiredKeyCount,
                    keyTargetLockId = ToSentinel(b.KeyTargetLockId),
                    keyEffect = b.KeyEffect.ToString(),
                    timeBonusSeconds = b.TimeBonusSeconds,
                }),
                gates = Map(Gates, g => new GateDto
                {
                    id = g.Id,
                    edge = g.Edge.ToString(),
                    offset = g.Offset,
                    width = g.Width,
                    color = g.Color.ToString(),
                    openAtClearCount = ToSentinel(g.OpenAtClearCount),
                }),
                shutters = Map(Shutters, s => new ShutterDto
                {
                    id = s.Id,
                    min = ToCoordDto(s.Min),
                    max = ToCoordDto(s.Max),
                    threshold = s.Threshold,
                    requiredColor = s.RequiredColor?.ToString() ?? string.Empty,
                }),
                generators = Map(Generators, g => new GeneratorDto
                {
                    id = g.Id,
                    edge = g.Edge.ToString(),
                    offset = g.Offset,
                    queue = Map(g.Queue, SpawnedToDto),
                }),
                elevators = Map(Elevators, e => new ElevatorDto
                {
                    id = e.Id,
                    min = ToCoordDto(e.Min),
                    max = ToCoordDto(e.Max),
                    waves = Map(e.Waves, w => new WaveDto { blocks = Map(w.Blocks, SpawnedToDto) }),
                }),
            };
        }

        private static SpawnedBlockDto SpawnedToDto(SpawnedBlockDraft sb) =>
            new SpawnedBlockDto
            {
                cells = CoordDtos(sb.Cells),
                colorStack = ColorNames(sb.ColorStack),
                axis = sb.Axis.ToString(),
                unfreezeAtClearCount = ToSentinel(sb.UnfreezeAtClearCount),
                lockId = ToSentinel(sb.LockId),
                requiredKeyCount = sb.RequiredKeyCount,
                keyTargetLockId = ToSentinel(sb.KeyTargetLockId),
                keyEffect = sb.KeyEffect.ToString(),
                timeBonusSeconds = sb.TimeBonusSeconds,
                hasRegionOrigin = sb.RegionOrigin.HasValue,
                regionOrigin = sb.RegionOrigin.HasValue ? ToCoordDto(sb.RegionOrigin.Value) : default,
            };

        // -- draft -> LevelContext -----------------------------------

        /// <summary>
        /// Converts to a <see cref="LevelContext"/>, applying <c>Core</c>'s rules.
        /// Throws <see cref="LevelSerializationException"/> for a structural
        /// fault (an enum name the draft somehow still holds, a missing array) or
        /// <see cref="ArgumentException"/> for a semantic one (a block outside
        /// the grid, a key pointing at no lock), with <c>Core</c>'s wording. The
        /// editor catches both and shows them rather than letting them escape.
        /// Goes straight through <see cref="LevelSerializer.FromDto"/> — no JSON
        /// text round trip — because this runs on every validation.
        /// </summary>
        public LevelContext ToContext() =>
            LevelSerializer.FromDto(ToDto(), "level editor draft");

        // -- Grid resize --------------------------------------------

        /// <summary>
        /// What a resize to <paramref name="newWidth"/> x
        /// <paramref name="newHeight"/> would push outside the grid, computed
        /// without changing anything. Growing a grid always returns a lossless
        /// impact; shrinking may not, and the editor confirms before
        /// <see cref="ApplyResize"/> removes the reported set.
        /// </summary>
        public ResizeImpact PreviewResize(int newWidth, int newHeight)
        {
            var blocks = new List<int>();
            foreach (var b in Blocks)
            {
                if (FootprintEscapes(b.StartOrigin, b.Cells, newWidth, newHeight))
                {
                    blocks.Add(b.Id);
                }
            }

            var gates = new List<int>();
            foreach (var g in Gates)
            {
                if (EdgeSpanEscapes(g.Edge, g.Offset, g.Width, newWidth, newHeight))
                {
                    gates.Add(g.Id);
                }
            }

            var shutters = new List<int>();
            foreach (var s in Shutters)
            {
                if (!Inside(s.Min, newWidth, newHeight) || !Inside(s.Max, newWidth, newHeight))
                {
                    shutters.Add(s.Id);
                }
            }

            var generators = new List<int>();
            foreach (var g in Generators)
            {
                if (EdgeSpanEscapes(g.Edge, g.Offset, 1, newWidth, newHeight))
                {
                    generators.Add(g.Id);
                }
            }

            var elevators = new List<int>();
            foreach (var e in Elevators)
            {
                if (!Inside(e.Min, newWidth, newHeight) || !Inside(e.Max, newWidth, newHeight))
                {
                    elevators.Add(e.Id);
                }
            }

            var walls = new List<Coord>();
            foreach (var w in StaticWalls)
            {
                if (!Inside(w, newWidth, newHeight))
                {
                    walls.Add(w);
                }
            }

            return new ResizeImpact(newWidth, newHeight, blocks, gates, shutters, generators, elevators, walls);
        }

        /// <summary>
        /// Resizes the grid, removing exactly the elements
        /// <see cref="PreviewResize"/> reports as escaping and nothing else.
        /// </summary>
        public void ApplyResize(int newWidth, int newHeight)
        {
            var impact = PreviewResize(newWidth, newHeight);

            Blocks.RemoveAll(b => impact.RemovedBlockIds.Contains(b.Id));
            Gates.RemoveAll(g => impact.RemovedGateIds.Contains(g.Id));
            Shutters.RemoveAll(s => impact.RemovedShutterIds.Contains(s.Id));
            Generators.RemoveAll(g => impact.RemovedGeneratorIds.Contains(g.Id));
            Elevators.RemoveAll(e => impact.RemovedElevatorIds.Contains(e.Id));
            StaticWalls.RemoveAll(w => impact.RemovedStaticWalls.Contains(w));

            Width = newWidth;
            Height = newHeight;
        }

        private static bool FootprintEscapes(Coord origin, IReadOnlyList<Coord> cells, int w, int h)
        {
            foreach (var cell in cells)
            {
                if (!Inside(origin + cell, w, h))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EdgeSpanEscapes(BoardEdge edge, int offset, int span, int w, int h)
        {
            var edgeLength = edge == BoardEdge.Top || edge == BoardEdge.Bottom ? w : h;
            return offset < 0 || offset + span > edgeLength;
        }

        private static bool Inside(Coord c, int w, int h) =>
            c.X >= 0 && c.X < w && c.Y >= 0 && c.Y < h;

        // -- Conversion primitives --------------------------------

        private static int SentinelNone => -1;

        private static int ToSentinel(int? value) => value ?? SentinelNone;

        private static int? FromSentinel(int value) => value == SentinelNone ? (int?)null : value;

        private static Coord ToCoord(CoordDto dto) => new Coord(dto.x, dto.y);

        private static CoordDto ToCoordDto(Coord c) => new CoordDto { x = c.X, y = c.Y };

        private static List<Coord> Coords(CoordDto[] dtos)
        {
            var list = new List<Coord>();
            foreach (var d in dtos ?? Array.Empty<CoordDto>())
            {
                list.Add(ToCoord(d));
            }

            return list;
        }

        private static CoordDto[] CoordDtos(IReadOnlyList<Coord> coords)
        {
            var result = new CoordDto[coords.Count];
            for (var i = 0; i < coords.Count; i++)
            {
                result[i] = ToCoordDto(coords[i]);
            }

            return result;
        }

        private static string[] ColorNames(IReadOnlyList<BlockColor> colors)
        {
            var result = new string[colors.Count];
            for (var i = 0; i < colors.Count; i++)
            {
                result[i] = colors[i].ToString();
            }

            return result;
        }

        private static TDto[] Map<TSource, TDto>(IReadOnlyList<TSource> source, Func<TSource, TDto> convert)
        {
            var result = new TDto[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = convert(source[i]);
            }

            return result;
        }
    }

    /// <summary>
    /// A value <see cref="LevelDraft.FromDto"/> could not read from a file — an
    /// unrecognised enum or colour name — recorded so the substituted default is
    /// never silent. <c>DraftValidator</c> turns each into a warning.
    /// </summary>
    public sealed class DraftLoadIssue
    {
        public string ElementLabel { get; }
        public string WhatKind { get; }
        public string RawValue { get; }
        public string Fallback { get; }

        public DraftLoadIssue(string elementLabel, string whatKind, string rawValue, string fallback)
        {
            ElementLabel = elementLabel;
            WhatKind = whatKind;
            RawValue = rawValue;
            Fallback = fallback;
        }

        public string Message =>
            $"{ElementLabel}: '{RawValue}' is not {WhatKind}; showing {Fallback} until you set one.";

        public override string ToString() => Message;
    }

    /// <summary>A block under edit. Cells and <see cref="StartOrigin"/> are stored as authored, not normalised.</summary>
    public sealed class BlockDraft
    {
        public int Id { get; set; }
        public List<Coord> Cells { get; set; } = new List<Coord>();
        public List<BlockColor> ColorStack { get; set; } = new List<BlockColor>();
        public Coord StartOrigin { get; set; }
        public MovementAxis Axis { get; set; }
        public int? UnfreezeAtClearCount { get; set; }
        public int? LockId { get; set; }
        public int RequiredKeyCount { get; set; }
        public int? KeyTargetLockId { get; set; }
        public KeyEffect KeyEffect { get; set; }
        public int TimeBonusSeconds { get; set; }
    }

    public sealed class GateDraft
    {
        public int Id { get; set; }
        public BoardEdge Edge { get; set; }
        public int Offset { get; set; }
        public int Width { get; set; }
        public BlockColor Color { get; set; }
        public int? OpenAtClearCount { get; set; }
    }

    public sealed class ShutterDraft
    {
        public int Id { get; set; }
        public Coord Min { get; set; }
        public Coord Max { get; set; }
        public int Threshold { get; set; }
        public BlockColor? RequiredColor { get; set; }
    }

    public sealed class GeneratorDraft
    {
        public int Id { get; set; }
        public BoardEdge Edge { get; set; }
        public int Offset { get; set; }
        public List<SpawnedBlockDraft> Queue { get; set; } = new List<SpawnedBlockDraft>();
    }

    public sealed class ElevatorDraft
    {
        public int Id { get; set; }
        public Coord Min { get; set; }
        public Coord Max { get; set; }
        public List<WaveDraft> Waves { get; set; } = new List<WaveDraft>();
    }

    public sealed class WaveDraft
    {
        public List<SpawnedBlockDraft> Blocks { get; set; } = new List<SpawnedBlockDraft>();
    }

    /// <summary>
    /// A spawned block under edit. <see cref="RegionOrigin"/> is set for an
    /// elevator wave block (its cell relative to the region's <c>Min</c>) and
    /// null for generator output (M9).
    /// </summary>
    public sealed class SpawnedBlockDraft
    {
        public List<Coord> Cells { get; set; } = new List<Coord>();
        public List<BlockColor> ColorStack { get; set; } = new List<BlockColor>();
        public MovementAxis Axis { get; set; }
        public int? UnfreezeAtClearCount { get; set; }
        public int? LockId { get; set; }
        public int RequiredKeyCount { get; set; }
        public int? KeyTargetLockId { get; set; }
        public KeyEffect KeyEffect { get; set; }
        public int TimeBonusSeconds { get; set; }
        public Coord? RegionOrigin { get; set; }
    }
}
