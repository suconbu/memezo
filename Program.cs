using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Suconbu.Scripting
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                int totalCount = 0;
                int okCount = 0;
                foreach (string file in Directory.GetFiles(args[0], "test*.txt"))
                {
                    var firstLine = File.ReadLines(file).FirstOrDefault();
                    string expectedError = null;
                    var match = Regex.Match(File.ReadLines(file).FirstOrDefault(), @"^// EXPECT:(\w+)");
                    if(match.Success)
                    {
                        expectedError = match.Groups[1].Value;
                    }
                    Console.Write($"{Path.GetFileName(file),-20} - ");

                    var code = File.ReadAllText(file);
                    var script = new OpeScript(code);
                    var output = new StringBuilder();
                    script.AddAction("print", (s, a) => output.Append(a.Count > 0 ? a.First().Convert(ValueType.String).String : null));
                    script.AddAction("printline", (s, a) => output.AppendLine(a.Count > 0 ? a.First().Convert(ValueType.String).String : null));

                    var sw = Stopwatch.StartNew();
                    var result = script.Run();
                    var elapsed = sw.ElapsedMilliseconds;

                    if (result && expectedError != null)
                        Console.Write($"NOK: Expected {expectedError}, but not occurred.");
                    else if (!result && expectedError == null)
                        Console.Write($"NOK: Unexpected {script.Error}.");
                    else if (!result && !script.Error.Message.StartsWith(expectedError))
                        Console.Write($"NOK: Expected {expectedError}, but {script.Error} occurred.");
                    else
                    {
                        Console.Write("OK");
                        ++okCount;
                    }
                    Console.WriteLine($" ({elapsed:#,0}ms)");
                    ++totalCount;
                }
                Console.WriteLine("----------------------------------------");
                Console.WriteLine($"Total:{totalCount} OK:{okCount} ({100.0 * okCount / totalCount:0.0}%)");
                Console.WriteLine();
                Console.ReadKey(false);
            }
        }
    }
}
