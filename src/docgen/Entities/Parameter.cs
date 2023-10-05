namespace DocGen.Entities
{
    // Entity representing parameter of a method

    [Serializable]
    public class Parameter
    {
        public string Name { get; set; } = string.Empty;

        public System.Type Type { get; set; } = typeof(object);

        public string Description { get; set; } = string.Empty;
    }
}
