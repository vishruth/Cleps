using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using ClepsCompiler.CompilerHelpers;
using System.Diagnostics;

namespace ClepsCompiler.Compiler
{
    /// <summary>
    /// This parser is used to generate a list of classes defined in the code
    /// </summary>
    class ClepsClassNamesParser : ClepsAbstractParser<int>
    {
        private ClassManager ClassManager;
        private CompileStatus Status;

        private List<string> CurrentNamespaceAndClass;

        public ClepsClassNamesParser(ClassManager classManager, CompileStatus status)
        {
            ClassManager = classManager;
            Status = status;
        }

        public override int VisitCompilationUnit([NotNull] ClepsParser.CompilationUnitContext context)
        {
            CurrentNamespaceAndClass = new List<String>();
            var ret = VisitChildren(context);
            return ret;
        }

        public override int VisitNamespaceBlockStatement([NotNull] ClepsParser.NamespaceBlockStatementContext context)
        {
            CurrentNamespaceAndClass.Add(context.NamespaceName.GetText());
            var ret = VisitChildren(context);
            CurrentNamespaceAndClass.RemoveAt(CurrentNamespaceAndClass.Count - 1);
            return ret;
        }

        public override int VisitClassDeclarationStatements([NotNull] ClepsParser.ClassDeclarationStatementsContext context)
        {
            CurrentNamespaceAndClass.Add(context.ClassName.Text);

            string className = String.Join(".", CurrentNamespaceAndClass);
            
            //if this qualified name already exists, there is an error
            if(ClassManager.IsClassLoaded(className))
            {
                string errorMessage = String.Format("Class {0} has multiple definitions", className);
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                //Don't process this class
                return -1;
            }

            ClassManager.AddNewClass(className);

            CurrentNamespaceAndClass.RemoveAt(CurrentNamespaceAndClass.Count - 1);
            return 0;
        }
    }
}
