namespace DocGen.Entities
{
    // Entity representing a type

    [Serializable]
    public class TypeInformation
    {
        public string FullName { get; set; } = string.Empty;

        public string Name { get; internal set; } = string.Empty;

        public string Summary { get; internal set; } = string.Empty;

        public GenericParameterInformation[] GenericParameters { get; internal set; }
            = new GenericParameterInformation[] { };

        public string Notes { get; internal set; } = string.Empty;

        public MemberInformation[] Members { get; internal set; }
            = new MemberInformation[] { };
    }
}
