namespace DocGen.Entities
{
    // Entity representing a type

    [Serializable]
    public class Type
    {
        public string Name { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public IEnumerable<GenericParameter> GenericParameters { get; set; }
            = Enumerable.Empty<GenericParameter>();

        public string Notes { get; set; } = string.Empty;

        public IEnumerable<Member> Members { get; set; }
            = Enumerable.Empty<Member>();
    }
}
