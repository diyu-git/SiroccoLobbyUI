using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace SiroccoLobby.Services.Helpers
{
    /// <summary>
    /// Composable object dumper with configurable filters and formatters
    /// </summary>
    public class ObjectDumper
    {
        private readonly Func<MemberInfo, bool> _memberFilter;
        private readonly Func<Type, bool> _typeFilter;
        private readonly int _maxDepth;
        private readonly string _prefix;

        private readonly int _maxEnumerableItems;

        private readonly HashSet<object> _visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        private bool _loggedFieldDumpException;
        private bool _loggedPropertyDumpException;
        private bool _loggedRootDumpException;

        public ObjectDumper(
            Func<MemberInfo, bool>? memberFilter = null,
            Func<Type, bool>? typeFilter = null,
            int maxDepth = 2,
            string prefix = "[ProtoTrace]",
            int maxEnumerableItems = 10)
        {
            _memberFilter = memberFilter ?? (_ => true);
            _typeFilter = typeFilter ?? (t => 
                t.Namespace?.StartsWith("UnityEngine") != true && 
                t.Namespace?.StartsWith("System") != true);
            _maxDepth = maxDepth;
            _prefix = prefix;
            _maxEnumerableItems = Math.Max(0, maxEnumerableItems);
        }

        public void Dump(object obj, string? label = null, string indent = "")
        {
            if (label != null)
                MelonLogger.Msg($"{_prefix} === {label} ===");

            _visited.Clear();
            _loggedFieldDumpException = false;
            _loggedPropertyDumpException = false;
            _loggedRootDumpException = false;
            DumpRecursive(obj, indent, _maxDepth);
        }

        private void DumpRecursive(object obj, string indent, int depth)
        {
            if (obj == null || depth <= 0) return;

            try
            {
                var type = obj.GetType();

                // Avoid cycles on reference types.
                if (!type.IsValueType)
                {
                    if (!_visited.Add(obj))
                    {
                        MelonLogger.Msg($"{_prefix}{indent}<visited {type.FullName}>");
                        return;
                    }
                }
                
                if (!_typeFilter(type)) return;

                // Special-case enumerables (but avoid dumping string as IEnumerable<char>).
                if (obj is IEnumerable enumerable && obj is not string)
                {
                    DumpEnumerable(enumerable, indent, depth);
                    return;
                }

                // Dump fields
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => _memberFilter(f));
                
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(obj);
                        MelonLogger.Msg($"{_prefix}{indent}{field.Name} = {value}");
                        
                        if (value != null && ShouldRecurse(field.FieldType))
                            DumpRecursive(value, indent + "  ", depth - 1);
                    }
                    catch (Exception ex)
                    {
                        if (!_loggedFieldDumpException)
                        {
                            _loggedFieldDumpException = true;
                            MelonLogger.Msg($"{_prefix}{indent}<field dump failed: {ex.GetType().Name}: {ex.Message}>");
                        }
                    }
                }

                // Dump properties
                var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(p => _memberFilter(p));
                
                foreach (var prop in props)
                {
                    try
                    {
                        var value = prop.GetValue(obj);
                        MelonLogger.Msg($"{_prefix}{indent}{prop.Name} = {value}");
                        
                        if (value != null && ShouldRecurse(prop.PropertyType))
                            DumpRecursive(value, indent + "  ", depth - 1);
                    }
                    catch (Exception ex)
                    {
                        if (!_loggedPropertyDumpException)
                        {
                            _loggedPropertyDumpException = true;
                            MelonLogger.Msg($"{_prefix}{indent}<property dump failed: {ex.GetType().Name}: {ex.Message}>");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_loggedRootDumpException)
                {
                    _loggedRootDumpException = true;
                    MelonLogger.Msg($"{_prefix}{indent}<dump failed: {ex.GetType().Name}: {ex.Message}>");
                }
            }
        }

        private void DumpEnumerable(IEnumerable enumerable, string indent, int depth)
        {
            if (_maxEnumerableItems <= 0)
                return;

            int i = 0;
            foreach (var item in enumerable)
            {
                if (i >= _maxEnumerableItems)
                {
                    MelonLogger.Msg($"{_prefix}{indent}<...>");
                    break;
                }

                MelonLogger.Msg($"{_prefix}{indent}[{i}] = {item}");
                if (item != null && depth > 1 && ShouldRecurse(item.GetType()))
                {
                    DumpRecursive(item, indent + "  ", depth - 1);
                }
                i++;
            }
        }

        private static bool ShouldRecurse(Type t)
        {
            return !t.IsPrimitive
                   && !t.IsEnum
                   && t != typeof(string)
                   && t != typeof(decimal)
                   && t != typeof(DateTime)
                   && t != typeof(TimeSpan);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        // Predefined filters
        public static Func<MemberInfo, bool> NetworkRelatedFilter => m =>
            m.Name.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("player", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("state", StringComparison.OrdinalIgnoreCase);

        public static Func<MemberInfo, bool> ProtoLobbyRelatedFilter => m =>
            m.Name.Contains("proto", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("lobby", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("player", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("status", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("steam", StringComparison.OrdinalIgnoreCase);
    }
}
