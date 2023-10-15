using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using DocGen.Entities;
using Type = DocGen.Entities.Type;
using Exception = DocGen.Entities.Exception;

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
                        // Replace ".ctor" with "#ctor", as it's stored in XML docs 
                        Name = m.Name.Replace('.', '#'),
                        Parameters = GetParameters(m),
                        Kind = GetKind(m)
                    })
                };

                foreach (var member in type.Members) member.Type = type;  // #5
                return type;
            }).ToList();
        }

        internal IEnumerable<Type> Parse(string docsPath)
        {
            var docs = XDocument.Load(docsPath);

            for (int i = 0; i < types.Count; i++)
            {
                string? typeDocsXml = Process(
                    (string)docs.XPathEvaluate($"//member[@name='T:{types[i].FullName}']"));
                if (typeDocsXml == null) continue;
                // XElement.Parse won't parse a string that doesn't start with an XML element
                var typeDocs = XElement.Parse($"<root>{typeDocsXml}</root>");

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
                        typeDocs.Elements("typeparam").FirstOrDefault(p =>
                        {
                            string? name = p.Attribute("name")?.Value;
                            if (name == null) return false;
                            return types[i].GenericParameters.ElementAt(j)
                                .Name == name;
                        })?.Value ?? string.Empty;
                }

                // --------- Parse members ---------
                for (int j = 0; j < types[i].Members.Count(); j++)
                {
                    Member member = types[i].Members.ElementAt(j);
                    if (member.Kind == MemberKind.Unknown) continue;

                    // Construct a string to look for in the docs
                    // (e.g. "M:System.Int32.Parse()")
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
                    
                    if (prefix == 'M')
                    {
                        nameBuilder.Append('(');

                        foreach (var parameter in member.Parameters!)
                        {
                            nameBuilder.Append(parameter.Type.FullName);
                            nameBuilder.Append(',');
                        }

                        nameBuilder.Remove(nameBuilder.Length - 1, 1);
                        nameBuilder.Append(')');
                    }

                    string? memberDocsXml = Process(
                        (string)docs.XPathEvaluate($"//member[@name='{nameBuilder}']"));
                    if (memberDocsXml == null) continue;
                    var memberDocs = XElement.Parse($"<root>{memberDocsXml}</root>");

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
                                memberDocs.Elements("param").FirstOrDefault(p =>
                                {
                                    string? name = p.Attribute("name")?.Value;
                                    if (name == null) return false;
                                    return member.Parameters.ElementAt(k)
                                        .Name == name;
                                })?.Value ?? string.Empty;
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

                            var type = System.Type.GetType(cref[2..]);
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

        internal void GetData(out Assembly assembly, out IEnumerable<Type> types)
        {
            assembly = this.assembly;
            types = this.types;
        }

        // Transform "<c>"s to asterisk-surrounded content
        // Reformat lists ("-+term+=description=all_the_rest"),
        // paragraphs (%paragraph%),
        // code examples (%*example*%),
        // <see> references ($referenced_name$&cref_attribute&)
        // Escape the special characters used
        private string? Process(string? raw)
        {
            if (raw == null) return null;

            // Escaping
            string result = Regex.Replace(raw, @"[\\*\-+=%$&]", m => "\\" + m.Value);

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
            // XElement.Parse won't parse a string that doesn't start with an XML element
            var element = XElement.Parse($"<root>{result}</root>");

            foreach (var list in element.Descendants("list"))
            {
                var listBuilder = new StringBuilder();

                foreach (var item in list.Descendants("item"))
                {
                    var term = item.Element("term");
                    var description = item.Element("description");

                    listBuilder.AppendLine($"-+{term?.Value.Trim()}+={description?.Value.Trim()}=" +
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
            foreach (var see in element.Descendants("see"))
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
            foreach (var reference in element.Descendants()
                .Where(el => el.Name == "<paramref>" || el.Name == "typeparamref"))
            {
                string? name = reference.Attribute("name")?.Value;
                if (name == null) continue;

                reference.AddAfterSelf(new XText($"*{name}*"));
                reference.Remove();
            }

            string resultWithRoot = element.ToString();
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
