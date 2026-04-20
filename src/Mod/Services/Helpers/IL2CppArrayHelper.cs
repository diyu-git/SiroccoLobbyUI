using System;
using System.Collections.Generic;
using System.Reflection;

namespace SiroccoLobby.Services.Helpers
{
    /// <summary>
    /// Helpers for iterating IL2CPP arrays/lists via reflection.
    /// IL2CPP collections expose Length or Count and an Item indexer.
    /// </summary>
    public static class IL2CppArrayHelper
    {
        /// <summary>Returns Length or Count of an IL2CPP array/list, or 0 if null.</summary>
        public static int GetLen(object? array)
        {
            if (array == null) return 0;
            var p = array.GetType().GetProperty("Length") ?? array.GetType().GetProperty("Count");
            return (int)(p?.GetValue(array) ?? 0);
        }

        /// <summary>Returns the Item indexer property, or null.</summary>
        public static PropertyInfo? GetItemProperty(object array)
        {
            return array.GetType().GetProperty("Item");
        }

        /// <summary>Iterates an IL2CPP array, yielding non-null items.</summary>
        public static IEnumerable<object> Iterate(object array)
        {
            var itemProp = GetItemProperty(array);
            int count = GetLen(array);

            for (int i = 0; i < count; i++)
            {
                object? item = null;
                try { item = itemProp?.GetValue(array, new object[] { i }); } catch (Exception ex) { MelonLoader.MelonLogger.Warning($"[IL2CppArrayHelper] Failed to read item at index {i}: {ex.Message}"); }
                if (item != null) yield return item;
            }
        }

        /// <summary>Iterates an IL2CPP array, yielding (item, index) pairs for non-null items.</summary>
        public static IEnumerable<(object item, int index)> IterateIndexed(object array)
        {
            var itemProp = GetItemProperty(array);
            int count = GetLen(array);

            for (int i = 0; i < count; i++)
            {
                object? item = null;
                try { item = itemProp?.GetValue(array, new object[] { i }); } catch (Exception ex) { MelonLoader.MelonLogger.Warning($"[IL2CppArrayHelper] Failed to read item at index {i}: {ex.Message}"); }
                if (item != null) yield return (item, i);
            }
        }
    }
}
