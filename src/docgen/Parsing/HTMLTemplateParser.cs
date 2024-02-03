using System.Diagnostics;
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
            var contexts = new List<Tuple<int, int, string>>();

            var unclosedBraces = new List<Tuple<int, string>>();
            foreach (var brace in braces)
            {
                if (brace.Item2 == true)
                {
                    int lastExclamation = template.LastIndexOf('!', brace.Item1);
                    int lastQuestion = template.LastIndexOf('?', brace.Item1);
                    int lastAsterisk = template.LastIndexOf('*', brace.Item1);
                    int lastSpecialChar =
                        Math.Max(lastExclamation, Math.Max(lastQuestion, lastAsterisk));

                    string contextString =
                        template.Substring(lastSpecialChar, brace.Item1 - lastSpecialChar);

                    unclosedBraces.Add(Tuple.Create(brace.Item1, contextString));
                }
                else
                {
                    if (unclosedBraces.Count == 0)
                    {
                        var lineAndColumn = GetLineAndColumn(template, brace.Item1);

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
                var lineAndColumn = GetLineAndColumn(template,
                    unclosedBraces[unclosedBraces.Count - 1].Item1);

                throw new HTMLTemplateSyntaxException(
                    $"Unclosed '{{' at line {lineAndColumn.Item1}" +
                    $" col {lineAndColumn.Item2}")
                {
                    Line = lineAndColumn.Item1,
                    Column = lineAndColumn.Item2
                };
            }
        }

        #region Helpers
        private static Tuple<int, bool>[] GetCurlyBraces(string text)
        {
            var braces = new List<Tuple<int, bool>>();

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{') braces.Add(Tuple.Create(i, true));
                if (text[i] == '}') braces.Add(Tuple.Create(i, false));
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
}
