using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DocGen.Entities;

namespace DocGen.Parsing
{
    // This class parses XML documentation into the entities
    public static class XMLDocParser
    {
        // Always initialized when the Parse method is called
        private static List<TypeInformation> types = null!;

        public static IEnumerable<TypeInformation> Parse(Assembly assembly,
            string? docs = null, string? file = null, Stream? stream = null)
        {
            ArgumentNullException.ThrowIfNull(assembly, nameof(assembly));
            if (docs == null && file == null && stream == null)
                throw new InvalidOperationException("You must specify at least one XML docs source");

            // Construct the entities based on types that the assembly contains
            // We don't yet fill in properties connected with documentation
            // (they will be filled in in the Parse method)
            types = assembly.GetTypes()
                .Where(t => t.IsVisible)
                // Filter out compiler-generated types
                .Where(t => t.GetCustomAttribute(typeof(CompilerGeneratedAttribute)) == null)
                .Select(t => {
                    var type = new TypeInformation
                    {
                        Name = t.Name,
                        FullName = t.FullName ?? t.Name,
                        GenericParameters = t.GetGenericArguments().Select(p => new GenericParameterInformation
                        {
                            Name = p.Name
                        }).ToArray(),
                        Members = t.GetMembers().Select(m => new MemberInformation
                        {
                            // Replace ".ctor" with "#ctor", as it's stored in XML docs 
                            Name = m.Name.Replace('.', '#'),
                            Accessors = GetAccessors(m),
                            Parameters = GetParameters(m),
                            Kind = GetKind(m),
                            ReturnType = GetReturnType(m),
                            GenericParameters = GetGenericParameters(m)
                        }).ToArray()
                    };

                    for (int i = 0; i < type.Members.Length; i++)
                    {
                        // Remove methods like get_AProperty or add_AnEvent
                        // Only leave the corresponding properties/events
                        if (
                            type.Members[i].Name.StartsWith("get_") ||
                            type.Members[i].Name.StartsWith("set_") ||
                            type.Members[i].Name.StartsWith("add_") ||
                            type.Members[i].Name.StartsWith("remove_")
                        )
                        {
                            string accessor = type.Members[i].Name[
                                ..type.Members[i].Name.IndexOf('_')
                            ];
                            string restOfName = type.Members[i].Name[
                                (type.Members[i].Name.IndexOf('_') + 1)..
                            ];
                            if (
                                type.Members.Any(m =>
                                    // Wasn't m removed?
                                    m != null &&
                                    m.Name == restOfName &&
                                    // Methods like set_AProperty are allowed
                                    // if the corresponding property doesn't have 
                                    // a "set" accessor
                                    (m.Accessors?.Contains(accessor) ?? false))
                            )
                                // Removed methods are assigned to null first
                                type.Members[i] = null!;
                        }

                        if (type.Members[i] != null)
                            type.Members[i].Type = type;  // #5
                    }

                    // Filter out removed methods
                    type.Members = type.Members
                        .Where(m => m != null)
                        .ToArray();

                    return type;
                }).ToList();

            if (docs != null) return ParseFromText(docs);
            else if (file != null) return ParseFromFile(file);
            else if (stream != null) return ParseFromStream(stream);
            // If docs, file and stream are all null, there will be an exception at line 26
            // So the following line won't execute
            else return null!;
        }

        private static IEnumerable<TypeInformation> ParseFromFile(string docsPath) =>
            ParseFromText(File.ReadAllText(docsPath));

        private static IEnumerable<TypeInformation> ParseFromStream(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return ParseFromText(reader.ReadToEnd());
        }

