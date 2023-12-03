namespace Test
{
    public class TestClass<T1, T2>
    {
        public int TestProperty { get => 0; }

        public void Generic<T3>(int i, List<T3> list1, List<T1> list2) { }

        public void DoTheJob(string s, int i) { }
    }
}
