using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.CompilerHelpers
{
    /// <summary>
    /// A class the handles the set of cleps classes loaded (processed) by the compiler
    /// </summary>
    class ClassManager
    {
        public Dictionary<string, ClepsClass> LoadedClassesAndMembers { get; private set; }
        public List<string> MainFunctionFullNames { get; private set; }

        public ClassManager()
        {
            LoadedClassesAndMembers = new Dictionary<string, ClepsClass>();
            MainFunctionFullNames = new List<string>();
        }

        public bool IsClassLoaded(string className)
        {
            return LoadedClassesAndMembers.ContainsKey(className);
        }

        public void AddNewClass(string className)
        {
            Debug.Assert(!LoadedClassesAndMembers.ContainsKey(className));
            LoadedClassesAndMembers.Add(className, new ClepsClass());
        }

        public bool DoesClassContainMember(string className, string memberName)
        {
            Debug.Assert(LoadedClassesAndMembers.ContainsKey(className));
            return LoadedClassesAndMembers[className].DoesClassContainMember(memberName);
        }

        public void AddNewMember(string className, string memberName, bool isStatic, ClepsType memberType)
        {
            Debug.Assert(LoadedClassesAndMembers.ContainsKey(className));
            LoadedClassesAndMembers[className].AddNewMember(memberName, isStatic, memberType);

            if (memberType.IsFunctionType && isStatic && 
                (
                    memberType.FunctionReturnType == ClepsType.GetBasicType("System.Types.Int32", 0 /* ptrIndirectionLevel */) ||
                    memberType.FunctionReturnType == ClepsType.GetBasicType("System.LLVMTypes.I32", 0 /* ptrIndirectionLevel */)
                )
                && memberType.FunctionParameters.Count == 0 && memberName == "Main")
            {
                string fullyQualifiedName = className + "." + memberName;
                MainFunctionFullNames.Add(fullyQualifiedName);
            }
        }
    }
}
