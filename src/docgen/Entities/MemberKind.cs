namespace DocGen.Entities
{
    // Enumeration containing values that represent different types of members
    // Operations and indexers are considered methods
    
    public enum MemberKind
    {
        Field,
        Constant,
        Event,
        Method,
        Property,
        Constructor
    }
}
