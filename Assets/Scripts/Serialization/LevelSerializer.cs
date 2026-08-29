using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Serialization
{
    /// <summary>
    /// Converts a <see cref="LevelContext"/> to JSON and back, through the DTO
    /// types in <c>LevelDtos.cs</c>. This is the one layer permitted to reference
    /// <c>UnityEngine</c> (for <see cref="JsonUtility"/>) while <c>GateRush.Core</c>
    /// is not — see <c>DECISIONS.md</c> D17.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The module draws a hard line between two validation questions.
    /// <em>Structural</em> validation lives here and asks only whether the text
    /// can become a <see cref="LevelContext"/> at all: is it JSON, is its
    /// <c>formatVersion</c> understood, do its enum names parse, are the
    /// structurally required arrays present, is every sentinel field either
    /// <c>-1</c> or non-negative. Failures are raised as
    /// <see cref="LevelSerializationException"/>.
    /// </para>
    /// <para>
    /// That structural work is split across two stages so the Level Editor can
    /// stop at the first. <see cref="ParseDto"/> does only what decides whether
    /// the text is a DTO at all — JSON, an object, a known <c>formatVersion</c> —
    /// and returns the raw <see cref="LevelDto"/>; the DTO-to-<c>Core</c>
    /// conversion (enum names, sentinels, required arrays, then
    /// <see cref="LevelContext"/>'s own constructor) is the second stage.
    /// <see cref="FromJson"/> runs both; the editor runs <see cref="ParseDto"/>
    /// alone so a semantically broken level can still be opened and repaired
    /// (see Module 09).
    /// </para>
    /// <para>
    /// <em>Semantic</em> validation — blocks inside the grid, cells connected,
    /// ids unique, keys pointing at real locks — belongs to <c>Core</c> and is
    /// left entirely to <see cref="LevelContext"/>'s constructor. Those
    /// <see cref="ArgumentException"/>s propagate untouched, with <c>Core</c>'s
    /// wording. Re-checking any of them here would create two places to fix one
    /// rule.
    /// </para>
    /// <para>
    /// <see cref="FromJson"/> reports the <em>first</em> structural fault it
    /// finds rather than collecting a list. The module's diagnostic model is a
    /// human reading one exception; a future Level Editor that wants a list can
    /// validate its own in-memory DTOs before it ever calls this.
    /// </para>
    /// </remarks>
    public static class LevelSerializer
    {
        /// <summary>
        /// The schema version this build reads and writes. A file carrying any
        /// other value is refused by <see cref="FromJson"/> before conversion is
        /// attempted, so a later schema change is a migration rather than a hunt
        /// for silently misread levels.
        /// </summary>
        public const int FormatVersion = 2;

        /// <summary>
        /// The value every nullable <c>int</c> field takes when absent.
        /// <c>JsonUtility</c> cannot serialize <c>int?</c>, and every nullable
        /// field in <c>Core</c> is non-negative when present, so <c>-1</c> cannot
        /// collide with a real value.
        /// </summary>
        private const int SentinelNone = -1;

        /// <summary>
        /// Serializes <paramref name="ctx"/> to pretty-printed JSON. Pretty
        /// printing costs almost nothing and these files are read in version
        /// control diffs.
        /// </summary>
        public static string ToJson(LevelContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            return JsonUtility.ToJson(ToDto(ctx), prettyPrint: true);
        }

        /// <summary>
        /// Serializes a raw <see cref="LevelDto"/> to pretty-printed JSON — the
        /// save half of the Level Editor's <c>draft -&gt; LevelDto -&gt; JSON</c>
        /// path (see Module 09). No structural or semantic checking: a
        /// half-built level is a normal thing to save, and the editor has
        /// already reported whatever is wrong with it.
        /// </summary>
        public static string ToJson(LevelDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return JsonUtility.ToJson(dto, prettyPrint: true);
        }

        /// <summary>
        /// Parses <paramref name="json"/> into a raw <see cref="LevelDto"/> and
        /// stops there — the load half of the Level Editor's
        /// <c>JSON -&gt; LevelDto -&gt; draft</c> path (see Module 09). It performs
        /// only the checks that decide whether the text can become a DTO
        /// <em>at all</em>: it is non-empty, it is JSON, it is a JSON object, and
        /// its <c>formatVersion</c> is one this build understands. A level that
        /// is structurally readable but semantically broken — a block outside the
        /// grid, a key pointing at no lock — comes back as a DTO here so the one
        /// tool that could repair it can open it. <see cref="FromJson"/> is this
        /// followed by the DTO-to-<c>Core</c> conversion.
        /// </summary>
        /// <exception cref="LevelSerializationException">
        /// The text is empty, is not JSON, is not a JSON object, or carries a
        /// <c>formatVersion</c> other than <see cref="FormatVersion"/>.
        /// </exception>
        public static LevelDto ParseDto(string json, string sourceName = null)
        {
            var source = sourceName ?? "(unnamed source)";

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new LevelSerializationException($"{source}: the level JSON is empty.");
            }

            LevelDto dto;
            try
            {
                dto = JsonUtility.FromJson<LevelDto>(json);
            }
            catch (Exception e)
            {
                throw new LevelSerializationException($"{source}: the text is not valid JSON ({e.Message}).", e);
            }

            if (dto == null)
            {
                throw new LevelSerializationException($"{source}: the text is not a JSON object.");
            }

            if (dto.formatVersion != FormatVersion)
            {
                throw new LevelSerializationException(
                    $"{source}: format version {dto.formatVersion} is not supported; " +
                    $"this build reads version {FormatVersion}.");
            }

            return dto;
        }

        /// <summary>
        /// Parses <paramref name="json"/> into a <see cref="LevelContext"/>.
        /// </summary>
        /// <param name="json">The level JSON.</param>
        /// <param name="sourceName">
        /// The file name or other origin, used only in error messages so a human
        /// diagnosing a malformed level knows which file and which element to
        /// look at. Optional.
        /// </param>
        /// <exception cref="LevelSerializationException">
        /// The text is not JSON, its <c>formatVersion</c> is not
        /// <see cref="FormatVersion"/>, an enum name does not parse, a required
        /// array is absent, or a sentinel field holds a negative other than
        /// <c>-1</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The JSON is structurally sound but describes an invalid level. Raised
        /// by <see cref="LevelContext"/>'s constructor, with <c>Core</c>'s
        /// message.
        /// </exception>
        public static LevelContext FromJson(string json, string sourceName = null)
        {
            var source = sourceName ?? "(unnamed source)";
            return FromDto(ParseDto(json, source), source);
        }

        // -- Core -> DTO ------------------------------------------------------

        private static LevelDto ToDto(LevelContext ctx)
        {
            return new LevelDto
            {
                formatVersion = FormatVersion,
                levelId = ctx.LevelId,
                width = ctx.Width,
                height = ctx.Height,
                staticWalls = ToCoordDtos(ctx.StaticWalls),
                blocks = ToArray(ctx.Blocks, b => ToDto(b)),
                gates = ToArray(ctx.Gates, g => ToDto(g)),
                shutters = ToArray(ctx.Shutters, s => ToDto(s)),
                generators = ToArray(ctx.Generators, g => ToDto(g)),
                elevators = ToArray(ctx.Elevators, e => ToDto(e)),
                suggestedTimeBudgetSeconds = ctx.SuggestedTimeBudgetSeconds,
                goldReward = ctx.GoldReward,
            };
        }

        private static BlockDto ToDto(BlockDefinition block)
        {
            return new BlockDto
            {
                id = block.Id,
                cells = ToCoordDtos(block.Cells),
                colorStack = ToColorNames(block.ColorStack),
                startOrigin = ToDto(block.StartOrigin),
                axis = block.Axis.ToString(),
                unfreezeAtClearCount = ToSentinel(block.UnfreezeAtClearCount),
                lockId = ToSentinel(block.LockId),
                requiredKeyCount = block.RequiredKeyCount,
                keyTargetLockId = ToSentinel(block.KeyTargetLockId),
                keyEffect = block.KeyEffect.ToString(),
                timeBonusSeconds = block.TimeBonusSeconds,
            };
        }

        private static SpawnedBlockDto ToDto(SpawnedBlock block)
        {
            return new SpawnedBlockDto
            {
                cells = ToCoordDtos(block.Cells),
                colorStack = ToColorNames(block.ColorStack),
                axis = block.Axis.ToString(),
                unfreezeAtClearCount = ToSentinel(block.UnfreezeAtClearCount),
                lockId = ToSentinel(block.LockId),
                requiredKeyCount = block.RequiredKeyCount,
                keyTargetLockId = ToSentinel(block.KeyTargetLockId),
                keyEffect = block.KeyEffect.ToString(),
                timeBonusSeconds = block.TimeBonusSeconds,
                hasRegionOrigin = block.RegionOrigin.HasValue,
                regionOrigin = block.RegionOrigin.HasValue ? ToDto(block.RegionOrigin.Value) : default,
            };
        }

        private static GateDto ToDto(GateDefinition gate)
        {
            return new GateDto
            {
                id = gate.Id,
                edge = gate.Edge.ToString(),
                offset = gate.Offset,
                width = gate.Width,
                color = gate.Color.ToString(),
                openAtClearCount = ToSentinel(gate.OpenAtClearCount),
            };
        }

        private static ShutterDto ToDto(ShutterDefinition shutter)
        {
            return new ShutterDto
            {
                id = shutter.Id,
                min = ToDto(shutter.Min),
                max = ToDto(shutter.Max),
                threshold = shutter.Threshold,
                requiredColor = shutter.RequiredColor?.ToString() ?? string.Empty,
            };
        }

        private static GeneratorDto ToDto(GeneratorDefinition generator)
        {
            return new GeneratorDto
            {
                id = generator.Id,
                edge = generator.Edge.ToString(),
                offset = generator.Offset,
                queue = ToArray(generator.Queue, b => ToDto(b)),
            };
        }

        private static ElevatorDto ToDto(ElevatorDefinition elevator)
        {
            var waves = new WaveDto[elevator.Waves.Count];
            for (var i = 0; i < elevator.Waves.Count; i++)
            {
                waves[i] = new WaveDto { blocks = ToArray(elevator.Waves[i], b => ToDto(b)) };
            }

            return new ElevatorDto
            {
                id = elevator.Id,
                min = ToDto(elevator.Min),
                max = ToDto(elevator.Max),
                waves = waves,
            };
        }

        private static CoordDto ToDto(Coord coord) => new CoordDto { x = coord.X, y = coord.Y };

        private static CoordDto[] ToCoordDtos(IReadOnlyList<Coord> coords)
        {
            var result = new CoordDto[coords.Count];
            for (var i = 0; i < coords.Count; i++)
            {
                result[i] = ToDto(coords[i]);
            }

            return result;
        }

        private static string[] ToColorNames(IReadOnlyList<BlockColor> colors)
        {
            var result = new string[colors.Count];
            for (var i = 0; i < colors.Count; i++)
            {
                result[i] = colors[i].ToString();
            }

            return result;
        }

        private static TDto[] ToArray<TSource, TDto>(IReadOnlyList<TSource> source, Func<TSource, TDto> convert)
        {
            var result = new TDto[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = convert(source[i]);
            }

            return result;
        }

        private static int ToSentinel(int? value) => value ?? SentinelNone;

        // -- DTO -> Core ----------------------------------------------------

        private static LevelContext FromDto(LevelDto dto, string source)
        {
            var staticWalls = FromCoordDtos(dto.staticWalls);
            var blocks = ConvertEach(dto.blocks, d => FromDto(d, source));
            var gates = ConvertEach(dto.gates, d => FromDto(d, source));
            var shutters = ConvertEach(dto.shutters, d => FromDto(d, source));
            var generators = ConvertEach(dto.generators, d => FromDto(d, source));
            var elevators = ConvertEach(dto.elevators, d => FromDto(d, source));

            return new LevelContext(
                levelId: dto.levelId,
                width: dto.width,
                height: dto.height,
                staticWalls: staticWalls,
                blocks: blocks,
                gates: gates,
                shutters: shutters,
                generators: generators,
                elevators: elevators,
                suggestedTimeBudgetSeconds: dto.suggestedTimeBudgetSeconds,
                goldReward: dto.goldReward);
        }

        private static BlockDefinition FromDto(BlockDto dto, string source)
        {
            var element = $"block {dto.id}";

            return new BlockDefinition(
                id: dto.id,
                cells: FromCoordDtos(RequireArray(dto.cells, $"{element}: 'cells'", source)),
                colorStack: FromColorNames(RequireArray(dto.colorStack, $"{element}: 'colorStack'", source), element, source),
                startOrigin: FromDto(dto.startOrigin),
                axis: ParseEnum<MovementAxis>(dto.axis, $"{element}: 'axis'", source),
                unfreezeAtClearCount: FromSentinel(dto.unfreezeAtClearCount, $"{element}: 'unfreezeAtClearCount'", source),
                lockId: FromSentinel(dto.lockId, $"{element}: 'lockId'", source),
                requiredKeyCount: dto.requiredKeyCount,
                keyTargetLockId: FromSentinel(dto.keyTargetLockId, $"{element}: 'keyTargetLockId'", source),
                keyEffect: ParseEnum<KeyEffect>(dto.keyEffect, $"{element}: 'keyEffect'", source),
                timeBonusSeconds: dto.timeBonusSeconds);
        }

        private static SpawnedBlock FromDto(SpawnedBlockDto dto, string element, string source)
        {
            return new SpawnedBlock(
                cells: FromCoordDtos(RequireArray(dto.cells, $"{element}: 'cells'", source)),
                colorStack: FromColorNames(RequireArray(dto.colorStack, $"{element}: 'colorStack'", source), element, source),
                axis: ParseEnum<MovementAxis>(dto.axis, $"{element}: 'axis'", source),
                unfreezeAtClearCount: FromSentinel(dto.unfreezeAtClearCount, $"{element}: 'unfreezeAtClearCount'", source),
                lockId: FromSentinel(dto.lockId, $"{element}: 'lockId'", source),
                requiredKeyCount: dto.requiredKeyCount,
                keyTargetLockId: FromSentinel(dto.keyTargetLockId, $"{element}: 'keyTargetLockId'", source),
                keyEffect: ParseEnum<KeyEffect>(dto.keyEffect, $"{element}: 'keyEffect'", source),
                timeBonusSeconds: dto.timeBonusSeconds,
                regionOrigin: dto.hasRegionOrigin ? FromDto(dto.regionOrigin) : (Coord?)null);
        }

        private static GateDefinition FromDto(GateDto dto, string source)
        {
            var element = $"gate {dto.id}";

            return new GateDefinition(
                id: dto.id,
                edge: ParseEnum<BoardEdge>(dto.edge, $"{element}: 'edge'", source),
                offset: dto.offset,
                width: dto.width,
                color: ParseEnum<BlockColor>(dto.color, $"{element}: 'color'", source),
                openAtClearCount: FromSentinel(dto.openAtClearCount, $"{element}: 'openAtClearCount'", source));
        }

        private static ShutterDefinition FromDto(ShutterDto dto, string source)
        {
            var element = $"shutter {dto.id}";

            return new ShutterDefinition(
                id: dto.id,
                min: FromDto(dto.min),
                max: FromDto(dto.max),
                threshold: dto.threshold,
                requiredColor: ParseNullableColor(dto.requiredColor, $"{element}: 'requiredColor'", source));
        }

        private static GeneratorDefinition FromDto(GeneratorDto dto, string source)
        {
            var element = $"generator {dto.id}";
            var queueDtos = RequireArray(dto.queue, $"{element}: 'queue'", source);
            var queue = new SpawnedBlock[queueDtos.Length];
            for (var i = 0; i < queueDtos.Length; i++)
            {
                queue[i] = FromDto(queueDtos[i], $"{element} queue entry {i}", source);
            }

            return new GeneratorDefinition(
                id: dto.id,
                edge: ParseEnum<BoardEdge>(dto.edge, $"{element}: 'edge'", source),
                offset: dto.offset,
                queue: queue);
        }

        private static ElevatorDefinition FromDto(ElevatorDto dto, string source)
        {
            var element = $"elevator {dto.id}";
            var waveDtos = RequireArray(dto.waves, $"{element}: 'waves'", source);
            var waves = new IReadOnlyList<SpawnedBlock>[waveDtos.Length];
            for (var w = 0; w < waveDtos.Length; w++)
            {
                var blockDtos = RequireArray(waveDtos[w]?.blocks, $"{element} wave {w}: 'blocks'", source);
                var wave = new SpawnedBlock[blockDtos.Length];
                for (var b = 0; b < blockDtos.Length; b++)
                {
                    wave[b] = FromDto(blockDtos[b], $"{element} wave {w} block {b}", source);
                }

                waves[w] = wave;
            }

            return new ElevatorDefinition(
                id: dto.id,
                min: FromDto(dto.min),
                max: FromDto(dto.max),
                waves: waves);
        }

        private static Coord FromDto(CoordDto dto) => new Coord(dto.x, dto.y);

        /// <summary>
        /// Converts a coordinate array. A <c>null</c> input becomes an empty
        /// list: the only <c>null</c> that reaches here is an absent top-level
        /// <c>staticWalls</c>, which <see cref="LevelContext"/> also treats as
        /// empty. A block's <c>cells</c> has already passed
        /// <see cref="RequireArray{T}"/> before it gets here.
        /// </summary>
        private static IReadOnlyList<Coord> FromCoordDtos(CoordDto[] dtos)
        {
            var safe = dtos ?? Array.Empty<CoordDto>();
            var result = new Coord[safe.Length];
            for (var i = 0; i < safe.Length; i++)
            {
                result[i] = FromDto(safe[i]);
            }

            return result;
        }

        private static IReadOnlyList<BlockColor> FromColorNames(string[] names, string element, string source)
        {
            var result = new BlockColor[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                result[i] = ParseEnum<BlockColor>(names[i], $"{element}: 'colorStack' entry {i}", source);
            }

            return result;
        }

        private static IReadOnlyList<T> ConvertEach<TDto, T>(TDto[] dtos, Func<TDto, T> convert)
        {
            var safe = dtos ?? Array.Empty<TDto>();
            var result = new T[safe.Length];
            for (var i = 0; i < safe.Length; i++)
            {
                result[i] = convert(safe[i]);
            }

            return result;
        }

        // -- Shared conversion primitives ---------------------------------

        /// <summary>
        /// Turns a sentinel <c>int</c> back into <c>int?</c>: <see cref="SentinelNone"/>
        /// becomes <c>null</c>, any other negative is a structural error, and a
        /// non-negative passes through. The "any other negative" rule is what
        /// keeps <c>-1</c> the single spelling of "none".
        /// </summary>
        private static int? FromSentinel(int value, string element, string source)
        {
            if (value == SentinelNone)
            {
                return null;
            }

            if (value < 0)
            {
                throw new LevelSerializationException(
                    $"{source}: {element} is {value}; a nullable field is {SentinelNone} when absent and " +
                    "non-negative otherwise.");
            }

            return value;
        }

        /// <summary>
        /// Parses an enum member by exact name. <see cref="Enum.IsDefined(Type, object)"/>
        /// is used deliberately in place of <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/>:
        /// <c>TryParse</c> accepts the integer text <c>"3"</c> and comma-separated
        /// lists, which would reintroduce exactly the silent renumbering that
        /// string names exist to prevent.
        /// </summary>
        private static TEnum ParseEnum<TEnum>(string name, string element, string source)
            where TEnum : struct, Enum
        {
            if (!string.IsNullOrEmpty(name) && Enum.IsDefined(typeof(TEnum), name))
            {
                return (TEnum)Enum.Parse(typeof(TEnum), name);
            }

            throw new LevelSerializationException(
                $"{source}: '{name}' is not a valid {typeof(TEnum).Name} for {element}.");
        }

        /// <summary>
        /// Parses an optional colour: an empty or absent string is <c>null</c>
        /// (a global shutter), anything else is parsed by <see cref="ParseEnum{TEnum}"/>.
        /// </summary>
        private static BlockColor? ParseNullableColor(string name, string element, string source)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return ParseEnum<BlockColor>(name, element, source);
        }

        /// <summary>
        /// Rejects an array that is <c>null</c> because its field was absent from
        /// the JSON — a structural fault this layer owns. A present-but-empty
        /// array passes through: whether an empty cell set or colour stack is
        /// legal is <c>Core</c>'s question, not this one.
        /// </summary>
        private static T[] RequireArray<T>(T[] array, string element, string source)
        {
            if (array == null)
            {
                throw new LevelSerializationException(
                    $"{source}: {element} is required but was absent from the JSON.");
            }

            return array;
        }
    }
}
