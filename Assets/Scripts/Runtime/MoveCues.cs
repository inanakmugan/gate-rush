using System;
using System.Collections.Generic;

namespace GateRush.Runtime
{
    /// <summary>
    /// The sound cues a stage of a move's presentation plays (Module 25),
    /// decided from <see cref="MoveChanges"/> alone: what the stage shows is
    /// what it sounds.
    /// </summary>
    /// <remarks>
    /// <para><b>Each kind once.</b> A stage plays a cue at most once, however
    /// many blocks it concerns: two blocks leaving together make one exit
    /// sound, not two stacked.</para>
    /// <para><b>Fixed order.</b> Leave: <see cref="SoundCue.Exit"/>, then
    /// <see cref="SoundCue.Peel"/>. Arrive: <see cref="SoundCue.IceBreak"/>,
    /// <see cref="SoundCue.ShutterOpen"/>, <see cref="SoundCue.Unlock"/>,
    /// <see cref="SoundCue.Spawn"/>, <see cref="SoundCue.CountPop"/>. A stage
    /// that shows nothing gives an empty list.</para>
    /// </remarks>
    public static class MoveCues
    {
        /// <summary>
        /// The leave stage's cues: <see cref="SoundCue.Exit"/> when a block is
        /// destroyed through its gate, <see cref="SoundCue.Peel"/> when a
        /// layered block sheds a colour and survives.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="changes"/> is null.</exception>
        public static IReadOnlyList<SoundCue> Leave(MoveChanges changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            var hasExit = false;
            var hasPeel = false;
            var clears = changes.Clears;
            for (var i = 0; i < clears.Count; i++)
            {
                if (clears[i].IsDestroyed)
                {
                    hasExit = true;
                }
                else
                {
                    hasPeel = true;
                }
            }

            if (!hasExit && !hasPeel)
            {
                return Array.Empty<SoundCue>();
            }

            var cues = new List<SoundCue>();
            AddIf(cues, hasExit, SoundCue.Exit);
            AddIf(cues, hasPeel, SoundCue.Peel);
            return cues;
        }

        /// <summary>
        /// The arrive stage's cues: <see cref="SoundCue.IceBreak"/> for a
        /// thawed block or an opened gate, <see cref="SoundCue.ShutterOpen"/>,
        /// <see cref="SoundCue.Unlock"/>, <see cref="SoundCue.Spawn"/> for a
        /// generator's block and an elevator's wave alike, and
        /// <see cref="SoundCue.CountPop"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="changes"/> is null.</exception>
        public static IReadOnlyList<SoundCue> Arrive(MoveChanges changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            if (!changes.HasArrivals)
            {
                return Array.Empty<SoundCue>();
            }

            var cues = new List<SoundCue>();
            AddIf(cues, changes.ThawedBlocks.Count > 0 || changes.OpenedGates.Count > 0, SoundCue.IceBreak);
            AddIf(cues, changes.OpenedShutters.Count > 0, SoundCue.ShutterOpen);
            AddIf(cues, changes.UnlockedBlocks.Count > 0, SoundCue.Unlock);
            AddIf(cues, changes.Spawns.Count > 0, SoundCue.Spawn);
            AddIf(cues, changes.CountPops.Count > 0, SoundCue.CountPop);
            return cues;
        }

        private static void AddIf(List<SoundCue> cues, bool isShown, SoundCue cue)
        {
            if (isShown)
            {
                cues.Add(cue);
            }
        }
    }
}
