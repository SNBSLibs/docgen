namespace Test
{
    internal class AnotherTestClass
    {
        public AnotherTestClass(Type type) { }

        public event EventHandler<EventArgs> MyEvent;

        public int StructureOfTheWorld = 42;

        public static implicit operator string(AnotherTestClass testClass) => testClass.ToString();
    }
}
