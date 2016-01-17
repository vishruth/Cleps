﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.Compiler
{
    class CompilerErrorException : Exception
    {
        public List<CompilerError> Errors = new List<CompilerError>();

        public CompilerErrorException(List<CompilerError> errors)
        {
            Errors = errors;
        }
    }
}
