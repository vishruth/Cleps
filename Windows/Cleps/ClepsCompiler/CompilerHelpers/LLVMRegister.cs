﻿using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.CompilerHelpers
{
    /// <summary>
    /// A class the holds the various values that have initialized during the compilation of a function
    /// </summary>
    class LLVMRegister
    {
        /// <summary>
        /// The cleps type of the variable initialized
        /// </summary>
        public ClepsType VariableType { get; private set; }

        /// <summary>
        /// The llvm register where this value is stored
        /// </summary>
        public LLVMValueRef LLVMValueRef { get; private set; }

        public LLVMRegister(ClepsType variableType, LLVMValueRef llvmValueRef)
        {
            VariableType = variableType;
            LLVMValueRef = llvmValueRef;
        }
    }
}
