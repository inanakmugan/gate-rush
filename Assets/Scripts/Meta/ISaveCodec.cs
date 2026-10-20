using System.Collections.Generic;

namespace GateRush.Meta
{
    /// <summary>
    /// Turns a <see cref="SaveData"/> into text and back (D56). Declared here
    /// and implemented in <c>GateRush.Serialization</c>: this assembly has no
    /// JSON reader, and the rules for what a missing, unreadable or too new
    /// save gives stay in <see cref="SaveRepository"/>, where they are tested
    /// without the engine. A codec only encodes and decodes.
    /// </summary>
    public interface ISaveCodec
    {
        /// <summary>
        /// The whole of <paramref name="data"/> as text, every field written,
        /// tagged with the format version it is written in.
        /// </summary>
        string Encode(SaveData data);

        /// <summary>
        /// Reads <paramref name="text"/> into <paramref name="data"/>. A
        /// section the text's version did not have takes the value a new
        /// player gets, built from <paramref name="rules"/> through the
        /// section's own factory, never from numbers of the codec's (D60).
        /// False for text <see cref="Encode"/> would not have written, a
        /// missing or non-positive format version, or a version newer than
        /// this codec knows: <paramref name="data"/> is then null and
        /// <paramref name="problem"/> says why. True leaves
        /// <paramref name="problem"/> null. Never throws for any text.
        /// </summary>
        /// <param name="resets">
        /// One sentence per field that was read at its default because its
        /// value broke a rule — a refused name or an empty avatar id (Module
        /// 38), where losing the whole save would be worse than resetting the
        /// field — naming the field and the value used. Empty, never null,
        /// when nothing was reset and whenever the result is false.
        /// </param>
        /// <exception cref="System.ArgumentNullException"><paramref name="rules"/> is null.</exception>
        bool TryDecode(string text, MetaRules rules, out SaveData data, out string problem, out IReadOnlyList<string> resets);
    }
}
