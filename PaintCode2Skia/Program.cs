using System;
using PaintCode2Skia.Resources;
using System.Collections.Generic;
using PaintCode2Skia.Core;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace PaintCode2Skia
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication(false);
            app.FullName = "PaintCode2Skia";
            app.Name = "pc2skia";

            var javaArg = app.Argument("java", "The path to the PaintCode Android Java export.");
            var csArg = app.Argument("cs", "The path to the output C# file.");
            var namespaceArg = app.Argument("-n|--namespace", "Set the namespace for the C# file.");

            app.OnExecute(() =>
            {
                if (javaArg.Value != null && csArg.Value != null)
                {
                    Console.WriteLine($"Processing: '{javaArg.Value}' => '{csArg.Value}'...");

                    var javaLines = File.ReadAllLines(javaArg.Value);
                    var parser = new Parser();
                    File.WriteAllLines(csArg.Value, parser.ParsePaintCodeJavaCode(javaLines, namespaceArg.Value));

                    Console.WriteLine("Done.");
                }
                else
                {
                    app.ShowHelp();
                }

                return 0;
            });


            app.Execute(args);
        }
    }
}
