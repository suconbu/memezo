using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Suconbu.Scripting
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                try
                {
                    var code = File.ReadAllText(args.First());
                    var script = new OpeScript(code);
                    script.Run();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                Console.ReadKey(false);
            }
        }
    }
}
