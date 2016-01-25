using Antlr4.Runtime.Misc;
using ClepsCompiler.CompilerHelpers;
using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.Compiler
{
    /// <summary>
    /// This parser is used to generate LLVM bytecode for function and method bodies
    /// </summary>
    class ClepsFunctionBodyGeneratorParser : ClepsAbstractParser<LLVMValueRef>
    {
        private LLVMContextRef Context;
        private LLVMModuleRef Module;
        private LLVMBuilderRef Builder;

        private List<string> CurrentNamespaceAndClass;
        private VariableManager VariableManager;

        public ClepsFunctionBodyGeneratorParser(LLVMContextRef context, LLVMModuleRef module, LLVMBuilderRef builder)
        {
            Context = context;
            Module = module;
            Builder = builder;
        }

        public override LLVMValueRef VisitCompilationUnit([NotNull] ClepsParser.CompilationUnitContext context)
        {
            CurrentNamespaceAndClass = new List<String>();
            VariableManager = new VariableManager();

            var ret = VisitChildren(context);
            return ret;
        }

        public override LLVMValueRef VisitNamespaceBlockStatement([NotNull] ClepsParser.NamespaceBlockStatementContext context)
        {
            CurrentNamespaceAndClass.Add(context.NamespaceName.GetText());
            var ret = VisitChildren(context);
            CurrentNamespaceAndClass.RemoveAt(CurrentNamespaceAndClass.Count - 1);
            return ret;
        }

        public override LLVMValueRef VisitClassDeclarationStatements([NotNull] ClepsParser.ClassDeclarationStatementsContext context)
        {
            CurrentNamespaceAndClass.Add(context.ClassName.Text);
            var ret = VisitChildren(context);
            CurrentNamespaceAndClass.RemoveAt(CurrentNamespaceAndClass.Count - 1);
            return ret;
        }

        public override LLVMValueRef VisitFunctionDeclarationStatement([NotNull] ClepsParser.FunctionDeclarationStatementContext context)
        {
            string functionName = context.FunctionName.Text;
            string fullyQualifiedName = String.Format("{0}.{1}", String.Join(".", CurrentNamespaceAndClass), functionName);

            LLVMValueRef currFunc = LLVM.GetNamedFunction(Module, fullyQualifiedName);
            LLVMBasicBlockRef basicBlock = LLVM.GetFirstBasicBlock(currFunc);
            LLVM.PositionBuilderAtEnd(Builder, basicBlock);

            ClepsType clepsReturnType = ClepsType.GetBasicOrVoidType(context.FunctionReturnType);
            List<ClepsType> clepsParameterTypes = context.functionParametersList()._FunctionParameterTypes.Select(t => ClepsType.GetBasicType(t)).ToList();

            List<string> paramNames = context.functionParametersList()._FunctionParameterNames.Select(p => p.GetText()).ToList();
            List<LLVMValueRef> paramValueRegisters = currFunc.GetParams().ToList();

            VariableManager.AddBlock();

            paramNames.Zip(clepsParameterTypes, (ParamName, ParamType) => new { ParamName, ParamType })
                .Zip(paramValueRegisters, (ParamNameAndType, ParamRegister) => new { ParamNameAndType.ParamName, ParamNameAndType.ParamType, ParamRegister })
                .ToList()
                .ForEach(p => VariableManager.AddLocalVariable(p.ParamName, p.ParamType, p.ParamRegister));

            var ret = VisitChildren(context);

            VariableManager.RemoveBlock();
            return ret;
        }

        public override LLVMValueRef VisitStatementBlock([NotNull] ClepsParser.StatementBlockContext context)
        {
            VariableManager.AddBlock();
            var ret = VisitChildren(context);
            VariableManager.RemoveBlock();

            return ret;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////// Function Statement Implementations ///////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Function Statement Implementations

        public override LLVMValueRef VisitFunctionReturnStatement([NotNull] ClepsParser.FunctionReturnStatementContext context)
        {
            LLVMValueRef returnValueRegister;

            if(context.rightHandExpression() == null)
            {
                returnValueRegister = LLVM.BuildRetVoid(Builder);
            }
            else
            {
                LLVMValueRef returnValue = Visit(context.rightHandExpression());
                returnValueRegister = LLVM.BuildRet(Builder, returnValue);
            }

            return returnValueRegister;
        }

        #endregion Function Statement Implementations

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////// Right Hand Expressions Implementations ///////////////////////////////////// 
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Right Hand Expressions Implementations

        public override LLVMValueRef VisitVariableAssignment([NotNull] ClepsParser.VariableAssignmentContext context)
        {
            string variableName = context.VariableName.Text;
            LLVMValueRef ret = VariableManager.GetVariable(variableName).LLVMValueRef;
            return ret;
        }

        public override LLVMValueRef VisitNumericAssignments([NotNull] ClepsParser.NumericAssignmentsContext context)
        {
            string numericValueString = context.NUMERIC().GetText();
            ulong numericValue;
            if(!ulong.TryParse(numericValueString, out numericValue))
            {
                throw new Exception(String.Format("Numeric value {0} not a valid int", numericValueString));
            }

            LLVMValueRef constantValue = LLVM.ConstInt(LLVM.Int32Type(), numericValue, false);
            return constantValue;
        }

        public override LLVMValueRef VisitBooleanAssignments([NotNull] ClepsParser.BooleanAssignmentsContext context)
        {
            bool boolValue = context.TRUE() != null;
            ulong value = boolValue ? 1u : 0u;
            LLVMValueRef boolRegister = LLVM.ConstInt(LLVM.Int1Type(), value, false);
            return boolRegister;
        }

        #endregion Right Hand Expressions Implementations
    }
}
