using System;
using System.Reflection;
namespace PumpkinPanicCoop
{
    /// <summary>Small reflection helper replacing Harmony's AccessTools utility only.</summary>
    internal static class CoopReflection
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static FieldInfo Field(Type type, string name) { return type.GetField(name, Flags); }
        internal static MethodInfo Method(Type type, string name, Type[] parameters = null, Type[] genericArguments = null)
        {
            if (parameters == null) return type.GetMethod(name, Flags);
            return type.GetMethod(name, Flags, null, parameters, null);
        }
        internal static MethodInfo PropertyGetter(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, Flags);
            return property != null ? property.GetGetMethod(true) : null;
        }
    }
}
