using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using DocGen.Entities;
using Type = DocGen.Entities.Type;
using Exception = DocGen.Entities.Exception;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;

namespace DocGen.Parsing
{
    // This class parses XML documentation into the entities
    public class XMLDocParser
    {
        private Assembly assembly;
        private List<Type> types;

        public XMLDocParser(Assembly assembly,
            string? docs = null, string? file = null, Stream? stream = null)
        {
            ArgumentNullException.ThrowIfNull(assembly, nameof(assembly));
            if (docs == null && file == null && stream == null)
                throw new InvalidOperationException("You must specify at least one XML docs source");

            this.assembly = assembly;

            // Construct the entities based on types that the assembly contains
            // We don't yet fill in properties connected with documentation
            // (they will be filled in in the Parse method)
            types = assembly.GetTypes()
                // Filter out compiler-generated types
                .Where(t => t.GetCustomAttribute(typeof(CompilerGeneratedAttribute)) == null)
                .Select(t => {
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
                            // Replace ".ctor" with "#ctor", as it's stored in XML docs 
                            Name = m.Name.Replace('.', '#'),
                            Parameters = GetParameters(m),
                            Kind = GetKind(m),
                            ReturnType = GetReturnType(m),
                            GenericParameters = GetGenericParameters(m)
                        })
                    };

