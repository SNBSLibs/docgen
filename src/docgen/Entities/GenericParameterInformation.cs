using System.Reflection;

namespace DocGen.Entities
{
    // Entity representing a generic parameter

    [Serializable]
    public class GenericParameterInformation
    {
        public string Name { get; internal set; } = string.Empty;

        public string Description { get; internal set; } = string.Empty;
    }
}
