namespace DocGen.Entities
{
    // Entity representing parameter of a method

    [Serializable]
    public class Parameter
    {
        public string Name { get; internal set; } = string.Empty;

        public System.Type Type { get; internal set; } = typeof(object);

        public string Description { get; internal set; } = string.Empty;
    }
}
