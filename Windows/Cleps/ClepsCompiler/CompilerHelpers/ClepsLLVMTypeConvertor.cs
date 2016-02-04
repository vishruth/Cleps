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
        private Dictionary<string, LLVMTypeRef> ClassSkeletons;
        private LLVMContextRef Context;

        public ClepsLLVMTypeConvertor(Dictionary<string, LLVMTypeRef> classSkeletons, LLVMContextRef context)
        {
            Context = context;
            ClassSkeletons = classSkeletons;
        }

        public LLVMTypeRef? GetPrimitiveLLVMTypeOrNull(ClepsType clepsType)
        {
            if (clepsType.IsFunctionType)
            {
                throw new Exception("Cannot convert function cleps type to llvm type");
            }

            if (clepsType.IsVoidType)
            {
                return LLVM.VoidTypeInContext(Context);
            }

            string identifierText = clepsType.RawTypeName;
            int indirectionLevel = clepsType.PtrIndirectionLevel;

            if (identifierText == "System.LLVMTypes.I1")
            {
                return GetPtrToLLVMType(LLVM.Int1TypeInContext(Context), indirectionLevel);
            }
            else if (identifierText == "System.LLVMTypes.I32")
            {
                return GetPtrToLLVMType(LLVM.Int32TypeInContext(Context), indirectionLevel);
            }

            return null;
        }

        public LLVMTypeRef? GetLLVMTypeOrNull(ClepsType clepsType)
        {
            LLVMTypeRef? primitiveLLVMType = GetPrimitiveLLVMTypeOrNull(clepsType);

            if (primitiveLLVMType != null)
            {
                return primitiveLLVMType;
            }

            string identifierText = clepsType.RawTypeName;
            int indirectionLevel = clepsType.PtrIndirectionLevel;

            LLVMTypeRef ret;
            if (ClassSkeletons.TryGetValue(identifierText, out ret))
            {
                return GetPtrToLLVMType(ClassSkeletons[identifierText], indirectionLevel);
            }

            return null;
        }

        public ClepsType GetClepsType(LLVMTypeRef llvmType)
        {
            if(llvmType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                if(llvmType.GetIntTypeWidth() == 1)
                {
                    return ClepsType.GetBasicType("System.Types.Bool", 0 /* ptr indirection level */);
                }
                else if (llvmType.GetIntTypeWidth() == 32)
                {
                    return ClepsType.GetBasicType("System.Types.Int32", 0 /* ptr indirection level */);
                }
            }
            else if(llvmType.TypeKind == LLVMTypeKind.LLVMVoidTypeKind)
            {
                //void type does not have a special implementation in Cleps - use the same thing for LLVM or cleps native types
                return ClepsType.GetVoidType();
            }

            throw new Exception("Unknown llvm type - cannot convert to cleps type");
        }

        public ClepsType GetClepsNativeLLVMType(LLVMTypeRef llvmType)
        {
            if (llvmType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
            {
                if (llvmType.GetIntTypeWidth() == 1)
                {
                    return ClepsType.GetBasicType("System.LLVMTypes.I1", 0 /* ptr indirection level */);
                }
                else if (llvmType.GetIntTypeWidth() == 32)
                {
                    return ClepsType.GetBasicType("System.LLVMTypes.I32", 0 /* ptr indirection level */);
                }
            }
            else if (llvmType.TypeKind == LLVMTypeKind.LLVMVoidTypeKind)
            {
                //void type does not have a special implementation in Cleps - use the same thing for LLVM or cleps native types
                return ClepsType.GetVoidType();
            }

            throw new Exception("Unknown llvm type - cannot convert to cleps native type");
        }

        private LLVMTypeRef GetPtrToLLVMType(LLVMTypeRef sourceType, int indirectionLevel)
        {
            LLVMTypeRef currentType = sourceType;
            for(int i = 0; i < indirectionLevel; i++)
            {
                currentType = LLVM.PointerType(sourceType, 0/* Global Address Space */);
            }
            return currentType;
        }
    }
}
