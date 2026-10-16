namespace GateRush.Runtime
{
    /// <summary>
    /// What one joker button shows (Module 34): its text, and whether it can
    /// be pressed. Decided by <see cref="JokerButtons"/>, shown by
    /// <see cref="JokerBar"/>.
    /// </summary>
    public readonly struct JokerButton
    {
        /// <summary>A button reading <paramref name="label"/>, pressable when <paramref name="isInteractable"/>.</summary>
        public JokerButton(string label, bool isInteractable)
        {
            Label = label;
            IsInteractable = isInteractable;
        }

        /// <summary>The button's text.</summary>
        public string Label { get; }

        /// <summary>True when the button can be pressed.</summary>
        public bool IsInteractable { get; }
    }
}
