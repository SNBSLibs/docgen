namespace DocGen.Entities
{
    // Entity representing a member of a type

    [Serializable]
    public class Member
    {
        public string Name { get; internal set; } = string.Empty;

        public string Summary { get; internal set; } = string.Empty;

        public IEnumerable<Parameter>? Parameters { get; internal set; }

        public IEnumerable<Exception>? Exceptions { get; internal set; }

        public string? ReturnValue { get; internal set; } = string.Empty;

        public System.Type? ReturnType { get; internal set; }

        public string Notes { get; internal set; } = string.Empty;

        public Type? Type { get; internal set; }  // Null means the type is unknown

        public MemberKind Kind { get; internal set; } = MemberKind.Unknown;
    }
}
