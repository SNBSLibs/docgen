using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Mime;
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
            ParseFromText(File.ReadAllText(templatePath), obj);

        private static string ParseFromStream(Stream stream, object obj)
        {
            using var reader = new StreamReader(stream);
            return ParseFromText(reader.ReadToEnd(), obj);
        }

        private static string ParseFromText(string template, object obj)
        {
            var curlyBraces = GetCurlyBraces(template);
            var result = FetchContexts(template, curlyBraces);
            var contexts = result.Item2;
            template = result.Item1;

            var braces = GetBraces(template);
            var placeholders = FetchPlaceholders(template, braces);

            for (int i = 0; i < placeholders.Count; i++)
            {
                var item = placeholders[i];

                if (item.Item1 < 0) continue;
                if (GetParentEnumerations(contexts, item.Item1) > 0) continue;

                string placeholder = template[item.Item1..(item.Item2 + 1)];
                char modifier = placeholder[1];
                string propertyName = (modifier == '-' || modifier == '#')
                    ? placeholder[2..^1] : placeholder[1..^1];

                var value = FetchProperty(obj, propertyName);
                string inserting;
                if (modifier == '-')
                {
                    inserting = value?.ToString() ?? string.Empty;
                    if (inserting.Length > 1)
                        inserting = inserting[0].ToString().ToLower() + inserting[1..];
                    else if (inserting.Length > 0) inserting = inserting.ToLower();
                }
                else if (modifier == '#')
                {
                    string[]? strings = value as string[];
                    if (strings == null) inserting = string.Empty;
                    else inserting = string.Join(", ", strings);
                }
                else inserting = value?.ToString() ?? string.Empty;

                template = Remove(template, item.Item1,
                    item.Item2 - item.Item1 + 1, ref contexts, ref placeholders);
                template = Insert(template, inserting, item.Item1, ref contexts,
                    ref placeholders);
            }

            for (int i = 0; i < contexts.Count; i++)
            {
                if (contexts[i].Item1 < 0) continue;  // Context has gone

                string contextType = contexts[i].Item3[0].ToString();

                switch (contextType)
                {
                    case "!":
                        template = ParseNotNull(template, ref contexts,
                            ref placeholders, i, obj, 0);
                        break;
                    case "?":
                        template = ParseNull(template, ref contexts,
                            ref placeholders, i, obj, 0);
                        break;
                    case "*":
                        template = ParseEnumeration(template, ref contexts,
                            ref placeholders, i, obj, 0);
                        break;
                    default:
                        int index = contexts[i].Item1 - contexts[i].Item3.Length;
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

            return RemoveEscapes(template);
        }

        private static Tuple<string, List<Tuple<int, int, string>>> FetchContexts(string text,
            List<Tuple<int, bool>> braces)
        {
            var contexts = new List<Tuple<int, int, string>>();

            var unclosedBraces = new List<Tuple<int, string>>();
            for (int j = 0; j < braces.Count; j++)
            {
                var brace = braces[j];

                if (brace.Item2 == true)
                {
                    int lastExclamation = text.LastIndexOf('!', brace.Item1);
                    int lastQuestion = text.LastIndexOf('?', brace.Item1);
                    int lastAsterisk = text.LastIndexOf('*', brace.Item1);
                    int lastSpecialChar =
                        Math.Max(lastExclamation, Math.Max(lastQuestion, lastAsterisk));
                    
                    string contextString = string.Empty;
                    if (lastSpecialChar >= 0)
                    {
                        contextString =
                            text.Substring(lastSpecialChar, brace.Item1 - lastSpecialChar);
                    }
                    
                    if (lastSpecialChar < 0 ||
                        contextString.Contains(' ') ||
                        contextString.Contains('\t') ||
                        contextString.Contains('\n'))
                    {
                        var lineAndColumn = GetLineAndColumn(text, brace.Item1);

                        throw new HTMLTemplateSyntaxException(
                            $"Property name expected at line {lineAndColumn.Item1}" +
                            $" col {lineAndColumn.Item2}")
                        {
                            Line = lineAndColumn.Item1,
                            Column = lineAndColumn.Item2
                        };
                    }

                    unclosedBraces.Add(Tuple.Create(brace.Item1 - contextString.Length, contextString));

                    // After a context string has been fetched, it is not necessary to keep it
                    text = text.Remove(lastSpecialChar, brace.Item1 - lastSpecialChar);

                    for (int i = 0; i < braces.Count; i++)
                    {
                        if (braces[i].Item1 > brace.Item1)
                            braces[i] = Tuple.Create(braces[i].Item1 - contextString.Length,
                                braces[i].Item2);
                    }
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
                        unclosedBraces[^1].Item1,
                        brace.Item1,
                        unclosedBraces[^1].Item2));
                    unclosedBraces.RemoveAt(unclosedBraces.Count - 1);
                }
            }

            if (unclosedBraces.Count > 0)
            {
                var lineAndColumn = GetLineAndColumn(text,
                    unclosedBraces[^1].Item1);

                throw new HTMLTemplateSyntaxException(
                    $"Unclosed '{{' at line {lineAndColumn.Item1}" +
                    $" col {lineAndColumn.Item2}")
                {
                    Line = lineAndColumn.Item1,
                    Column = lineAndColumn.Item2
                };
            }

            return Tuple.Create(text, contexts);
        }

        private static List<Tuple<int, int>> FetchPlaceholders(string text,
            List<Tuple<int, bool>> braces)
        {
            var placeholders = new List<Tuple<int, int>>();

            var unclosedBraces = new List<int>();
            for (int j = 0; j < braces.Count; j++)
            {
                var brace = braces[j];

                if (brace.Item2 == true) unclosedBraces.Add(brace.Item1);
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

                    placeholders.Add(Tuple.Create(
                        unclosedBraces[^1],
                        brace.Item1));
                    unclosedBraces.RemoveAt(unclosedBraces.Count - 1);
                }
            }

            if (unclosedBraces.Count > 0)
            {
                var lineAndColumn = GetLineAndColumn(text,
                    unclosedBraces[^1]);

                throw new HTMLTemplateSyntaxException(
                    $"Unclosed '{{' at line {lineAndColumn.Item1}" +
                    $" col {lineAndColumn.Item2}")
                {
                    Line = lineAndColumn.Item1,
                    Column = lineAndColumn.Item2
                };
            }

            return placeholders;
        }

        private static string ParseNull(string template,
            ref List<Tuple<int, int, string>> contexts,
            ref List<Tuple<int, int>> placeholders, int i, object obj,
            int depth)
        {
            if (GetParentEnumerations(contexts, contexts[i].Item1) > depth) return template;

            string newTemplate = template;

            string propertyName = contexts[i].Item3[1..];
            var value = FetchProperty(obj, propertyName);

            if (value == null)
            {
                newTemplate = Remove(newTemplate, contexts[i].Item1, 1,
                    ref contexts, ref placeholders);
                newTemplate = Remove(newTemplate, contexts[i].Item2, 1,
                    ref contexts, ref placeholders);
                return newTemplate;
            }

            if (value is not bool || !(bool)value)
            {
                if (value is not IEnumerable ||
                    ((IEnumerable)value).Count() == 0)
                {
                    newTemplate = Remove(newTemplate, contexts[i].Item1, 1,
                        ref contexts, ref placeholders);
                    newTemplate = Remove(newTemplate, contexts[i].Item2, 1,
                        ref contexts, ref placeholders);
                    return newTemplate;
                }
            }

            newTemplate = Remove(newTemplate, contexts[i].Item1,
                contexts[i].Item2 - contexts[i].Item1 + 1, ref contexts,
                ref placeholders);
            return newTemplate;
        }

        private static string ParseNotNull(string template,
            ref List<Tuple<int, int, string>> contexts,
            ref List<Tuple<int, int>> placeholders, int i, object obj,
            int depth)
        {
            if (GetParentEnumerations(contexts, contexts[i].Item1) > depth) return template;

            string newTemplate = template;

            string propertyName = contexts[i].Item3[1..];
            var value = FetchProperty(obj, propertyName);

            if (value != null)
            {
                if (value is not bool || (bool)value)
                {
                    if (value is not IEnumerable ||
                        ((IEnumerable)value).Count() > 0)
                    {
                        // Leave the context untouched
                        newTemplate = Remove(newTemplate, contexts[i].Item1, 1,
                            ref contexts, ref placeholders);
                        newTemplate = Remove(newTemplate, contexts[i].Item2, 1,
                            ref contexts, ref placeholders);
                        return newTemplate;
                    }
                }
            }

            newTemplate = Remove(newTemplate, contexts[i].Item1,
                contexts[i].Item2 - contexts[i].Item1 + 1, ref contexts,
                ref placeholders);
            return newTemplate;
        }

        private static string ParseEnumeration(string template,
            ref List<Tuple<int, int, string>> contexts,
            ref List<Tuple<int, int>> placeholders, int i, object obj,
            int depth)
        {
            if (GetParentEnumerations(contexts, contexts[i].Item1) > depth) return template;

            string newTemplate = template;
            string playground = newTemplate;

            string propertyName = contexts[i].Item3[1..];
            var value = FetchProperty(obj, propertyName);
            var enumerable = value as IEnumerable;

            if (enumerable == null || enumerable.Count() == 0)
            {
                newTemplate = Remove(newTemplate, contexts[i].Item1,
                    contexts[i].Item2 - contexts[i].Item1 + 1,
                    ref contexts, ref placeholders);
                return newTemplate;
            }

            int startIndex = contexts[i].Item1;
            var removing = new Range(contexts[i].Item1,
                contexts[i].Item1 + contexts[i].Item2 - contexts[i].Item1 + 1);
            string contents = newTemplate[(contexts[i].Item1 + 1)..contexts[i].Item2];

            var builder = new StringBuilder();
            foreach (var item in enumerable)
            {
                var oldContexts = contexts;
                var oldPlaceholders = placeholders;
                playground = newTemplate;
                string newContents = contents;

                var children = GetChildren(contexts, contexts[i]);

                foreach (var child in children)
                {
                    if (contexts[child].Item1 < 0) continue;

                    string type = contexts[child].Item3[0].ToString();
                    switch (type)
                    {
                        case "!":
                            string parsed = ParseNotNull(playground, ref contexts,
                                  ref placeholders, child, item, depth + 1);
                            newContents = parsed
                                [(contexts[i].Item1 + 1)..contexts[i].Item2];
                            playground = parsed;
                            break;
                        case "?":
                            string parsed2 = ParseNull(playground, ref contexts,
                                ref placeholders, child, item, depth + 1);
                            newContents = parsed2
                                [(contexts[i].Item1 + 1)..contexts[i].Item2];
                            playground = parsed2;
                            break;
                        case "*":
                            string parsed3 = ParseEnumeration(playground, ref contexts,
                                ref placeholders, child, item, depth + 1);
                            newContents = parsed3
                                [(contexts[i].Item1 + 1)..contexts[i].Item2];
                            playground = parsed3;
                            break;
                        default:
                            int index = contexts[child].Item1 - contexts[child].Item3.Length;
                            var lineAndColumn = GetLineAndColumn(newTemplate, index);

                            throw new HTMLTemplateSyntaxException(
                                $"Unexpected '{type}' at line {lineAndColumn.Item1}" +
                                $" col {lineAndColumn.Item2}")
                            {
                                Line = lineAndColumn.Item1,
                                Column = lineAndColumn.Item2
                            };
                    }
                }

                var childPlaceholders = GetChildPlaceholders(placeholders, contexts[i]);
                for (int j = 0; j < childPlaceholders.Count; j++)
                {
                    var item2 = placeholders[childPlaceholders[j]];

                    if (item2.Item1 < 0) continue;
                    if (GetParentEnumerations(contexts, item2.Item1) > (depth + 1)) continue;
                    
                    string placeholder = playground[item2.Item1..(item2.Item2 + 1)];
                    char modifier = placeholder[1];
                    string propertyName2 = (modifier == '-' || modifier == '#')
                        ? placeholder[2..^1] : placeholder[1..^1];

                    var value2 = FetchProperty(item, propertyName2);
                    string inserting;
                    if (modifier == '-')
                    {
                        inserting = value2?.ToString() ?? string.Empty;
                        if (inserting.Length > 1)
                            inserting = inserting[0].ToString().ToLower() + inserting[1..];
                        else if (inserting.Length > 0) inserting = inserting.ToLower();
                    }
                    else if (modifier == '#')
                    {
                        string[]? strings = value2 as string[];
                        if (strings == null) inserting = string.Empty;
                        else inserting = string.Join(", ", strings);
                    }
                    else inserting = value2?.ToString() ?? string.Empty;

                    playground = Remove(playground, item2.Item1,
                        item2.Item2 - item2.Item1 + 1, ref contexts, ref placeholders);
                    playground = Insert(playground, inserting, item2.Item1, ref contexts,
                        ref placeholders);
                }
                newContents = playground
                    [(contexts[i].Item1 + 1)..contexts[i].Item2];

                builder.AppendLine(newContents);
                contexts = oldContexts;
                placeholders = oldPlaceholders;
            }

            newTemplate = Remove(newTemplate, removing.Start.Value,
                removing.End.Value - removing.Start.Value, ref contexts,
                ref placeholders);
            newTemplate = Insert(newTemplate, builder.ToString(), startIndex,
                ref contexts, ref placeholders);
            return newTemplate;
        }

        #region Helpers
        private static object? FetchProperty(object obj, string address)
        {
            if (obj == null) return null;

            object result = obj;
            string[] properties = address.Split('.');
            foreach (var propertyName in properties)
            {
                var property = result!.GetType().GetProperty(propertyName);
                if (property == null) return null;
                result = property.GetValue(result)!;
                if (result == null) return null;
            }

            return result;
        }

        private static string Remove(string original, int start, int length,
            ref List<Tuple<int, int, string>> contexts,
            ref List<Tuple<int, int>> placeholders)
        {
            string result = original.Remove(start, length);

            var newContexts = new List<Tuple<int, int, string>>();
            foreach (var context in contexts)
            {
                if (context.Item1 >= start)
                {
                    if (context.Item2 <= (start + length - 1))
                    {
                        newContexts.Add(Tuple.Create(
                            -1, -1, string.Empty));
                        continue;
                    }

                    newContexts.Add(Tuple.Create(
                        context.Item1 - length, context.Item2 - length, context.Item3));
                    continue;
                }

                if (context.Item2 >= start)
                {
                    newContexts.Add(Tuple.Create(
                        context.Item1, context.Item2 - length, context.Item3));
                    continue;
                }

                newContexts.Add(Tuple.Create(context.Item1, context.Item2, context.Item3));
            }
            contexts = newContexts;

            var newPlaceholders = new List<Tuple<int, int>>();
            foreach (var placeholder in placeholders)
            {
                if (placeholder.Item1 >= start)
                {
                    if (placeholder.Item2 <= (start + length - 1))
                    {
                        newPlaceholders.Add(Tuple.Create(-1, -1));
                        continue;
                    }

                    newPlaceholders.Add(Tuple.Create(
                        placeholder.Item1 - length, placeholder.Item2 - length));
                    continue;
                }

                if (placeholder.Item2 >= start)
                {
                    newPlaceholders.Add(Tuple.Create(
                        placeholder.Item1, placeholder.Item2 - length));
                    continue;
                }

                newPlaceholders.Add(
                    Tuple.Create(placeholder.Item1, placeholder.Item2));
            }
            placeholders = newPlaceholders;

            return result;
        }

        private static string Insert(string original, string inserting, int index,
            ref List<Tuple<int, int, string>> contexts,
            ref List<Tuple<int, int>> placeholders)
        {
            string result = original.Insert(index, inserting);

            var newContexts = new List<Tuple<int, int, string>>();
            foreach (var context in contexts)
            {
                if (context.Item1 >= index)
                {
                    newContexts.Add(Tuple.Create(
                        context.Item1 + inserting.Length,
                        context.Item2 + inserting.Length,
                        context.Item3));
                    continue;
                }

                if (context.Item2 >= index)
                {
                    newContexts.Add(Tuple.Create(
                        context.Item1,
                        context.Item2 + inserting.Length,
                        context.Item3));
                    continue;
                }

                newContexts.Add(Tuple.Create(context.Item1, context.Item2, context.Item3));
            }
            contexts = newContexts;

            var newPlaceholders = new List<Tuple<int, int>>();
            foreach (var placeholder in placeholders)
            {
                if (placeholder.Item1 >= index)
                {
                    newPlaceholders.Add(Tuple.Create(
                        placeholder.Item1 + inserting.Length,
                        placeholder.Item2 + inserting.Length));
                    continue;
                }

                if (placeholder.Item2 >= index)
                {
                    newPlaceholders.Add(Tuple.Create(
                        placeholder.Item1,
                        placeholder.Item2 + inserting.Length));
                    continue;
                }

                newPlaceholders.Add(
                    Tuple.Create(placeholder.Item1, placeholder.Item2));
            }
            placeholders = newPlaceholders;

            return result;
        }

        private static string RemoveEscapes(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' &&
                    i != text.Length - 1 &&
                    (text[i + 1] == '(' || text[i + 1] == ')' ||
                    text[i + 1] == '{' || text[i + 1] == '}'))
                {
                    text = text.Remove(i, 1);
                }
            }

            return text;
        }

        private static List<Tuple<int, bool>> GetCurlyBraces(string text)
        {
            var braces = new List<Tuple<int, bool>>();

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{' &&
                    (i == 0 || text[i - 1] != '\\')) braces.Add(Tuple.Create(i, true));
                if (text[i] == '}' &&
                    (i == 0 || text[i - 1] != '\\')) braces.Add(Tuple.Create(i, false));
            }

            return braces;
        }

        private static List<Tuple<int, bool>> GetBraces(string text)
        {
            var braces = new List<Tuple<int, bool>>();

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '(' &&
                    (i == 0 || text[i - 1] != '\\')) braces.Add(Tuple.Create(i, true));
                if (text[i] == ')' &&
                    (i == 0 || text[i - 1] != '\\')) braces.Add(Tuple.Create(i, false));
            }

            return braces;
        }

        private static int GetParentEnumerations(List<Tuple<int, int, string>> contexts,
            int index)
        {
            int count = 0;

            foreach (var item in contexts)
            {
                if (item.Item1 < index && item.Item2 > index
                    && item.Item3[0] == '*') count++;
            }

            return count;
        }

        private static List<int> GetChildren(
            List<Tuple<int, int, string>> contexts, Tuple<int, int, string> context)
        {
            var result = new List<int>();

            for (int i = 0; i < contexts.Count; i++)
            {
                if (contexts[i].Item1 > context.Item1 && contexts[i].Item2 < context.Item2)
                    result.Add(i);
            }

            return result;
        }

        private static List<int> GetChildPlaceholders(
            List<Tuple<int, int>> placeholders, Tuple<int, int, string> context)
        {
            var result = new List<int>();

            for (int i = 0; i < placeholders.Count; i++)
            {
                if (placeholders[i].Item1 > context.Item1 &&
                    placeholders[i].Item2 < context.Item2)
                    result.Add(i);
            }

            return result;
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
