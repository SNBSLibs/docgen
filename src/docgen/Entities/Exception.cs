namespace DocGen.Entities
{
    // Entity representing an exception (like in <exception> tags in XML docs)

    [Serializable]
    public class Exception
    {
        public System.Type Type { get; internal set; } = typeof(Exception);

        public string ThrownOn { get; internal set; } = string.Empty;
    }
}
