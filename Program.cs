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
                    bool expectResult = !file.Contains("nok");
                    string expectOutput = null;
                    var match = Regex.Match(File.ReadLines(file).FirstOrDefault(), @"^// EXPECT:(.+)");
                    if(match.Success)
                    {
                        expectOutput = match.Groups[1].Value;
                    }
                    Console.Write($"{Path.GetFileName(file),-30} - ");

                    var code = File.ReadAllText(file);
                    var interpreter = new Memezo.Interpreter(code);
                    var output = new StringBuilder();
                    interpreter.AddAction("print", (a) =>
                        output.Append(a.Count > 0 ? a.First().Convert(Memezo.ValueType.String).String : null));
                    interpreter.AddAction("printline", (a) =>
                        output.AppendLine(a.Count > 0 ? a.First().Convert(Memezo.ValueType.String).String : null));

                    var sw = Stopwatch.StartNew();
                    var result = interpreter.Run();
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
