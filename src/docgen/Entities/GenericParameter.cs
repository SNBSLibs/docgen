namespace DocGen.Entities
{
    // Entity representing a generic parameter

    [Serializable]
    public class GenericParameter
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
