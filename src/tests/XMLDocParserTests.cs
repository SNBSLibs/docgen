using System.Reflection;
using System.Reflection.Metadata;
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
                    <member name="M:Test.TestClass`2.Generic`1(System.Int32,System.Collections.Generic.List{``0},System.Collections.Generic.List{`0})">
                        <summary>
                        Test of the generic parameters handling.
                        </summary>
                        <typeparam name="T3">This is just a generic parameter.</typeparam>
                    </member>
                    <member name="M:Test.TestClass`2.DoTheJob(System.String,System.Int32)">
                        <param name="s">A string.</param>
                        <param name="i">A 32-bit integer.</param>
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
                        <exception cref="T:System.Exception">
                        Never throws this exception.
                        </exception>
                        <seealso>
                        <list type="bullet">
                        <item>A</item>
                        <item>B</item>
                        <item>C</item>
                        <item>D</item>
                        <item>E</item>
                        <item>F</item>
                        <item>G</item>
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
        // Added to ensure that testassembly is referenced
        private Test.AnotherTestClass test = null!;

        [SetUp]
        public void SetUp()
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

        [Test]
        public void CanFetchTypeSummary()
        {
            var type = types.Single(t => t.Name == "AnotherTestClass");
            string summary = "A nice little class.";

            Assert.That(type.Summary, Is.EqualTo(summary));
        }

        [Test]
        public void CanFetchTypeNotes()
        {
            var type = types.Single(t => t.Name == "TestClass`2");
            string notes =
                "It is used to test the DocGen library.\n\n*T1* is a very nice generic parameter.\n\nhttps://github.com/SNBSLibs/docgen";

            Assert.That(type.Notes, Is.EqualTo(notes));
        }

        [Test]
        public void CanFetchTypeGenericParameters()
        {
            var type = types.Single(t => t.Name == "TestClass`2");

            Assert.That(type.GenericParameters[0].Name, Is.EqualTo("T1"));
            Assert.That(type.GenericParameters[0].Description, Is.EqualTo("A generic parameter."));
            Assert.That(type.GenericParameters[1].Name, Is.EqualTo("T2"));
            Assert.That(type.GenericParameters[1].Description, Is.EqualTo("Another generic parameter."));
        }

        [Test]
        public void CanFetchMemberSummary()
        {
            var type = types.Single(t => t.Name == "TestClass`2");
            var member = type.Members.Single(m => m.Name == "TestProperty");
            string summary = "This is a property of the $TestClass$&T:Test.TestClass& class.";

            Assert.That(member.Summary, Is.EqualTo(summary));
        }

        [Test]
        public void CanFetchMemberNotes()
        {
            var type = types.Single(t => t.Name == "TestClass`2");
            var member = type.Members.Single(m => m.Name == "TestProperty");
            string notes = "https://github.com/SNBSLibs/docgen";

            Assert.That(member.Notes, Is.EqualTo(notes));
        }

        [Test]
        public void CanFetchMemberParameters()
        {
            var type = types.Single(t => t.Name == "TestClass`2");
            var member = type.Members.Single(m => m.Name == "DoTheJob");

            Assert.That(member.Parameters?[0].Name, Is.EqualTo("s"));
            Assert.That(member.Parameters?[0].Description, Is.EqualTo("A string."));
            Assert.That(member.Parameters?[0].Type.FullName, Is.EqualTo("System.String"));
            Assert.That(member.Parameters?[0].IsReference == false);
            Assert.That(member.Parameters?[1].Name, Is.EqualTo("i"));
            Assert.That(member.Parameters?[1].Description, Is.EqualTo("A 32\\-bit integer."));
            Assert.That(member.Parameters?[1].Type.FullName, Is.EqualTo("System.Int32"));
            Assert.That(member.Parameters?[1].IsReference == false);
        }

        [Test]
        public void CanFetchMemberExceptions()
        {
            var type = types.Single(t => t.Name == "AnotherTestClass");
            var member = type.Members.Single(m => m.Name == "#ctor");

            Assert.That(member.Exceptions?[0].Type, Is.EqualTo("System.Exception"));
            Assert.That(member.Exceptions?[0].ThrownOn, Is.EqualTo("Never throws this exception."));
        }

        [Test]
        public void CanFetchMemberGenericParameters()
        {
            var type = types.Single(t => t.Name == "TestClass`2");
            var member = type.Members.Single(m => m.Name == "Generic");

            Assert.That(member.GenericParameters?[0].Name, Is.EqualTo("T3"));
            Assert.That(member.GenericParameters?[0].Description, Is.EqualTo("This is just a generic parameter."));
        }

        [Test]
        public void CanFetchMemberReturnDescription()
        {
            var type = types.Single(t => t.Name == "TestClass`2");
            var member = type.Members.Single(m => m.Name == "TestProperty");
            string returnDescription = "Always returns 0.";

            Assert.That(member.ReturnDescription, Is.EqualTo(returnDescription));
        }

        [Test]
        public void CanFetchMemberReturnType()
        {
            var type = types.Single(t => t.Name == "TestClass`2");
            var member = type.Members.Single(m => m.Name == "TestProperty");
            string returnType = "System.Int32";

            Assert.That(member.ReturnType?.FullName, Is.EqualTo(returnType));
        }

        [Test]
        public void CanFetchMemberAccessors()
        {
            var type = types.Single(t => t.Name == "TestClass`2");
            var member = type.Members.Single(m => m.Name == "TestProperty");
            string[] accessors = { "get" };

            Assert.That(member.Accessors, Is.EqualTo(accessors));
        }

        [Test]
        public void CanFetchMemberType()
        {
            var type = types.Single(t => t.Name == "AnotherTestClass");
            var member = type.Members.Single(m => m.Name == "StructureOfTheWorld");
            string typeName = "Test.AnotherTestClass";

            Assert.That(member.Type?.FullName, Is.EqualTo(typeName));
        }

        [Test]
        public void CanFetchMemberKind()
        {
            var type = types.Single(t => t.Name == "AnotherTestClass");
            var member = type.Members.Single(m => m.Name == "MyEvent");

            Assert.That(member.Kind, Is.EqualTo(MemberKind.Event));
        }
    }
}
