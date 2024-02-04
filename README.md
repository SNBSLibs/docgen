**Please note that this project is curently under development. Although the core features work, they are not yet fully stable.**

# docgen

A lightweight HTML documentation ("manpages") generator for .NET-oriented libraries.

## Usage example

This project has not yet been released (although there's a pre-release), so we haven't yet generated a documentation for it! However, we have a usage example.

```c#
using System.Reflection;
using DocGen.Parsing;
using static System.Console;

WriteLine("Starting\n");
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

WriteLine("Parsing");

var types = XMLDocParser.Parse(test, file: "YourXmlDocsPath");

WriteLine("\n-----------------------\n");

WriteLine("Results");

foreach (var type in types)
{
    WriteLine("Type {0}", type.Name);
    WriteLine("Summary:\n{0}\n", type.Summary);
    WriteLine("Notes:\n{0}\n", type.Notes);

    WriteLine("Generic parameters:\n");
    foreach (var genericParameter in type.GenericParameters)
    {
        WriteLine("{0}: {1}", genericParameter.Name, genericParameter.Description);
    }
    WriteLine("\n");

    WriteLine("Members:\n");
    foreach (var member in type.Members)
    {
        WriteLine("Member {0} ({1})", member.Name, member.Kind);
        WriteLine("Summary:\n{0}\n", member.Summary);
        WriteLine("Notes:\n{0}\n", member.Notes);

        WriteLine("Returns: {0}\n{1}\n", member.ReturnType, member.ReturnDescription);

        if (member.GenericParameters != null)
        {
            WriteLine("Generic parameters:\n");
            foreach (var genericParameter in member.GenericParameters)
            {
                WriteLine("{0}: {1}",  genericParameter.Name, genericParameter.Description);
            }
            WriteLine("\n");
        }

        if (member.Parameters != null)
        {
            WriteLine("Parameters:\n");
            foreach (var param in member.Parameters)
            {
                WriteLine("{0}{1} of type {2}: {3}",
                    param.Name, (param.IsReference ? " (reference)" : null), param.Type, param.Description);
            }
            WriteLine("\n");
        }

        if (member.Exceptions != null)
        {
            WriteLine("Possible exceptions:\n");
            foreach (var exception in member.Exceptions)
            {
                WriteLine("{0}: {1}", exception.Type, exception.ThrownOn);
            }
            WriteLine("\n");
        }
    }
    WriteLine("\n");
}

WriteLine("\nFinish");
```
