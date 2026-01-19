using System;
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

        public ObjectDumper(
            Func<MemberInfo, bool>? memberFilter = null,
            Func<Type, bool>? typeFilter = null,
            int maxDepth = 2,
            string prefix = "[ProtoTrace]")
        {
            _memberFilter = memberFilter ?? (_ => true);
            _typeFilter = typeFilter ?? (t => 
                t.Namespace?.StartsWith("UnityEngine") != true && 
                t.Namespace?.StartsWith("System") != true);
            _maxDepth = maxDepth;
            _prefix = prefix;
        }

        public void Dump(object obj, string? label = null, string indent = "")
        {
            if (label != null)
                MelonLogger.Msg($"{_prefix} === {label} ===");
            
            DumpRecursive(obj, indent, _maxDepth);
        }

        private void DumpRecursive(object obj, string indent, int depth)
        {
            if (obj == null || depth <= 0) return;

            try
            {
                var type = obj.GetType();
                
                if (!_typeFilter(type)) return;

                // Dump fields
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => _memberFilter(f));
                
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(obj);
                        MelonLogger.Msg($"{_prefix}{indent}{field.Name} = {value}");
                        
                        if (value != null && !field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                            DumpRecursive(value, indent + "  ", depth - 1);
                    }
                    catch { }
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
                        
                        if (value != null && !prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string))
                            DumpRecursive(value, indent + "  ", depth - 1);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Predefined filters
        public static Func<MemberInfo, bool> NetworkRelatedFilter => m =>
            m.Name.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("player", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("state", StringComparison.OrdinalIgnoreCase);
    }
}
