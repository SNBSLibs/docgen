namespace Test
{
    internal class AnotherTestClass
    {
#pragma warning disable CS8618
        public AnotherTestClass(Type type) { }
#pragma warning restore CS8618

        public event EventHandler<EventArgs> MyEvent;

        public int StructureOfTheWorld = 42;

        public static implicit operator string(AnotherTestClass testClass) => testClass.ToString()!;
    }
}
