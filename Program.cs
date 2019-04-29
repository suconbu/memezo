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
            while (true)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (line == "exit") return;
                else if (line == "@test")
                {
                    RunTest(@"..\..");
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
                    var output = new StringBuilder();
                    interpreter.AddAction("print", (a) => Console.Write(a.Count > 0 ? a.First().ToString() : null));
                    interpreter.AddAction("printline", (a) => Console.WriteLine(a.Count > 0 ? a.First().ToString() : null));
                    if (interpreter.Run(line))
                    {
                        if (interpreter.LastResultValue.HasValue) Console.WriteLine(interpreter.LastResultValue);
                    }
                    else
                    {
                        Console.WriteLine(interpreter.Error);
                    }
                }
            }
        }

        static void RunTest(string directoryPath)
        {
            var swAll = Stopwatch.StartNew();
            int totalCount = 0;
            int okCount = 0;
            //foreach (string file in Directory.GetFiles(args[0], "test_ok_goto1.txt"))
            foreach (string file in Directory.GetFiles(directoryPath, "test*.txt"))
            {
                var firstLine = File.ReadLines(file).FirstOrDefault();
                bool expectResult = true;
                string expectOutput = null;
                var match = Regex.Match(firstLine, @"^// (\w+):(.+)");
                if (match.Success)
                {
                    expectResult = match.Groups[1].Value == "OK";
                    expectOutput = match.Groups[2].Value;
                }
                Console.Write($"{Path.GetFileName(file),-30} - ");

                var interpreter = new Memezo.Interpreter();
                var output = new StringBuilder();
                interpreter.AddAction("print", (a) => output.Append(a.Count > 0 ? a.First().ToString() : null));
                interpreter.AddAction("printline", (a) => output.AppendLine(a.Count > 0 ? a.First().ToString() : null));
                interpreter.AddAction("debug", (a) => Debug.Write(a.Count > 0 ? a.First().ToString() : null));
                interpreter.AddAction("debugline", (a) => Debug.WriteLine(a.Count > 0 ? a.First().ToString() : null));

                var code = File.ReadAllText(file);
                var sw = Stopwatch.StartNew();
                var result = interpreter.Run(code);
                var elapsed = sw.ElapsedMilliseconds;

                if (expectResult && !result)
                    // 期待通り成功せず
                    Console.Write($"NOK: Unexpected {interpreter.Error}.");
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
