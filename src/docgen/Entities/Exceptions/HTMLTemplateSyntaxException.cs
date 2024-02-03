namespace DocGen.Entities.Exceptions
{
    public class HTMLTemplateSyntaxException : Exception
    {
        public HTMLTemplateSyntaxException(string message) : base(message) { }
        public HTMLTemplateSyntaxException(string message, Exception innerException) : base(message, innerException) { }

        public int Line { get; internal set; }

        public int Column { get; internal set; }
    }
}
