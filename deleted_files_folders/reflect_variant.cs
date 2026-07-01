using System;
using System.Reflection;
using Tmds.DBus.Protocol;

namespace ReflectTest2
{
    class Program
    {
        static void Main()
        {
            var type = typeof(VariantValue);
            Console.WriteLine($"Type: {type.FullName}");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var parameters = string.Join(", ", Array.ConvertAll(method.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"{method.ReturnType.Name} {method.Name}({parameters})");
            }
        }
    }
}
