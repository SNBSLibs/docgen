namespace DocGen.Entities
{
    // Entity representing a generic parameter

    [Serializable]
    public class GenericParameter
    {
        public string Name { get; internal set; } = string.Empty;

        public string Description { get; internal set; } = string.Empty;
    }
}
