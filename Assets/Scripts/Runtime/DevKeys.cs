#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;

namespace GateRush.Runtime
{
    /// <summary>
    /// The development keys (Module 21) and nothing else: the one place their
    /// bindings are named. Compiled in the editor and in development builds
    /// only, so a release build has no such keys.
    /// </summary>
    /// <remarks>
    /// Page Down and Page Up sit in the same place on every keyboard layout
    /// and clash with neither <b>R</b> nor the pointer. The tooltip of
    /// <see cref="LevelBootstrap"/>'s Level field names them for the owner;
    /// change it together with these.
    /// </remarks>
    public static class DevKeys
    {
        /// <summary>Goes to the next level in catalog order.</summary>
        public const Key NextLevel = Key.PageDown;

        /// <summary>Goes to the previous level in catalog order.</summary>
        public const Key PreviousLevel = Key.PageUp;

        /// <summary>The step <see cref="NextLevel"/> asks for.</summary>
        public const int NextStep = 1;

        /// <summary>The step <see cref="PreviousLevel"/> asks for.</summary>
        public const int PreviousStep = -1;

        /// <summary>
        /// True on the frame a level key went down, with
        /// <see cref="NextStep"/> or <see cref="PreviousStep"/>. Next wins
        /// when both went down together. False with no keyboard.
        /// </summary>
        public static bool TryReadLevelStep(Keyboard keyboard, out int step)
        {
            step = 0;
            if (keyboard == null)
            {
                return false;
            }

            if (keyboard[NextLevel].wasPressedThisFrame)
            {
                step = NextStep;
            }
            else if (keyboard[PreviousLevel].wasPressedThisFrame)
            {
                step = PreviousStep;
            }

            return step != 0;
        }
    }
}
#endif
