using System;

namespace GateRush.Serialization
{
    /// <summary>
    /// Thrown when a JSON string cannot be turned into a <c>LevelContext</c> at
    /// all: the text is not JSON, its <c>formatVersion</c> is not one this build
    /// understands, an enum name does not parse, a structurally required array is
    /// absent, or a sentinel field holds a negative value other than <c>-1</c>.
    /// </summary>
    /// <remarks>
    /// This is the boundary between the two validation questions the module keeps
    /// separate: <em>can this become a level</em> (here) versus <em>is this level
    /// valid</em> (<c>GateRush.Core</c>). Semantic faults — a block outside the
    /// grid, a key pointing at no lock — are raised by <c>Core</c>'s own
    /// constructors as <see cref="ArgumentException"/> and are never caught,
    /// wrapped, or pre-empted here.
    /// </remarks>
    public sealed class LevelSerializationException : Exception
    {
        public LevelSerializationException(string message)
            : base(message)
        {
        }

        public LevelSerializationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
