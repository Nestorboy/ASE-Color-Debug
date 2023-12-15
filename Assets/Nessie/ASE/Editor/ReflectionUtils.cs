using System;
using System.Reflection;

namespace Nessie.ASE.Editor
{
    public static class ReflectionUtils
    {
        private const BindingFlags PublicInstanced = BindingFlags.Instance | BindingFlags.NonPublic;
        
        public static T GetPrivateField<T>(object obj, string fieldName)
        {
            Type type = obj.GetType();

            return GetPrivateField<T>(type, obj, fieldName);
        }

        public static T GetPrivateField<T>(Type type, object obj, string fieldName)
        {
            FieldInfo fInfo = type.GetField(fieldName, PublicInstanced);

            return (T)fInfo?.GetValue(obj);
        }
    }
}
