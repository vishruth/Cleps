using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.CompilerHelpers
{
    /// <summary>
    /// A class the helps with the conversion of a Cleps type to a low level LLVM type
    /// </summary>
    class ClepsLLVMTypeConvertor
    {
        public static LLVMTypeRef? GetPrimitiveLLVMTypeOrNull(ClepsType clepsType)
        {
            if (clepsType.IsVoidType)
            {
                return LLVM.VoidType();
            }

            string identifierText = clepsType.RawTypeName;

            if (identifierText == "System.Types.Bool")
            {
                return LLVM.Int1Type();
            }
            else if (identifierText == "System.Types.Int32")
            {
                return LLVM.Int32Type();
            }
            else if (identifierText == "System.LLVMTypes.I1")
            {
                return LLVM.Int1Type();
            }
            else if(identifierText == "System.LLVMTypes.I32")
            {
                return LLVM.Int32Type();
            }

            return null;
        }
    }
}
