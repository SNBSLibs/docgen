# docgen

A lightweight HTML documentation ("manpages") generator for .NET-oriented libraries.

## Usage example

This project has not yet been released (although there's a pre-release), so we haven't yet generated a documentation for it! However, we have a usage example.

```c#
using System.Reflection;
using DocGen.Parsing;
using Type = DocGen.Entities.Type;

WriteLine("Starting\n");

// ************************
// ------------------------
// ************************

WriteLine("Loading test assembly");

Assembly test;
try
{
    test = Assembly.Load("YourTestAssembly");
}
catch
{
    ForegroundColor = ConsoleColor.Red;
    WriteLine("FATAL ERROR: test assembly not found");
    WriteLine("Exiting");
    return;
}

WriteLine("Creating XMLDocParser");

var parser = new XMLDocParser(test, file: "docs.xml");

WriteLine("Deconstructing");

(Assembly tests, IEnumerable<Type> types) = parser;

WriteLine("\n-----------------------\n\n");

WriteLine("Results");

foreach (var type in types)
{
    WriteLine("Type {0}", type.Name);
    WriteLine("Summary:\n\n{0}\n\n", type.Summary);
    WriteLine("Notes:\n\n{0}\n\n", type.Notes);

    WriteLine("Generic parameters:\n\n");
    foreach (var genericParameter in type.GenericParameters)
    {
        WriteLine("{0}: {1}", genericParameter.Name, genericParameter.Description);
    }
    WriteLine("\n\n");

    WriteLine("Members:\n\n");
    foreach (var member in type.Members)
    {
        WriteLine("Member {0} ({1})", member.Name, member.Kind);
        WriteLine("Summary:\n\n{0}\n\n", member.Summary);
        WriteLine("Notes:\n\n{0}\n\n", member.Notes);

        WriteLine("Returns: {0}\n\n{1}\n\n", member.ReturnType, member.ReturnDescription);

        if (member.GenericParameters != null)
        {
            WriteLine("Generic parameters:\n\n");
            foreach (var genericParameter in member.GenericParameters)
            {
                WriteLine("{0}: {1}",
                    genericParameter.Name, genericParameter.Description);
            }
            WriteLine("\n\n");
        }

        if (member.Parameters != null)
        {
            WriteLine("Parameters:\n\n");
            foreach (var param in member.Parameters)
            {
                WriteLine("{0}{1} of type {2}: {3}",
                    param.Name, (param.IsReference ? " (reference)" : null), param.Type, param.Description);
            }
            WriteLine("\n\n");
        }

        if (member.Exceptions != null)
        {
            WriteLine("Possible exceptions:\n\n");
            foreach (var exception in member.Exceptions)
            {
                WriteLine("{0}: {1}", exception.Type, exception.ThrownOn);
            }
            WriteLine("\n\n");
        }
    }
    WriteLine("\n\n");
}

// ************************
// ------------------------
// ************************

WriteLine("\nFinish");
```
