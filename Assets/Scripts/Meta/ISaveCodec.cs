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
        /// Reads <paramref name="text"/> into <paramref name="data"/>. False
        /// for text <see cref="Encode"/> would not have written, a missing or
        /// non-positive format version, or a version newer than this codec
        /// knows: <paramref name="data"/> is then null and
        /// <paramref name="problem"/> says why. True leaves
        /// <paramref name="problem"/> null. Never throws for any text.
        /// </summary>
        bool TryDecode(string text, out SaveData data, out string problem);
    }
}
