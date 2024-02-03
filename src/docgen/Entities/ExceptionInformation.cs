namespace DocGen.Entities
{
    // Entity representing an exception (like in <exception> tags in XML docs)

    [Serializable]
    public class ExceptionInformation
    {
        public string Type { get; internal set; } = "System.Exception";

        public string ThrownOn { get; internal set; } = string.Empty;
    }
}
