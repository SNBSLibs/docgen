namespace DocGen.Entities
{
    // Entity representing an exception (like in <exception> tags in XML docs)

    [Serializable]
    public class Exception
    {
        public System.Type Type { get; set; } = typeof(Exception);

        public string ThrownOn { get; set; } = string.Empty;
    }
}
