using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Reflection;

namespace Suconbu.Scripting
{
    class Program
    {
        static void Main(string[] args)
        {
            var interp = new Memezo.Interpreter();
            interp.Install(new Memezo.StandardLibrary(), new Memezo.RandomLibrary());
            interp.Output += (s, e) => Console.WriteLine(e);
            //interpreter.StatementReached += (s, e) => Console.WriteLine($"Statement:{e}");
            interp.ErrorOccurred += (s,e) => Console.WriteLine($"ERROR: {e}");

            var output = new StringBuilder();
            var deferred = false;
            while (true)
            {
                Console.Write(deferred ? ". " : "> ");

                var line = Console.ReadLine();
                if (line == "@version")
                {
                    Console.WriteLine("v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
                }
                else if (line.StartsWith("@run"))
                {
                    var match = Regex.Match(line, "@run (.+)");
                    if(match.Success)
                    {
                        if (File.Exists(match.Groups[1].Value))
                        {
                            interp.Source = File.ReadAllText(match.Groups[1].Value);
                            interp.Run();
                        }
                    }
                }
                else if (line.StartsWith("@test"))
                {
                    var pattern = "*.txt";
                    var match = Regex.Match(line, "@test (.+)");
                    if (match.Success)
                    {
                        pattern = match.Groups[1].Value;
                    }
                    RunTest(@"..\..\test", pattern, line.EndsWith("step"));
                }
                else if (line == "@vars")
                {
                    foreach (var var in interp.Vars)
                    {
                        Console.Write($"{var.Key}:{var.Value} ");
                    }
                    Console.WriteLine();
                }
                else
                {
                    interp.InteractiveRun(line, out deferred);
                }
            }
        }

        static void RunTest(string directoryPath, string pattern, bool stepByStep = false)
        {
            var swAll = Stopwatch.StartNew();
            int totalCount = 0;
            int okCount = 0;

            {
                var interp = new Memezo.Interpreter();

                ++totalCount;
                if (!interp.Run()) ++okCount;
                else Console.Write($"NOK: Expected 'NothingSource', but not occurred.");

                int nextIndex = 0;

                ++totalCount;
                if (!interp.Step(0, out nextIndex)) ++okCount;
                else Console.Write($"NOK: Expected 'NothingSource', but not occurred.");

                interp.Source = "n = 1";

                ++totalCount;
                if (interp.Step(100, out nextIndex)) ++okCount;
                else Console.Write($"NOK: ");

                ++totalCount;
                if (interp.Step(-1, out nextIndex)) ++okCount;
                else Console.Write($"NOK: ");
            }

            foreach (string file in Directory.GetFiles(directoryPath, pattern))
            {
                var firstLine = File.ReadLines(file).FirstOrDefault();
                if (firstLine == null) continue;
                bool expectResult = true;
                string expectOutput = null;
                var match = Regex.Match(firstLine, @"^# (\w+):(.+)");
                if (match.Success)
                {
                    expectResult = match.Groups[1].Value == "OK";
                    expectOutput = match.Groups[2].Value;
                }
                Console.Write($"{Path.GetFileName(file),-20} - ");

                var interp = new Memezo.Interpreter();
                interp.Install(new Memezo.StandardLibrary(), new Memezo.RandomLibrary());
                var output = new StringBuilder();
                interp.Functions["print"] = (a) => { output.Append(a.Count > 0 ? a.First().ToString() : null); return Memezo.Value.Zero; };
                interp.Functions["printline"] = (a) => { output.AppendLine(a.Count > 0 ? a.First().ToString() : null); return Memezo.Value.Zero; };

                interp.Source = File.ReadAllText(file);
                var sw = Stopwatch.StartNew();
                bool result = false;
                if (stepByStep)
                {
                    int index = 0;
                    while (true)
                    {
                        result = interp.Step(index, out var nextIndex);
                        if (!result || nextIndex < 0) break;
                        index = nextIndex;
                    }
                }
                else
                {
                    result = interp.Run();
                }
                var elapsed = sw.ElapsedMilliseconds;

                Console.Write($"{elapsed,5:#,0}ms - ");

                if (expectResult && !result)
                    // 期待通り成功せず
                    Console.Write($"NOK: {interp.LastError}.");
                else if (expectResult && result && expectOutput != null && output.ToString() != expectOutput)
                    // 期待通り成功したけど出力が違う
                    Console.Write($"NOK: Expected '{expectOutput}', but output was '{output}'.");
                else if (!expectResult && result)
                    // 期待通り失敗せず
                    Console.Write($"NOK: Expected '{expectOutput}', but not occurred.");
                else if (!expectResult && !result && expectOutput != null && !interp.LastError.Message.StartsWith(expectOutput))
                    // 期待通り失敗したけどエラーが違う
                    Console.Write($"NOK: Expected '{expectOutput}', but {interp.LastError} occurred.");
                else
                {
                    Console.Write($"OK: {interp.LastError}");
                    ++okCount;
                }
                Console.WriteLine($"   (statements:{interp.Stat.StatementCount} tokens:{interp.Stat.TotalTokenCount} outputlength:{output.Length})");
                ++totalCount;
            }
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"Total:{totalCount} OK:{okCount} ({100.0 * okCount / totalCount:0.0}%) {swAll.ElapsedMilliseconds:#,0}ms");
            Console.WriteLine();
        }
    }
}
