using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.Compiler
{
    class CompilerDriver
    {
        public List<string> Files { get; private set; }

        public CompilerDriver(List<string> files)
        {
            Files = files;
        }

        public CompileStatus CompileFiles()
        {
            return CompileStatus.GetFailureStatus("", "", "Not implemented");
        }
    }
}