                    foreach (var member in type.Members) member.Type = type;  // #5
                    return type;
                }).ToList();

            if (docs != null) Parse(docs);
            else if (file != null) ParseFromFile(file);
            else if (stream != null) ParseFromStream(stream);
        }

        private IEnumerable<Type> ParseFromFile(string docsPath) =>
            Parse(File.ReadAllText(docsPath));

        private IEnumerable<Type> ParseFromStream(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }

        private IEnumerable<Type> Parse(string documentation)
        {
            var docs = XDocument.Parse(Process(documentation)!);

            for (int i = 0; i < types.Count; i++)
            {
                XElement? typeDocs = docs.XPathSelectElement($"//member[@name='T:{types[i].FullName}']");
                if (typeDocs == null) continue;

                types[i].Summary = typeDocs.Element("summary")?.Value
                    ?? string.Empty;

                // Notes consist of <remarks> and <seealso>
                // If they both exist, their contents are separated with two new lines
                string? remarks = typeDocs.Element("remarks")?.Value;
                string? seealso = typeDocs.Element("seealso")?.Value;
                types[i].Notes = Combine(remarks, seealso);

                for (int j = 0; j < types[i].GenericParameters.Count(); j++)
                {
                    types[i].GenericParameters.ElementAt(j).Description =
                        FindElementValueByName(typeDocs, "typeparam",
                            types[i].GenericParameters.ElementAt(j).Name);
                }

                // --------- Parse members ---------
                for (int j = 0; j < types[i].Members.Count(); j++)
                {
                    Member member = types[i].Members.ElementAt(j);
                    if (member.Kind == MemberKind.Unknown) continue;

                    var nameBuilder = new StringBuilder();

                    char prefix = member.Kind switch
                    {
                        MemberKind.Field => 'F',
                        MemberKind.Event => 'E',
                        MemberKind.Property => 'P',
                        _ => 'M'
                    };
                    nameBuilder.Append(prefix);

                    nameBuilder.Append(types[i].FullName);
                    nameBuilder.Append('.');
                    nameBuilder.Append(member.Name);

                    if (member.GenericParameters != null &&
                        member.GenericParameters.Count() > 0)
                    {
                        nameBuilder.Append('`' + member.GenericParameters.Count());
                    }
                    
                    if (prefix == 'M')
                    {
                        nameBuilder.Append('(');

                        foreach (var parameter in member.Parameters!)
                        {
                            nameBuilder.Append(parameter.Type.FullName);

                            var genericParameters = parameter.Type.GetGenericArguments();
                            if (genericParameters.Length > 0)
                            {
                                nameBuilder.Append('{');

                                foreach (System.Type genericParameter in genericParameters)
                                {
                                    // If full name is null or empty, it's a reference to
                                    // a generic parameter of the method or of the type
                                    if (string.IsNullOrEmpty(genericParameter.FullName))
                                    {
                                        // Such references are represented in XML docs as
                                        // `<member_typeparam_number>
                                        // if it's a generic parameter of the member
                                        // OR
                                        // `<member_typeparams_count>+<type_typeparam_number> 
                                        // if it's a generic parameter of the type

                                        int index = -1;

                                        // Search in generic parameters of the member first
                                        // They're not null because it's a method or constructor
                                        if (member.GenericParameters!.Count() > 0)
                                        {
                                            index = member.GenericParameters!
                                                .Select(p => p.Name)
                                                .ToList()
                                                .IndexOf(genericParameter.Name);
                                        }

                                        // It's a generic parameter of the type
                                        if (index < 0 &&
                                            types[i].GenericParameters.Count() > 0)
                                        {
                                            index = member.GenericParameters!
                                                .Select(p => p.Name)
                                                .ToList()
                                                .IndexOf(genericParameter.Name) +
                                                member.GenericParameters!.Count();
                                        }

                                        if (index >= 0) nameBuilder.Append('`' + index);
                                        // Falling back to Object if such generic parameter
                                        // not found
                                        else nameBuilder.Append("System.Object");
                                    }
                                    else  // Otherwise it's a hard-coded type reference
                                        nameBuilder.Append(genericParameter.FullName);

                                    nameBuilder.Append(',');
                                }

                                nameBuilder.Remove(nameBuilder.Length - 1, 1);
                                nameBuilder.Append('}');
                            }
                            nameBuilder.Append(',');
                        }

                        nameBuilder.Remove(nameBuilder.Length - 1, 1);
                        nameBuilder.Append(')');

                        // Casts (named op_Implicit or op_Explicit) have a special name format
                        if (member.Name == "op_Implicit" || member.Name == "op_Explicit")
                        {
                            nameBuilder.Append('~');
                            nameBuilder.Append(member.ReturnType!.FullName);
                        }
                    }

                    XElement? memberDocs = docs.XPathSelectElement($"//member[@name='{nameBuilder}']");
                    if (memberDocs == null) continue;

                    member.Summary = memberDocs.Element("summary")?.Value
                        ?? string.Empty;

                    // Return value consists of <returns> and <value>
                    string? returns = memberDocs.Element("returns")?.Value;
                    string? value = memberDocs.Element("value")?.Value;
                    member.ReturnValue = Combine(returns, value);

                    string? memberRemarks = memberDocs.Element("remarks")?.Value;
                    string? memberSeealso = memberDocs.Element("seealso")?.Value;
                    member.Notes = Combine(memberRemarks, memberSeealso);

                    if (member.Parameters != null)
                    {
                        for (int k = 0; k < member.Parameters.Count(); k++)
                        {
                            member.Parameters.ElementAt(k).Description =
                                FindElementValueByName(memberDocs, "param",
                                    member.Parameters.ElementAt(k).Name);
                        }
                    }

                    if (member.GenericParameters != null)
                    {
                        for (int k = 0; k < member.GenericParameters.Count(); k++)
                        {
                            member.GenericParameters.ElementAt(k).Description =
                                FindElementValueByName(memberDocs, "typeparam",
                                    member.GenericParameters.ElementAt(k).Name);
                        }
                    }

                    if (member.Kind != MemberKind.Field
                        && member.Kind != MemberKind.Event)
                    {
                        var exceptionElements = memberDocs.Elements("exception");
                        var exceptions = new List<Exception>();

                        foreach (var element in exceptionElements)
                        {
                            string? cref = element.Attribute("cref")?.Value;
                            if (cref == null) continue;

                            var exception = new Exception();

                            var type = assembly.GetType(cref[2..]);
                            // If it cannot find type, we fall back to Exception
                            // (as specified in the definition of the Type property)
                            if (type != null) exception.Type = type;

                            exception.ThrownOn = element.Value ?? string.Empty;

                            exceptions.Add(exception);
                        }

                        member.Exceptions = exceptions;
                    }
                }
            }

            return types;
        }

        public void Deconstruct(out Assembly assembly, out IEnumerable<Type> types)
        {
            assembly = this.assembly;
            types = this.types;
        }

        // Transform "<c>"s to asterisk-surrounded content
        // Reformat lists ("-+term+^description^all_the_rest"),
        // paragraphs (%paragraph%),
        // code examples (%*example*%),
        // <see> references ($referenced_name$&cref_attribute&)
        // Escape the special characters used
        private string? Process(string? raw)
        {
            if (raw == null) return null;

            // Escaping
            string result = Regex.Replace(raw, @"[\\*\-+\^%$&]", m => "\\" + m.Value);

            // Inline code
            result = result.Replace("<c>", "*");
            result = result.Replace("</c>", "*");

            // Paragraphs
            result = result.Replace("<para>", "%");
            result = result.Replace("</para>", "%");

            // Code examples
            result = Regex.Replace(result, @"<code>|<example>|</code>|</example>",
                m => m.Value.Contains('/') ? "*%" : "%*");

            // Lists
            var document = XDocument.Parse(result);

            // Eager load the lists because they will be
            // disappearing as we reformat them
            var lists = document.Descendants("list").ToArray();
            foreach (var list in lists)
            {
                var listBuilder = new StringBuilder();

                foreach (var item in list.Descendants("item"))
                {
                    var term = item.Element("term");
                    var description = item.Element("description");

                    listBuilder.AppendLine($"-+{term?.Value.Trim()}+^{description?.Value.Trim()}^" +
                        // Also fetch text nodes that are direct children of the <item>
                        // and put them after term and description
                        string.Concat(item.Nodes().Where(n => n.NodeType == XmlNodeType.Text)
                        .Select(n => n.ToString())));
                }

                // Replace the list element with formatted text
                list.AddAfterSelf(new XText(listBuilder.ToString()));
                list.Remove();
            }

            // "<see>"s
            var sees = document.Descendants("see").ToArray();
            foreach (var see in sees)
            {
                string cref = see.Attribute("cref")!.Value;

                // Fetch short name
                // (from the last full stop before the first parenthesis to that parenthesis)
                string tillParenthesis = cref[..cref.IndexOf('(')];
                string shortName = tillParenthesis
                    [(tillParenthesis.LastIndexOf('.') + 1)..];

                // Only distinguish between types and members
                // (all the rest is stored in MemberKind)
                if (cref[0] != 'T') cref = string.Concat("M", cref.AsSpan(1));

                see.AddAfterSelf(new XText($"${shortName}$&{cref}&"));
                see.Remove();
            }

            // Parameter and type parameter references
            var references = document.Descendants()
                .Where(el => el.Name == "paramref" || el.Name == "typeparamref")
                .ToArray();
            foreach (var reference in references)
            {
                string? name = reference.Attribute("name")?.Value;
                if (name == null) continue;

                reference.AddAfterSelf(new XText($"*{name}*"));
                reference.Remove();
            }

            string resultWithRoot = document.ToString();
            // Remove <root> and </root>
            result = resultWithRoot[6..^7];

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
                    Type = p.ParameterType,
                    IsReference = p.IsOut || p.ParameterType.IsByRef
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

        private System.Type? GetReturnType(MemberInfo member)
        {
            if (member is FieldInfo field) return field.FieldType;
            if (member is EventInfo @event) return @event.EventHandlerType;
            if (member is MethodInfo method) return method.ReturnType;
            if (member is PropertyInfo property) return property.PropertyType;
            if (member is ConstructorInfo constructor) return constructor.DeclaringType;

            return null;
        }

        private IEnumerable<GenericParameter>? GetGenericParameters(MemberInfo member)
        {
            if (member is MethodInfo method)
                return method.GetGenericArguments().Select(p => new GenericParameter
                {
                    Name = p.Name
                });

            return null;
        }

        // In root, searches for an element called searchFor with a name attribute with value compareTo
        // and returns value of the element
        private string FindElementValueByName(XElement root, string searchFor, string compareTo)
        {
            return root.Elements(searchFor).FirstOrDefault(p =>
            {
                string? name = p.Attribute("name")?.Value;
                if (name == null) return false;
                return name == compareTo;
            })?.Value ?? string.Empty;
        }

        private string Combine(string? str1, string? str2)
        {
            if (str1 == null && str2 == null)
                return string.Empty;
            else if (str2 == null)
                return str1!;
            else if (str1 == null)
                return str2;
            else
                return $"{str1}\n\n{str2}";
        }
        #endregion
    }
}
