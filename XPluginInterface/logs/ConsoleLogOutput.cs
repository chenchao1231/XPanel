using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XPlugin.logs
{
    public class ConsoleLogOutput : ILogOutput
    {
        public void AppendLog(string message)
        {
            Console.WriteLine(message);
        }
    }
}
