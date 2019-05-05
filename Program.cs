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
            var interpreter = new Memezo.Interpreter();
            interpreter.PrintValue += (s, e) => Console.WriteLine(e);

            var output = new StringBuilder();
            while (true)
            {
                if(interpreter.DeferedClauseCount > 0)
                    Console.Write(". ");
                else
                    Console.Write("> ");
                var line = Console.ReadLine();
                if (line.StartsWith("@test"))
                {
                    var pattern = "test*.txt";
                    var match = Regex.Match(line, "@test (.+)");
                    if(match.Success)
                    {
                        pattern = match.Groups[1].Value;
                    }
                    RunTest(@"..\..", pattern);
                }
                else if (line == "@vars")
                {
                    foreach (var var in interpreter.Vars)
                    {
                        Console.Write($"{var.Key}:{var.Value} ");
                    }
                    Console.WriteLine();
                }
                else
                {
                    if (!interpreter.RunAsInteractive(line))
                    {
                        Console.WriteLine(interpreter.Error);
                    }
                }
            }
        }

        static void RunTest(string directoryPath, string pattern)
        {
            var swAll = Stopwatch.StartNew();
            int totalCount = 0;
            int okCount = 0;
            foreach (string file in Directory.GetFiles(directoryPath, pattern))
            {
                var firstLine = File.ReadLines(file).FirstOrDefault();
                bool expectResult = true;
                string expectOutput = null;
                var match = Regex.Match(firstLine, @"^# (\w+):(.+)");
                if (match.Success)
                {
                    expectResult = match.Groups[1].Value == "OK";
                    expectOutput = match.Groups[2].Value;
                }
                Console.Write($"{Path.GetFileName(file),-30} - ");

                var interpreter = new Memezo.Interpreter();
                var output = new StringBuilder();
                interpreter.AddFunction("print", (a) => { output.Append(a.Count > 0 ? a.First().ToString() : null); return Memezo.Value.Zero; });
                interpreter.AddFunction("printline", (a) => { output.AppendLine(a.Count > 0 ? a.First().ToString() : null); return Memezo.Value.Zero; });
                interpreter.AddFunction("debug", (a) => { Debug.Write(a.Count > 0 ? a.First().ToString() : null); return Memezo.Value.Zero; });
                interpreter.AddFunction("debugline", (a) => { Debug.WriteLine(a.Count > 0 ? a.First().ToString() : null); return Memezo.Value.Zero; });

                var code = File.ReadAllText(file);
                var sw = Stopwatch.StartNew();
                var result = interpreter.Run(code);
                var elapsed = sw.ElapsedMilliseconds;

                if (expectResult && !result)
                    // 期待通り成功せず
                    Console.Write($"NOK: {interpreter.Error}.");
                else if (expectResult && result && expectOutput != null && output.ToString() != expectOutput)
                    // 期待通り成功したけど出力が違う
                    Console.Write($"NOK: Expected '{expectOutput}', but output was '{output}'.");
                else if (!expectResult && result)
                    // 期待通り失敗せず
                    Console.Write($"NOK: Expected '{expectOutput}', but not occurred.");
                else if (!expectResult && !result && expectOutput != null && !interpreter.Error.Message.StartsWith(expectOutput))
                    // 期待通り失敗したけどエラーが違う
                    Console.Write($"NOK: Expected '{expectOutput}', but {interpreter.Error} occurred.");
                else
                {
                    Console.Write($"OK: {interpreter.Error}");
                    ++okCount;
                }
                Console.WriteLine($" --- {elapsed:#,0}ms statements:{interpreter.TotalStatementCount} tokens:{interpreter.TotalTokenCount} outputlength:{output.Length}");
                ++totalCount;
            }
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"Total:{totalCount} OK:{okCount} ({100.0 * okCount / totalCount:0.0}%) {swAll.ElapsedMilliseconds:#,0}ms");
            Console.WriteLine();
        }
    }
}
