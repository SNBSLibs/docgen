namespace DocGen.Entities
{
    // Entity representing a type

    [Serializable]
    public class Type
    {
        internal string FullName { get; set; } = string.Empty;

        public string Name { get; internal set; } = string.Empty;

        public string Summary { get; internal set; } = string.Empty;

        public IEnumerable<GenericParameter> GenericParameters { get; internal set; }
            = Enumerable.Empty<GenericParameter>();

        public string Notes { get; internal set; } = string.Empty;

        public IEnumerable<Member> Members { get; internal set; }
            = Enumerable.Empty<Member>();
    }
}
