namespace DocGen.Entities
{
    // Entity representing a member of a type

    [Serializable]
    public class Member
    {
        public string Name { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public IEnumerable<Parameter>? Parameters { get; set; }

        public IEnumerable<Exception>? Exceptions { get; set; }

        public string? ReturnValue { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public Type? Type { get; set; }  // Null means the type is unknown

        public MemberKind Kind { get; set; } = MemberKind.Unknown;
    }
}
