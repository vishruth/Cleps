using Antlr4.Runtime.Misc;
using ClepsCompiler.CompilerHelpers;
using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.Compiler
{
    /// <summary>
    /// This parser is used to generate a list of members defined in the various classes created in the code
    /// </summary>
    class ClepsMemberParser : ClepsAbstractParser<int>
    {
        private ClassManager ClassManager;
        private CompileStatus Status;

        private List<string> CurrentNamespaceAndClass;

        public ClepsMemberParser(ClassManager classManager, CompileStatus status)
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
            var ret = VisitChildren(context);
            CurrentNamespaceAndClass.RemoveAt(CurrentNamespaceAndClass.Count - 1);
            return ret;
        }

        public override int VisitMemberDeclarationStatement([NotNull] ClepsParser.MemberDeclarationStatementContext context)
        {
            bool isStatic = context.STATIC() != null;

            ClepsParser.MemberFunctionDeclarationStatementContext memberFunctionContext = context.declarationStatement().memberFunctionDeclarationStatement();
            ClepsParser.MemberVariableDeclarationStatementContext memberVariableContext = context.declarationStatement().memberVariableDeclarationStatement();

            if (memberFunctionContext != null)
            {
                return DeclareFunction(memberFunctionContext, isStatic);
            }
            else
            {
                return DeclareVariable(memberVariableContext, isStatic);
            }
        }

        private int DeclareFunction([NotNull] ClepsParser.MemberFunctionDeclarationStatementContext context, bool isStatic)
        {
            string className = String.Join(".", CurrentNamespaceAndClass);
            var functionDeclarationContext = context.functionDeclarationStatement();
            var assignmentFunctionDeclarationContext = context.assignmentFunctionDeclarationStatement();

            string functionName;
            ClepsParser.TypenameAndVoidContext functionReturnContext;
            ClepsParser.FunctionParametersListContext parameterContext;

            if (functionDeclarationContext != null)
            {
                functionName = functionDeclarationContext.FunctionName.Text;
                functionReturnContext = functionDeclarationContext.FunctionReturnType;
                parameterContext = functionDeclarationContext.functionParametersList();
            }
            else
            {
                functionName = assignmentFunctionDeclarationContext.FunctionName.Text;
                functionReturnContext = assignmentFunctionDeclarationContext.FunctionReturnType;
                parameterContext = assignmentFunctionDeclarationContext.functionParametersList();

                if (!ClepsType.GetBasicOrVoidType(functionReturnContext).IsVoidType)
                {
                    string errorMessage = "Assignment operator definitions should have a void return type";
                    Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                    //Don't process this member
                    return -1;
                }
            }

            string fullyQualifiedName = String.Format("{0}.{1}", String.Join(".", CurrentNamespaceAndClass), functionName);

            if (ClassManager.DoesClassContainMember(className, functionName))
            {
                string errorMessage = String.Format("Class {0} has multiple definitions of member {1}", className, functionName);
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                //Don't process this member
                return -1;
            }

            ClepsType clepsReturnType = ClepsType.GetBasicOrVoidType(functionReturnContext);
            List<ClepsType> clepsParameterTypes = parameterContext._FunctionParameterTypes.Select(t => ClepsType.GetBasicType(t)).ToList();
            if(!isStatic)
            {
                ClepsType currentClassPtrType = ClepsType.GetBasicType(className, 1 /* */);
                clepsParameterTypes.Insert(0, currentClassPtrType);
            }

            ClepsType clepsFunctionType = ClepsType.GetFunctionType(clepsReturnType, clepsParameterTypes);

            ClassManager.AddNewMember(className, functionName, isStatic, clepsFunctionType);
            return 0;
        }

        private int DeclareVariable([NotNull] ClepsParser.MemberVariableDeclarationStatementContext context, bool isStatic)
        {
            string className = String.Join(".", CurrentNamespaceAndClass);
            string variableName = context.variableDeclarationStatement().VariableName.Text;

            if (ClassManager.DoesClassContainMember(className, variableName))
            {
                string errorMessage = String.Format("Class {0} has multiple definitions of member {1}", className, variableName);
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                //Don't process this member
                return -1;
            }

            ClepsParser.TypenameContext variableTypeContext = context.variableDeclarationStatement().typename();
            ClepsType clepsVariableType = ClepsType.GetBasicType(variableTypeContext);

            ClassManager.AddNewMember(className, variableName, isStatic, clepsVariableType);

            return 0;            
        }
    }
}
