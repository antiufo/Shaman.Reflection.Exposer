using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Shaman.Runtime;
using Shaman.Runtime.ReflectionExtensions;

namespace Shaman.Runtime
{


    public class Exposer : DynamicObject
    {
        public object Object { get; private set; }
        public Type Type { get; private set; }
#if NETFX_CORE
        private List<TypeInfo> typeInfos;
#endif

        public Exposer(object obj)
        {
            this.Object = obj;
            this.Type = obj.GetType();
#if NETFX_CORE
            InitializeInheritanceChain();
#endif
        }

#if NETFX_CORE
        private void InitializeInheritanceChain()
        {
            typeInfos = new List<TypeInfo>();
            var t = this.Type;
            while (t != null)
            {
                var info = t.GetTypeInfo();
                typeInfos.Add(info);
                t = info.BaseType;
            }
        }
#endif

        internal Exposer(Type type)
        {
            this.Type = type;
#if NETFX_CORE
            InitializeInheritanceChain();
#endif
        }

#if !NETFX_CORE
        private BindingFlags BindingType
        {
            get
            {
                return Object != null ? BindingFlags.Instance : BindingFlags.Static;
            }
        }
#endif

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var key = binder.Name;
#if NETFX_CORE
            var member = (MemberInfo)typeInfos.FirstNonNull(x => x.GetDeclaredField(key)) ?? typeInfos.FirstNonNull(x => x.GetDeclaredProperty(key));
#else
            var member = this.Type.GetMembers(
            BindingType | BindingFlags.Public |
    BindingFlags.NonPublic | BindingFlags.GetProperty |
    BindingFlags.GetField).FirstOrDefault(x => x.Name == key);
#endif


            if (member != null)
            {
                var field = member as FieldInfo;
                if (field != null)
                {
                    result = ObjectConvertFromExposer(field.GetValue(Object));
                }
                else
                {
                    result = ((PropertyInfo)member).GetValue(Object, EmptyObjectArray);
                }
                return true;
            }
            else
            {
                return base.TryGetMember(binder, out result);
            }
        }

        private static object[] EmptyObjectArray = new object[] { };

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            var key = binder.Name;

#if NETFX_CORE            
            var member = (MemberInfo)typeInfos.FirstNonNull(x => x.GetDeclaredField(key)) ?? typeInfos.FirstNonNull(x => x.GetDeclaredProperty(key));
#else
            var member = this.Type.GetMembers(
            BindingType | BindingFlags.Public |
    BindingFlags.NonPublic | BindingFlags.SetProperty |
    BindingFlags.SetField).FirstOrDefault(x => x.Name == key);
#endif

            if (member != null)
            {
                var field = member as FieldInfo;
                if (field != null)
                {
                    field.SetValue(Object, ObjectConvertFromExposer(value));
                }
                else
                {
                    ((PropertyInfo)member).SetValue(Object, value, EmptyObjectArray);
                }
                return true;
            }
            else
            {
                return base.TrySetMember(binder, value);
            }
        }

        private static object ObjectConvertFromExposer(object value)
        {
            if (value == null) return null;
            var exposer = value as Exposer;
            return exposer != null ? exposer.Object : value;
        }

        public override bool TryInvokeMember
        (InvokeMemberBinder binder, object[] args, out object result)
        {
            ConvertExposerArrayToObjectArray(args);

            var argTypes = args.Select(arg => arg != null ? arg.GetType() : null).ToArray();

#if NETFX_CORE
            var method = typeInfos.FirstNonNull(y => y.GetDeclaredMethods(binder.Name).FirstOrDefault(x => ArgsMatch(x, argTypes)));
#else
            var method = this.Type.GetMethods
    (BindingFlags.Public | BindingFlags.NonPublic | BindingType)
    .FirstOrDefault(x => x.Name == binder.Name && ArgsMatch(x, argTypes));
#endif

            if (method != null)
            {
                result = ObjectConvertFromExposer(method.Invoke(Object, args));
                return true;
            }
            else
            {
                return base.TryInvokeMember(binder, args, out result);
            }
        }

        private static bool ArgsMatch(MethodInfo info, Type[] argTypes)
        {
            var parameters = info.GetParameters();
            if (argTypes.Length != parameters.Length) return false;

            for (int i = 0; i < parameters.Length; i++)
            {
                var currentType = argTypes[i];
                if (currentType != null && !parameters[i].ParameterType
#if NETFX_CORE
.GetTypeInfo()
#endif
.IsAssignableFrom(currentType
#if NETFX_CORE
.GetTypeInfo()
#endif
)) return false;
            }

            return true;
        }

        public static dynamic CreateObject(Assembly assembly, string typeName, params object[] args)
        {
            var type = assembly.GetType(typeName);
            if (type == null) throw new ArgumentException();
            return CreateObject(type, args);
        }

        public static dynamic CreateObject(Type type, params object[] args)
        {
            ConvertExposerArrayToObjectArray(args);
#if NETFX_CORE
            return Activator.CreateInstance(type, args);
#else
            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null).Expose();
#endif
        }

        public static dynamic Expose(object target)
        {
            if (target is Exposer) return target;
            return new Exposer(target);
        }

        public static dynamic ExposeStatics(Type target)
        {
            return new Exposer(target);
        }

        private static void ConvertExposerArrayToObjectArray(object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = ObjectConvertFromExposer(values[i]);
            }
        }

    }
}
namespace Shaman.Runtime.ReflectionExtensions
{
    public static class ExposerExtensionMethods
    {
        public static dynamic Expose(this object target)
        {
            return Exposer.Expose(target);
        }

        public static dynamic ExposeStatics(this Type target)
        {
            return Exposer.ExposeStatics(target);
        }

        internal static TResult FirstNonNull<TSource, TResult>(this IEnumerable<TSource> items, Func<TSource, TResult> func) where TResult : class
        {
            foreach (var item in items)
            {
                var result = func(item);
                if (result != null) return result;
            }
            return null;
        }
    }
}