using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Linq;
using DocGen.Entities;
using Type = DocGen.Entities.Type;

namespace DocGen.Parsing
{
    // This class parses XML documentation into the entities

    internal class XMLDocParser
    {
        private string docsPath;
        private Assembly assembly;
        private List<Type> types;

        internal XMLDocParser(string docsPath, Assembly assembly)
        {
            this.docsPath = docsPath;
            this.assembly = assembly;

            // Construct the entities based on types that the assembly contains
            // We don't yet fill in properties connected with documentation
            // (they will be filled in in the Parse method)
            types = assembly.GetTypes().Select(t => new Type
            {
                Name = t.Name,
                FullName = t.FullName ?? t.Name,
                GenericParameters = t.GetGenericArguments().Select(p => new GenericParameter
                {
                    Name = p.Name
                }),
                Members = t.GetMembers().Select(m => new Member
                {
                    Name = m.Name,
                    Parameters = GetParameters(m),
                    Kind = GetKind(m)
                })
            }).ToList();

            // Unfortunately, it isn't possible to use the object being constructed
            // in its initializer, so we have to set the Type properties of newly
            // constructed Members separately
            foreach (var type in types)
            {
                foreach (var member in type.Members) member.Type = type;
            }
        }

        private IEnumerable<Parameter>? GetParameters(MemberInfo member)
        {
            if (member.MemberType != MemberTypes.Method &&
                member.MemberType != MemberTypes.Constructor) return null;

            if (member.MemberType == MemberTypes.Method)
            {
                var method = (MethodInfo)member;
                return method.GetParameters().Select(p => new Parameter
                {
                    Name = p.Name ?? "param",  // This is the default name for a parameter
                    Type = p.ParameterType
                });
            } else  // The member is a constructor
            {
                var ctor = (ConstructorInfo)member;
                return ctor.GetParameters().Select(p => new Parameter
                {
                    Name = p.Name ?? "param",
                    Type = p.ParameterType
                });
            }
        }

        private MemberKind GetKind(MemberInfo member)
        {
            return member.MemberType switch
            {
                MemberTypes.Field => MemberKind.Field,
                MemberTypes.Property => MemberKind.Property,
                MemberTypes.Event => MemberKind.Event,
                MemberTypes.Method => MemberKind.Method,
                MemberTypes.Constructor => MemberKind.Constructor,
                _ => MemberKind.Unknown
            };
        }
    }
}
