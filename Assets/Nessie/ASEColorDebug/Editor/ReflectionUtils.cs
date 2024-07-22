using System;
using System.Reflection;

namespace Nessie.ASE.Editor
{
    public static class ReflectionUtils
    {
        public const BindingFlags PrivateInstanced = BindingFlags.Instance | BindingFlags.NonPublic;
        public const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        public const BindingFlags Private = PrivateInstanced | PrivateStatic;
        
        public static T GetField<T>(object obj, string fieldName, BindingFlags flags)
        {
            return GetField<T>(obj.GetType(), obj, fieldName, flags);
        }

        public static T GetField<T>(Type type, object obj, string fieldName, BindingFlags flags)
        {
            FieldInfo fInfo = type.GetField(fieldName, flags);

            return (T)fInfo?.GetValue(obj);
        }
        
        public static T GetPrivateField<T>(object obj, string fieldName)
        {
            return GetPrivateField<T>(obj.GetType(), obj, fieldName);
        }

        public static T GetPrivateField<T>(Type type, object obj, string fieldName)
        {
            return GetField<T>(type, obj, fieldName, Private);
        }
    }
}
