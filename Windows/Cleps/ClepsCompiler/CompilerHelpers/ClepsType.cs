using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.CompilerHelpers
{
    /// <summary>
    /// The properties of a cleps type
    /// </summary>
    class ClepsType
    {
        public bool IsVoidType { get; private set; }
        public bool IsFunctionType { get; private set; }
        public bool IsBasicType { get; private set; }

        public string RawTypeName { get; private set; }
        public int PtrIndirectionLevel { get; private set; }
        public ClepsType FunctionReturnType { get; private set; }
        public List<ClepsType> FunctionParameters { get; private set; }

        private ClepsType() { }

        private static bool IsEqual(ClepsType c1, ClepsType c2)
        {
            if (c1.IsVoidType == true && c2.IsVoidType == true)
            {
                return true;
            }

            if (c1.IsFunctionType == true && c2.IsFunctionType == true &&
                c1.FunctionReturnType == c2.FunctionReturnType &&
                c1.FunctionParameters.Count == c2.FunctionParameters.Count &&
                c1.FunctionParameters.Zip(c2.FunctionParameters, (p1, p2) => p1 == p2).All(e => e))
            {
                return true;
            }

            if (c1.IsBasicType == true && c2.IsBasicType == true && c1.RawTypeName == c2.RawTypeName && c1.PtrIndirectionLevel == c2.PtrIndirectionLevel)
            {
                return true;
            }

            return false;
        }

        public static bool operator == (ClepsType c1, ClepsType c2)
        {
            return IsEqual(c1, c2);
        }

        public static bool operator != (ClepsType c1, ClepsType c2)
        {
            return !IsEqual(c1, c2);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || typeof(ClepsType) != obj.GetType())
            {
                return false;
            }

            ClepsType p = (ClepsType)obj;
            return this == p;
        }

        public override int GetHashCode()
        {
            if(IsVoidType)
            {
                return -1;
            }

            if (IsFunctionType)
            {
                //TODO: Make more robust
                return FunctionReturnType.GetHashCode();
            }

            if (IsBasicType)
            {
                return RawTypeName.GetHashCode();
            }

            throw new Exception("Type is not void, basic or a function type");
        }

        public static ClepsType GetBasicType(string rawTypeName, int ptrIndirectionLevel)
        {
            return new ClepsType()
            {
                IsFunctionType = false,
                IsVoidType = false,
                IsBasicType = true,
                RawTypeName = rawTypeName,
                PtrIndirectionLevel = ptrIndirectionLevel
            };
        }

        public static ClepsType GetBasicType(ClepsParser.TypenameContext typeContext)
        {
            return GetBasicType(typeContext.RawTypeName.GetText(), typeContext._PtrIndirectionLevel.Count);
        }

        public static ClepsType GetPointerToBasicType(ClepsType basicType)
        {
            if(!basicType.IsBasicType)
            {
                throw new Exception("Expected basic type");
            }

            return GetBasicType(basicType.RawTypeName, basicType.PtrIndirectionLevel + 1);
        }

        public static ClepsType GetVoidType()
        {
            return new ClepsType()
            {
                IsFunctionType = false,
                IsVoidType = true,
                IsBasicType = false
            };
        }

        public static ClepsType GetBasicOrVoidType(ClepsParser.TypenameAndVoidContext typenameAndVoidContext)
        {
            if(typenameAndVoidContext.VOID() != null)
            {
                return GetVoidType();
            }

            return GetBasicType(typenameAndVoidContext.typename());
        }

        public static ClepsType GetFunctionType(ClepsType functionReturnType, List<ClepsType> functionParameters)
        {
            return new ClepsType()
            {
                IsFunctionType = true,
                IsVoidType = false,
                IsBasicType = false,
                FunctionReturnType = functionReturnType,
                FunctionParameters = functionParameters
            };
        }

        public string GetTypeName()
        {
            if (IsBasicType || IsFunctionType)
            {
                return RawTypeName + new string('*', PtrIndirectionLevel);
            }
            else if (IsVoidType)
            {
                return "void";
            }
            else
            {
                throw new Exception("Unknown cleps type category");
            }
        }

        public override string ToString()
        {
            if(IsBasicType)
            {
                return RawTypeName + new string('*', PtrIndirectionLevel); ;
            }
            else if(IsVoidType)
            {
                return "void";
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
