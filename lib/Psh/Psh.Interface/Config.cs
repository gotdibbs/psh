using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Psh.Interface
{
    public class Config
    {
        public string ConnectionString { get; set; }

        public bool DryRun { get; set; }

        public string[] Files { get; set; }

        public string Path { get; set; }

        public string RootNamespace { get; set; }

        public string SolutionName { get; set; }
    }
}
