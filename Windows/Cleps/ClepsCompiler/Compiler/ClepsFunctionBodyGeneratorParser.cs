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
    class ClepsFunctionBodyGeneratorParser : ClepsAbstractParser<LLVMRegister>
    {
        private ClassManager ClassManager;
        private CompileStatus Status;
        private LLVMContextRef Context;
        private LLVMModuleRef Module;
        private LLVMBuilderRef Builder;
        private ClepsLLVMTypeConvertor ClepsLLVMTypeConvertorInst;

        private List<string> CurrentNamespaceAndClass;
        private VariableManager VariableManager;

        public ClepsFunctionBodyGeneratorParser(ClassManager classManager, CompileStatus status, LLVMContextRef context, LLVMModuleRef module, LLVMBuilderRef builder, ClepsLLVMTypeConvertor clepsLLVMTypeConvertor)
        {
            ClassManager = classManager;
            Status = status;
            Context = context;
            Module = module;
            Builder = builder;
            ClepsLLVMTypeConvertorInst = clepsLLVMTypeConvertor;
        }

        public override LLVMRegister VisitCompilationUnit([NotNull] ClepsParser.CompilationUnitContext context)
        {
            CurrentNamespaceAndClass = new List<String>();
            VariableManager = new VariableManager();

            var ret = VisitChildren(context);
            return ret;
        }

        public override LLVMRegister VisitNamespaceBlockStatement([NotNull] ClepsParser.NamespaceBlockStatementContext context)
        {
            CurrentNamespaceAndClass.Add(context.NamespaceName.GetText());
            var ret = VisitChildren(context);
            CurrentNamespaceAndClass.RemoveAt(CurrentNamespaceAndClass.Count - 1);
            return ret;
        }

        public override LLVMRegister VisitClassDeclarationStatements([NotNull] ClepsParser.ClassDeclarationStatementsContext context)
        {
            CurrentNamespaceAndClass.Add(context.ClassName.Text);
            var ret = VisitChildren(context);
            CurrentNamespaceAndClass.RemoveAt(CurrentNamespaceAndClass.Count - 1);
            return ret;
        }

        public override LLVMRegister VisitAssignmentFunctionDeclarationStatement([NotNull] ClepsParser.AssignmentFunctionDeclarationStatementContext context)
        {
            ClepsParser.TypenameAndVoidContext returnTypeContext = context.FunctionReturnType;
            ClepsParser.FunctionParametersListContext parametersContext = context.functionParametersList();
            string functionName = context.FunctionName.Text;
            return ImplementFunctionBody(context, returnTypeContext, parametersContext, functionName);
        }

        public override LLVMRegister VisitFunctionDeclarationStatement([NotNull] ClepsParser.FunctionDeclarationStatementContext context)
        {
            ClepsParser.TypenameAndVoidContext returnTypeContext = context.FunctionReturnType;
            ClepsParser.FunctionParametersListContext parametersContext = context.functionParametersList();
            string functionName = context.FunctionName.Text;
            return ImplementFunctionBody(context, returnTypeContext, parametersContext, functionName);
        }

        private LLVMRegister ImplementFunctionBody<T>
        (
            T context, 
            ClepsParser.TypenameAndVoidContext returnTypeContext, 
            ClepsParser.FunctionParametersListContext parametersContext, 
            string functionName
        ) where T : Antlr4.Runtime.ParserRuleContext
        {
            string className = String.Join(".", CurrentNamespaceAndClass);
            string fullyQualifiedName = String.Format("{0}.{1}", className, functionName);

            if(!ClassManager.DoesClassContainMember(className, functionName))
            {
                string errorMessage = String.Format("Class {0} does not have a definition for {1}", className, functionName);
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                //Don't process this member
                return null;
            }

            LLVMValueRef currFunc = LLVM.GetNamedFunction(Module, fullyQualifiedName);
            LLVMBasicBlockRef basicBlock = LLVM.GetFirstBasicBlock(currFunc);
            LLVM.PositionBuilderAtEnd(Builder, basicBlock);

            ClepsType clepsReturnType = ClepsType.GetBasicOrVoidType(returnTypeContext);
            List<ClepsType> clepsParameterTypes = parametersContext._FunctionParameterTypes.Select(t => ClepsType.GetBasicType(t)).ToList();

            List<string> paramNames = parametersContext._FunctionParameterNames.Select(p => p.GetText()).ToList();
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

        public override LLVMRegister VisitStatementBlock([NotNull] ClepsParser.StatementBlockContext context)
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

        public override LLVMRegister VisitFunctionReturnStatement([NotNull] ClepsParser.FunctionReturnStatementContext context)
        {
            LLVMValueRef returnValueRegister;
            ClepsType returnType;

            if(context.rightHandExpression() == null)
            {
                returnValueRegister = LLVM.BuildRetVoid(Builder);
                returnType = ClepsType.GetVoidType();
            }
            else
            {
                var returnValue = Visit(context.rightHandExpression());
                returnValueRegister = LLVM.BuildRet(Builder, returnValue.LLVMValueRef);
                returnType = returnValue.VariableType;
            }

            var ret = new LLVMRegister(returnType, returnValueRegister);
            return ret;
        }

        public override LLVMRegister VisitFunctionVariableDeclarationStatement([NotNull] ClepsParser.FunctionVariableDeclarationStatementContext context)
        {
            ClepsParser.VariableDeclarationStatementContext variableDeclarationStatement = context.variableDeclarationStatement();
            string variableName = variableDeclarationStatement.VariableName.Text;
            
            if(VariableManager.IsVariableDefined(variableName))
            {
                string errorMessage = String.Format("Variable {0} is already defined", variableName);
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                return null;
            }

            ClepsType clepsVariableType = ClepsType.GetBasicType(variableDeclarationStatement.typename());
            LLVMTypeRef? llvmVariableType = ClepsLLVMTypeConvertorInst.GetPrimitiveLLVMTypeOrNull(clepsVariableType);

            if(llvmVariableType == null)
            {
                string errorMessage = String.Format("Type {0} was not found", clepsVariableType.GetTypeName());
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                return null;
            }

            LLVMValueRef variablePtr = LLVM.BuildAlloca(Builder, llvmVariableType.Value, variableName);
            VariableManager.AddLocalVariable(variableName, clepsVariableType, variablePtr);
            LLVMRegister ret = new LLVMRegister(clepsVariableType, variablePtr);

            return ret;
        }

        #endregion Function Statement Implementations

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////// Right Hand Expressions Implementations ///////////////////////////////////// 
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Right Hand Expressions Implementations

        public override LLVMRegister VisitVariableAssignment([NotNull] ClepsParser.VariableAssignmentContext context)
        {
            string variableName = context.VariableName.Text;
            LLVMRegister ret = VariableManager.GetVariable(variableName);
            return ret;
        }

        public override LLVMRegister VisitNumericAssignments([NotNull] ClepsParser.NumericAssignmentsContext context)
        {
            string valueString = context.NUMERIC().GetText();
            ulong value;
            if(!ulong.TryParse(valueString, out value))
            {
                throw new Exception(String.Format("Numeric value {0} not a valid int", valueString));
            }

            LLVMTypeRef llvmType = LLVM.Int32Type();
            ClepsType clepsType = ClepsLLVMTypeConvertor.GetClepsType(llvmType);
            LLVMValueRef register = LLVM.ConstInt(llvmType, value, false);
            LLVMRegister ret = new LLVMRegister(clepsType, register);
            return ret;
        }

        public override LLVMRegister VisitBooleanAssignments([NotNull] ClepsParser.BooleanAssignmentsContext context)
        {
            bool boolValue = context.TRUE() != null;
            ulong value = boolValue ? 1u : 0u;
            LLVMTypeRef llvmType = LLVM.Int1Type();
            ClepsType clepsType = ClepsLLVMTypeConvertor.GetClepsType(llvmType);
            LLVMValueRef register = LLVM.ConstInt(llvmType, value, false);
            LLVMRegister ret = new LLVMRegister(clepsType, register);
            return ret;
        }

        #endregion Right Hand Expressions Implementations
    }
}
