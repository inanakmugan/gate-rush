namespace GateRush.Runtime
{
    /// <summary>
    /// A moment the game gives a sound (Module 25). Each has its clips and
    /// levels in <see cref="SoundConfig"/>; which moment plays which cue is
    /// decided by <see cref="MoveCues"/>, <see cref="TimerTicks"/> and the
    /// bootstrap, never by a view.
    /// </summary>
    public enum SoundCue
    {
        /// <summary>A block is grabbed.</summary>
        Lift,

        /// <summary>A drag ends and the block stays on the board: a settle with no clear, a snap back, a cancel.</summary>
        Drop,

        /// <summary>Leave stage: a block is destroyed through its gate.</summary>
        Exit,

        /// <summary>Leave stage: a layered block sheds a colour (M4).</summary>
        Peel,

        /// <summary>Arrive stage: a block thaws (M3) or a gate's ice breaks (M2).</summary>
        IceBreak,

        /// <summary>Arrive stage: a shutter lifts (M5).</summary>
        ShutterOpen,

        /// <summary>Arrive stage: a lock opens (M8).</summary>
        Unlock,

        /// <summary>Arrive stage: a generator pushes a block (M6) or an elevator raises a wave (M9).</summary>
        Spawn,

        /// <summary>Arrive stage: a badge's count goes down without reaching its end.</summary>
        CountPop,

        /// <summary>A move earns a time bonus (M10), with the HUD's "+N s".</summary>
        TimeBonus,

        /// <summary>One of the last whole seconds of the countdown, while the level is played.</summary>
        TimerTick,

        /// <summary>The win panel opens.</summary>
        Win,

        /// <summary>The loss panel opens.</summary>
        Loss,

        /// <summary>A button of the menu, the HUD, the result panel or an introduction card is pressed.</summary>
        UiTap
    }
}
