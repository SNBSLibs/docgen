namespace DocGen.Entities
{
    // Enumeration containing values that represent different types of members
    // Operations and indexers are considered methods
    
    public enum MemberKind
    {
        Field,
        Event,
        Method,
        Property,
        Constructor,
        Unknown  // This member kind has no return value, parameters or exceptions
    }
}