        private static IEnumerable<TypeInformation> ParseFromText(string documentation)
        {
            string? temp = Process(documentation);
            var docs = XDocument.Parse(Process(documentation)!);

            for (int i = 0; i < types.Count; i++)
            {
                XElement? typeDocs;

                var options = docs.Descendants("member")
                    .Where(m => m.Attribute("name")?.Value == $"T:{types[i].FullName}");
                if (options.Count() == 1) typeDocs = options.Single();
                else if (options.Count() < 1) typeDocs = null;
                else
                {
                    var exception = new AmbiguousMatchException
                        ($"Not exactly one <member> element in the docs matched {types[i].FullName}. See exception data for the matches.");
                    for (int j = 0; j < options.Count(); j++)
                        exception.Data.Add(j, options.ElementAt(j));
                    throw exception;
                }

                if (typeDocs == null) continue;

                types[i].Summary = RemoveIndents((typeDocs.Element("summary")?.Value
                    ?? string.Empty).Trim())!;

                // Notes consist of <remarks> and <seealso>
                // If they both exist, their contents are separated with two new lines
                string? remarks =
                    RemoveIndents(typeDocs.Element("remarks")?.Value.Trim());
                string? seealso =
                    RemoveIndents(typeDocs.Element("seealso")?.Value.Trim());
                types[i].Notes = Combine(remarks, seealso);

                for (int j = 0; j < types[i].GenericParameters.Count(); j++)
                {
                    string description = FindElementValueByName(typeDocs, "typeparam",
                        types[i].GenericParameters.ElementAt(j).Name).Trim();
                    types[i].GenericParameters[j].Description = RemoveIndents(description)!;
                }

                // --------- Parse members ---------
                for (int j = 0; j < types[i].Members.Count(); j++)
                {
                    Entities.MemberInformation member = types[i].Members[j];
                    if (member.Kind == MemberKind.Unknown) continue;

                    var nameBuilder = new StringBuilder();

                    string prefix = member.Kind switch
                    {
                        MemberKind.Field => "F:",
                        MemberKind.Event => "E:",
                        MemberKind.Property => "P:",
                        _ => "M:"
                    };
                    nameBuilder.Append(prefix);

                    nameBuilder.Append(types[i].FullName);
                    nameBuilder.Append('.');
                    nameBuilder.Append(member.Name);

                    if (member.GenericParameters != null &&
                        member.GenericParameters.Count() > 0)
                    {
                        nameBuilder.Append("`" + member.GenericParameters.Count());
                    }
                    
                    if (prefix == "M:")
                    {
                        nameBuilder.Append('(');

                        foreach (var parameter in member.Parameters!)
                        {
                            string fullName;
                            if (parameter.Type.IsGenericType)
                            {
                                fullName = parameter.Type.GetGenericTypeDefinition().FullName!;
                                fullName = fullName[..fullName.IndexOf('`')];
                            }
                            else fullName = parameter.Type.FullName!;
                            nameBuilder.Append(fullName);

                            var genericParameters = parameter.Type.GenericTypeArguments;
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
                                        // ``<member_typeparam_number>
                                        // if it's a generic parameter of the member
                                        // OR
                                        // `<type_typeparam_number> 
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

                                        if (index >= 0)
                                            nameBuilder.Append("``" + index);
                                        else
                                        {
                                            // It's a generic parameter of the type
                                            if (types[i].GenericParameters.Count() > 0)
                                            {
                                                index = member.GenericParameters!
                                                    .Select(p => p.Name)
                                                    .ToList()
                                                    .IndexOf(genericParameter.Name) +
                                                    member.GenericParameters!.Count();
                                            }

                                            if (index >= 0) nameBuilder.Append("`" + index);
                                            // Falling back to Object if such generic parameter
                                            // not found
                                            else nameBuilder.Append("System.Object");
                                        }
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

                    XElement? memberDocs;

                    var options2 = docs.Descendants("member")
                        .Where(m => m.Attribute("name")?.Value == nameBuilder.ToString());
                    if (options2.Count() == 1) memberDocs = options2.Single();
                    else if (options2.Count() < 1) memberDocs = null;
                    else
                    {
                        var exception = new AmbiguousMatchException
                            ($"Not exactly one <member> element in the docs matched {nameBuilder}. See exception data for the matches.");
                        for (int k = 0; k < options2.Count(); k++)
                            exception.Data.Add(k, options2.ElementAt(k));
                        throw exception;
                    }

                    if (memberDocs == null) continue;

                    member.Summary = RemoveIndents((memberDocs.Element("summary")?.Value
                        ?? string.Empty).Trim())!;

                    // Return value consists of <returns> and <value>
                    string? returns =
                        RemoveIndents(memberDocs.Element("returns")?.Value.Trim());
                    string? value =
                        RemoveIndents(memberDocs.Element("value")?.Value.Trim());
                    member.ReturnDescription = Combine(returns, value);

                    string? memberRemarks =
                        RemoveIndents(memberDocs.Element("remarks")?.Value.Trim());
                    string? memberSeealso =
                        RemoveIndents(memberDocs.Element("seealso")?.Value.Trim());
                    member.Notes = Combine(memberRemarks, memberSeealso);

                    if (member.Parameters != null)
                    {
                        for (int k = 0; k < member.Parameters.Count(); k++)
                        {
                            string description = FindElementValueByName(memberDocs, "param",
                                    member.Parameters.ElementAt(k).Name).Trim();
                            member.Parameters[k].Description = RemoveIndents(description)!;
                        }
                    }

                    if (member.GenericParameters != null)
                    {
                        for (int k = 0; k < member.GenericParameters.Count(); k++)
                        {
                            string description = FindElementValueByName(memberDocs, "typeparam",
                                    member.GenericParameters.ElementAt(k).Name).Trim();
                            member.GenericParameters[k].Description = RemoveIndents(description)!;
                        }
                    }

                    if (member.Kind != MemberKind.Field
                        && member.Kind != MemberKind.Event)
                    {
                        var exceptionElements = memberDocs.Elements("exception");
                        var exceptions = new List<ExceptionInformation>();

                        foreach (var element in exceptionElements)
                        {
                            string? cref = element.Attribute("cref")?.Value;
                            if (cref == null) continue;

                            var exception = new ExceptionInformation
                            {
                                Type = cref[2..],
                                ThrownOn = RemoveIndents(element.Value.Trim()) ?? string.Empty
                            };
                            exceptions.Add(exception);
                        }

                        member.Exceptions = exceptions.ToArray();
                    }
                }
            }

            return types;
        }

        // Transform "<c>"s to asterisk-surrounded content
        // Reformat lists ("-+term+^description^all_the_rest"),
        // paragraphs (%paragraph%),
        // code examples (%*example*%),
        // <see> references ($referenced_name$&cref_attribute&)
        // Escape the special characters used
        private static string? Process(string? raw)
        {
            if (raw == null) return null;

            // Escaping
            string result = Regex.Replace(raw, @"[*\-+\^%$&]", m => "\\" + m.Value);

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

                string shortName;
                int parenthesis = cref.IndexOf('(');
                if (parenthesis >= 0)
                {
                    // Fetch short name
                    // (from the last full stop before the first parenthesis to that parenthesis)
                    string tillParenthesis = cref[..parenthesis];
                    shortName = tillParenthesis[(tillParenthesis.LastIndexOf('.') + 1)..];
                }
                else shortName = cref[(cref.LastIndexOf('.') + 1)..];

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

            return document.ToString();
        }

        #region Helpers
        private static string[]? GetAccessors(System.Reflection.MemberInfo member)
        {
            if (member.MemberType != MemberTypes.Property &&
                member.MemberType != MemberTypes.Event) return null;

            if (member.MemberType == MemberTypes.Property)
            {
                var property = (PropertyInfo)member;
                return property.GetAccessors(false)
                    .Select(a => a.Name[..a.Name.IndexOf('_')])
                    .ToArray();
            } 
            // If the member is not a property, it is an event
            // Events always have "add" and "remove" accessors
            else return new string[] { "add", "remove" };
        }

        private static ParameterInformation[]? GetParameters(System.Reflection.MemberInfo member)
        {
            if (member.MemberType != MemberTypes.Method &&
                member.MemberType != MemberTypes.Constructor) return null;

            if (member.MemberType == MemberTypes.Method)
            {
                var method = (MethodInfo)member;
                return method.GetParameters().Select(p => new ParameterInformation
                {
                    Name = p.Name ?? "param",  // This is the default name for a parameter
                    Type = p.ParameterType
                }).ToArray();
            } else  // The member is a constructor
            {
                var ctor = (ConstructorInfo)member;
                return ctor.GetParameters().Select(p => new ParameterInformation
                {
                    Name = p.Name ?? "param",
                    Type = p.ParameterType,
                    IsReference = p.IsOut || p.ParameterType.IsByRef
                }).ToArray();
            }
        }

        private static MemberKind GetKind(System.Reflection.MemberInfo member)
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

        private static System.Type? GetReturnType(System.Reflection.MemberInfo member)
        {
            if (member is FieldInfo field) return field.FieldType;
            if (member is EventInfo @event) return @event.EventHandlerType;
            if (member is MethodInfo method) return method.ReturnType;
            if (member is PropertyInfo property) return property.PropertyType;
            if (member is ConstructorInfo constructor) return constructor.DeclaringType;

            return null;
        }

        private static GenericParameterInformation[]? GetGenericParameters(System.Reflection.MemberInfo member)
        {
            if (member is MethodInfo method)
                return method.GetGenericArguments().Select(p => new GenericParameterInformation
                {
                    Name = p.Name
                }).ToArray();

            return null;
        }

        // In root, searches for an element called searchFor with a name attribute with value compareTo
        // and returns value of the element
        private static string FindElementValueByName(XElement root, string searchFor, string compareTo)
        {
            return root.Elements(searchFor).FirstOrDefault(p =>
            {
                string? name = p.Attribute("name")?.Value;
                if (name == null) return false;
                return name == compareTo;
            })?.Value ?? string.Empty;
        }

        private static string Combine(string? str1, string? str2)
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

        private static string? RemoveIndents(string? text) =>
            (text == null) ? null : Regex.Replace(text,
                @"^[\t ]+", string.Empty, RegexOptions.Multiline);
        #endregion
    }
}