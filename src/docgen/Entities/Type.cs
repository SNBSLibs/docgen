namespace DocGen.Entities
{
    // Entity representing a type

    [Serializable]
    public class Type
    {
        public string FullName { get; set; } = string.Empty;

        public string Name { get; internal set; } = string.Empty;

        public string Summary { get; internal set; } = string.Empty;

        public GenericParameter[] GenericParameters { get; internal set; }
            = new GenericParameter[] { };

        public string Notes { get; internal set; } = string.Empty;

        public Member[] Members { get; internal set; }
            = new Member[] { };
    }
}
