using System;

namespace SiroccoLobby.Services.Helpers
{
    /// <summary>
    /// Non-cryptographic FNV-1a hash for string → uint.
    /// Matches Mirror / UNet behavior (char-based, not UTF8 bytes).
    /// </summary>
    public static class StringToUintFNV1a
    {
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;

        public static uint Compute(string value, out uint hash)
        {
            hash = Compute(value);
            return hash;
        }

        public static uint Compute(string value)
        {
            if (value == null)
                return 0;

            unchecked
            {
                uint hash = OffsetBasis;

                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i]; // IMPORTANT: char, not byte
                    hash *= Prime;
                }

                return hash;
            }
        }
    }
}
