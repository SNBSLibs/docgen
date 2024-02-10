using System.Collections;
using System.Diagnostics;
using System.Text;
using DocGen.Entities.Exceptions;

namespace DocGen.Parsing
{
    public static class HTMLTemplateParser
    {
        public static string Parse(object obj,
            string? template = null, string? file = null, Stream? stream = null)
        {
            ArgumentNullException.ThrowIfNull(obj, nameof(obj));
            if (template == null && file == null && stream == null)
                throw new InvalidOperationException("You must specify at least one HTML template source");

            if (template != null) return ParseFromText(template, obj);
            else if (file != null) return ParseFromFile(file, obj);
            else if (stream != null) return ParseFromStream(stream, obj);
            // If template, file and stream are all null, there will be an exception at line 10
            // So the following line won't execute
            else return null!;
        }

        private static string ParseFromFile(string templatePath, object obj) =>
            ParseFromFile(File.ReadAllText(templatePath), obj);

        private static string ParseFromStream(Stream stream, object obj)
        {
            using var reader = new StreamReader(stream);
            return ParseFromText(reader.ReadToEnd(), obj);
        }

        private static string ParseFromText(string template, object obj)
        {
            var braces = GetCurlyBraces(template);
            var contexts = FetchContexts(template, braces);

            foreach (var context in contexts)
            {
                char contextType = context.Item3[0];

                switch (contextType)
                {
                    case '!':
                        string propertyName = context.Item3[1..];
                        var value = FetchProperty(obj, propertyName);

                        if (value != null)
                        {
                            if (value is not bool || (bool)value)
                            {
                                if (value is not IEnumerable ||
                                    ((IEnumerable)value).Count() > 0)
                                {
                                    // Leave the context untouched
                                    template.Remove(context.Item1, 1);
                                    template.Remove(context.Item2, 1);
                                    break;
                                }
                            }
                        }

                        template.Remove(context.Item1,
                            context.Item2 - context.Item1 + 1);
                        break;
                    case '?':
                        string propertyName2 = context.Item3[1..];
                        var value2 = FetchProperty(obj, propertyName2);

                        if (value2 == null)
                        {
                            template.Remove(context.Item1, 1);
                            template.Remove(context.Item2, 1);
                            break;
                        }

                        if (value2 is not bool || !(bool)value2)
                        {
                            if (value2 is not IEnumerable ||
                                ((IEnumerable)value2).Count() == 0)
                            {
                                template.Remove(context.Item1, 1);
                                template.Remove(context.Item2, 1);
                                break;
                            }
                        }

                        template.Remove(context.Item1,
                            context.Item2 - context.Item1 + 1);
                        break;
                    case '*':
                        string propertyName3 = context.Item3[1..];
                        var value3 = FetchProperty(obj, propertyName3);
                        var enumerable = value3 as IEnumerable;

                        if (enumerable == null || enumerable.Count() == 0)
                        {
                            template.Remove(context.Item1,
                                context.Item2 - context.Item1 + 1);
                            break;
                        }

                        string contents = template[(context.Item1 + 1)..context.Item2];
                        template.Remove(context.Item1,
                            context.Item2 - context.Item1 + 1);

                        var builder = new StringBuilder();
                        foreach (var item in enumerable)
                        {
                            builder.AppendLine(contents);
                        }

                        template = template.Insert(context.Item1, builder.ToString());
                        break;
                    default:
                        int index = context.Item1 - context.Item3.Length;
                        var lineAndColumn = GetLineAndColumn(template, index);

                        throw new HTMLTemplateSyntaxException(
                            $"Unexpected '{contextType}' at line {lineAndColumn.Item1}" +
                            $" col {lineAndColumn.Item2}")
                        {
                            Line = lineAndColumn.Item1,
                            Column = lineAndColumn.Item2
                        };
                }
            }
        }

        private static List<Tuple<int, int, string>> FetchContexts(string text,
            Tuple<int, bool>[] braces)
        {
            var contexts = new List<Tuple<int, int, string>>();

            var unclosedBraces = new List<Tuple<int, string>>();
            foreach (var brace in braces)
            {
                if (brace.Item2 == true)
                {
                    int lastExclamation = text.LastIndexOf('!', brace.Item1);
                    int lastQuestion = text.LastIndexOf('?', brace.Item1);
                    int lastAsterisk = text.LastIndexOf('*', brace.Item1);
                    int lastSpecialChar =
                        Math.Max(lastExclamation, Math.Max(lastQuestion, lastAsterisk));

                    string contextString =
                        text.Substring(lastSpecialChar, brace.Item1 - lastSpecialChar);

                    unclosedBraces.Add(Tuple.Create(brace.Item1, contextString));

                    // After a context string has been fetched, it is not necessary to keep it
                    text = text.Remove(lastSpecialChar, brace.Item1 - lastSpecialChar);
                }
                else
                {
                    if (unclosedBraces.Count == 0)
                    {
                        var lineAndColumn = GetLineAndColumn(text, brace.Item1);

                        throw new HTMLTemplateSyntaxException(
                            $"Unexpected '}}' at line {lineAndColumn.Item1}" +
                            $" col {lineAndColumn.Item2}")
                        {
                            Line = lineAndColumn.Item1,
                            Column = lineAndColumn.Item2
                        };
                    }

                    contexts.Add(Tuple.Create(
                        unclosedBraces[unclosedBraces.Count - 1].Item1,
                        brace.Item1,
                        unclosedBraces[unclosedBraces.Count - 1].Item2));
                    unclosedBraces.RemoveAt(unclosedBraces.Count - 1);
                }
            }

            if (unclosedBraces.Count > 0)
            {
                var lineAndColumn = GetLineAndColumn(text,
                    unclosedBraces[unclosedBraces.Count - 1].Item1);

                throw new HTMLTemplateSyntaxException(
                    $"Unclosed '{{' at line {lineAndColumn.Item1}" +
                    $" col {lineAndColumn.Item2}")
                {
                    Line = lineAndColumn.Item1,
                    Column = lineAndColumn.Item2
                };
            }

            return contexts;
        }

        private static object? FetchProperty(object obj, string propertyName)
        {
            if (obj == null) return null;

            var property = obj.GetType().GetProperty(propertyName);
            if (property == null) return null;

            return property.GetValue(obj);
        }

        #region Helpers
        private static Tuple<int, bool>[] GetCurlyBraces(string text)
        {
            var braces = new List<Tuple<int, bool>>();

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{' &&
                    (i == 0 || text[i - 1] == '\\')) braces.Add(Tuple.Create(i, true));
                if (text[i] == '}' &&
                    (i == 0 || text[i - 1] == '\\')) braces.Add(Tuple.Create(i, false));
            }

            return braces.ToArray();
        }

        private static Tuple<int, int> GetLineAndColumn(string text, int index)
        {
            int line = text.Substring(0, index)
                .Where(c => c == '\n')
                .Count();
            int column = index - text.LastIndexOf('\n', index) + 1;

            return Tuple.Create(line, column);
        }
        #endregion
    }

    internal static class IEnumerableExtensions
    {
        public static int Count(this IEnumerable enumerable)
        {
            int count = 0;
            foreach (var item in enumerable) count++;
            return count;
        }
    }
}
