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
            // If docs, file and stream are all null, there will be an exception at line 10
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
            throw new NotImplementedException();
        }
    }
}
