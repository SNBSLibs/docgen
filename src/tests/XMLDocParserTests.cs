using System.Reflection;
using DocGen.Entities;
using DocGen.Parsing;
using Type = DocGen.Entities.Type;

namespace DocGen.Tests
{
    [TestFixture]
    public class XMLDocParserTests
    {
        private string docs = """
            <?xml version="1.0"?>
            <doc>
                <assembly>
                    <name>Test</name>
                </assembly>
                <members>
                    <member name="T:Test.TestClass`2">
                        <summary>
                        This is just a test class.
                        </summary>
                        <typeparam name="T1">A generic parameter.</typeparam>
                        <typeparam name="T2">Another generic parameter.</typeparam>
                        <remarks>
                        It is used to test the DocGen library.

                        <typeparamref name="T1"/> is a very nice generic parameter.
                        </remarks>
                        <seealso>
                        https://github.com/SNBSLibs/docgen
                        </seealso>
                    </member>
                    <member name="P:Test.TestClass`2.TestProperty">
                        <summary>
                        This is a property of the <see cref="T:Test.TestClass"/> class.
                        </summary>
                        <value>
                        Always returns 0.
                        </value>
                        <seealso>
                        https://github.com/SNBSLibs/docgen
                        </seealso>
                    </member>
                    <member name="M:Test.TestClass`2.Generic`1(System.Int32,System.Collections.Generic.List{`0},System.Collections.Generic.List{`3})">
                        <summary>
                        Test of the generic parameters handling.
                        </summary>
                    </member>
                    <member name="M:Test.TestClass`2.DoTheJob(System.String,System.Int32)">
                        <param name="str">A string.</param>
                        <param name="integer">A 32-bit integer.</param>
                        <remarks>
                        A dummy method. Don't pass 3 to <paramref name="integer"/>.
                        </remarks>
                    </member>
                    <member name="T:Test.AnotherTestClass">
                        <summary>
                        A nice little class.
                        </summary>
                    </member>
                    <member name="E:Test.AnotherTestClass.MyEvent">
                        <summary>
                        Occurs very frequently.
                        </summary>
                        <example>
                        anotherTestClass.MyEvent += MyHandler;
                        </example>
                    </member>
                    <member name="F:Test.AnotherTestClass.StructureOfTheWorld">
                        Some idiot put this text here.
                        <summary>
                        Contains the <c>structure</c> of the world.
                        </summary>
                        <remarks>
                        Includes:

                        <list type="bullet">
                        <item>
                        <term>Stars</term>
                        <description>Large shining spheres</description>
                        </item>
                        <item>
                        <term>Planets</term>
                        <description>Smaller spheres that go around stars</description>
                        </item>
                        <item>
                        <term>Planets</term>
                        <description>Smaller spheres that go around stars</description>
                        </item>
                        <item>
                        <term>Black holes</term>
                        <description>Very curious objects</description>
                        </item>
                        </list>
                        </remarks>
                    </member>
                    <member name="M:Test.AnotherTestClass.#ctor(System.Type)">
                        <summary>
                        This is a constructor.
                        </summary>
                        <seealso>
                        <list type="bullet">
                        <item>I</item>
                        <item>Mum</item>
                        <item>Dad</item>
                        <item>Grandad</item>
                        <item>Grandma</item>
                        <item>Brother</item>
                        <item>Sister</item>
                        </list>
                        </seealso>
                    </member>
                    <member name="M:Test.AnotherTestClass.op_Implicit(Test.AnotherTestClass)~System.String">
                        <summary>
                        Just calls ToString().
                        </summary>
                        <remarks>
                        Do not use.
                        </remarks>
                    </member>
                </members>
            </doc>
            """;

        // Initialized in the set-up method before each test
        // So we can assume that null never evaluates to null xD
        private IEnumerable<Type> types = null!;

        [SetUp]
        public void SetUp(TestContext context)
        {
            var references = Assembly
                .GetExecutingAssembly()
                .GetReferencedAssemblies();
            Assembly? test = null;

            foreach (var reference in references)
                if (reference.Name == "testassembly")
                    test = Assembly.Load(reference);

            if (test == null)
                throw new InvalidOperationException("Cannot find the test assembly");

            types = XMLDocParser.Parse(test, docs: docs);
        }
    }
}
