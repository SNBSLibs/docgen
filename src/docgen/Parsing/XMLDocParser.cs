using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Linq;
using System.Text;
using DocGen.Entities;
using Type = DocGen.Entities.Type;

namespace DocGen.Parsing
{
    // This class parses XML documentation into the entities

    internal class XMLDocParser
    {
        private Assembly assembly;
        private List<Type> types;

        internal XMLDocParser(Assembly assembly)
        {
            this.assembly = assembly;

            // Construct the entities based on types that the assembly contains
            // We don't yet fill in properties connected with documentation
            // (they will be filled in in the Parse method)
            types = assembly.GetTypes().Select(t => {
                var type = new Type
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
                };

                foreach (var member in type.Members) member.Type = type;  // #5
                return type;
            }).ToList();
        }

        // Remove elements like <c>, <see> etc and surround their contents with asterisks
        // (to highlight them with special font in HTML)
        // Escape existing asterisks with \
        // Also reformatting lists: "- +term+ =description=" and escaping - + =
        private string? Process(string? raw)
        {
            if (raw == null) return null;

            string result = raw.Replace("*", "\\*");
            result = string.Join('*', result.Split(new string[]
                { "<c>", "</c>", "<code>", "</code>", "<example>", "</example>", "<see>", "</see>" },
                StringSplitOptions.RemoveEmptyEntries));

            result = result.Replace("-", "\\-");
            result = result.Replace("+", "\\+");
            result = result.Replace("=", "\\=");

            var element = XElement.Parse($"<root>{result}</root>");
            foreach (var list in element.Descendants("list"))
            {
                var listBuilder = new StringBuilder();

                foreach (var item in list.Descendants("item"))
                {
                    var term = item.Element("term");
                    var description = item.Element("description");

                    listBuilder.AppendLine($"- +{term?.Value.Trim()}+ ={description?.Value.Trim()}=");
                }

                list.AddAfterSelf(new XText(listBuilder.ToString()));
                list.Remove();
            }
            string resultWithRoot = element.ToString();
            result = resultWithRoot.Substring(6, resultWithRoot.Length - 13);  // Remove <root> and </root>

            return result;
        }

        #region Helpers
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
        #endregion
    }
}
