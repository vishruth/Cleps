using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.Compiler
{
    class LLVMRegister
    {
        public string VariableName;
        public string VariableType;
        public LLVMValueRef LLVMValueRef;

        public LLVMRegister(string variableName, string variableType, LLVMValueRef llvmValueRef)
        {
            VariableName = variableName;
            VariableType = variableType;
            LLVMValueRef = llvmValueRef;
        }
    }
}
