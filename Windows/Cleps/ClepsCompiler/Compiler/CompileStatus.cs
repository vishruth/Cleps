using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.Compiler
{
    class CompileStatus
    {
        public bool Success { get; private set; }

        public List<CompilerError> Errors = new List<CompilerError>();

        public CompileStatus()
        {
            Success = true;
        }

        public void AddError(CompilerError e)
        {
            Errors.Add(e);
            Success = false;
        }

        public void ThrowOnError()
        {
            if (Errors.Count > 0)
            {
                CompilerErrorException e = new CompilerErrorException(Errors);
                throw e;
            }
        }

        public void ThrowError(CompilerError e)
        {
            AddError(e);
            ThrowOnError();
        }
    }
}
