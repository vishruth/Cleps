using Antlr4.Runtime;
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

        public override LLVMRegister VisitMemberDeclarationStatement([NotNull] ClepsParser.MemberDeclarationStatementContext context)
        {
            bool isStatic = context.STATIC() != null;
            ClepsParser.MemberFunctionDeclarationStatementContext memberDecarationContext = context.declarationStatement().memberFunctionDeclarationStatement();

            if (memberDecarationContext == null)
            {
                return null;
            }

            var assignmentFunctionDeclarationStatement = memberDecarationContext.assignmentFunctionDeclarationStatement();
            var functionDeclarationContext = memberDecarationContext.functionDeclarationStatement();

            if (assignmentFunctionDeclarationStatement != null)
            {
                return VisitAssignmentFunctionDeclarationStatement(assignmentFunctionDeclarationStatement, isStatic);
            }
            else
            {
                return VisitFunctionDeclarationStatement(functionDeclarationContext, isStatic);
            }
        }

        private LLVMRegister VisitAssignmentFunctionDeclarationStatement([NotNull] ClepsParser.AssignmentFunctionDeclarationStatementContext context, bool isStatic)
        {
            ClepsParser.TypenameAndVoidContext returnTypeContext = context.FunctionReturnType;
            ClepsParser.FunctionParametersListContext parametersContext = context.functionParametersList();
            string functionName = context.FunctionName.Text;
            return VisitFunctionDeclarationBody(context, returnTypeContext, parametersContext, functionName, isStatic);
        }

        private LLVMRegister VisitFunctionDeclarationStatement([NotNull] ClepsParser.FunctionDeclarationStatementContext context, bool isStatic)
        {
            ClepsParser.TypenameAndVoidContext returnTypeContext = context.FunctionReturnType;
            ClepsParser.FunctionParametersListContext parametersContext = context.functionParametersList();
            string functionName = context.FunctionName.Text;
            return VisitFunctionDeclarationBody(context, returnTypeContext, parametersContext, functionName, isStatic);
        }

        private LLVMRegister VisitFunctionDeclarationBody
        (
            Antlr4.Runtime.ParserRuleContext context,
            ClepsParser.TypenameAndVoidContext returnTypeContext,
            ClepsParser.FunctionParametersListContext parametersContext,
            string functionName,
            bool isStatic
        )
        {
            string className = String.Join(".", CurrentNamespaceAndClass);
            string fullyQualifiedName = String.Format("{0}.{1}", className, functionName);

            if (!ClassManager.DoesClassContainMember(className, functionName))
            {
                string errorMessage = String.Format("Class {0} does not have a definition for {1}", className, functionName);
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                //Don't process this member
                return null;
            }

            LLVMValueRef currFunc = LLVM.GetNamedFunction(Module, fullyQualifiedName);
            LLVMBasicBlockRef basicBlock = LLVM.GetFirstBasicBlock(currFunc);
            LLVM.PositionBuilderAtEnd(Builder, basicBlock);

            VariableManager.AddBlock();

            ClepsType clepsReturnType = ClepsType.GetBasicOrVoidType(returnTypeContext);
            List<string> paramNames = parametersContext._FunctionParameterNames.Select(p => p.GetText()).ToList();
            List<ClepsType> clepsParameterTypes = parametersContext._FunctionParameterTypes.Select(t => ClepsType.GetBasicType(t)).ToList();

            if (!isStatic)
            {
                ClepsType thisClassType = ClepsType.GetBasicType(className, 1);
                paramNames.Insert(0, "this");
                clepsParameterTypes.Insert(0, thisClassType);
            }

            List<LLVMValueRef> paramValueRegisters = currFunc.GetParams().ToList();

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
            LLVMTypeRef? primitiveTypeOrNull = ClepsLLVMTypeConvertorInst.GetPrimitiveLLVMTypeOrNull(clepsVariableType);
            LLVMValueRef variable;

            if (primitiveTypeOrNull != null)
            {
                variable = CreateVariableOnStack(variableName, primitiveTypeOrNull.Value);
            }
            else if (clepsVariableType.IsPointerType)
            {
                LLVMTypeRef? pointerType = ClepsLLVMTypeConvertorInst.GetLLVMTypeOrNull(clepsVariableType);
                if(pointerType == null)
                {
                    string errorMessage = String.Format("Could not find type {0}", clepsVariableType.GetTypeName());
                    Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                    return null;
                }

                variable = CreateVariableOnStack(variableName, pointerType.Value);
            }
            else
            {
                LLVMValueRef? constructorReturn = CallConstructorForType(context, clepsVariableType, variableName);
                if(constructorReturn == null)
                {
                    return null;
                }

                variable = constructorReturn.Value;
            }

            VariableManager.AddLocalVariable(variableName, clepsVariableType, variable);
            LLVMRegister ret = new LLVMRegister(clepsVariableType, variable);
            return ret;
        }

        private LLVMValueRef CreateVariableOnStack(string variableName, LLVMTypeRef variableType)
        {
            LLVMValueRef variablePtr = LLVM.BuildAlloca(Builder, variableType, variableName + "Ptr");
            LLVMValueRef variable = LLVM.BuildLoad(Builder, variablePtr, variableName);
            return variable;
        }

        private LLVMValueRef? CallConstructorForType(ParserRuleContext context, ClepsType clepsVariableType, string variableName)
        {
            string constructorToCall = String.Format("{0}.new", clepsVariableType.RawTypeName);
            if (!ClassManager.IsClassLoaded(clepsVariableType.RawTypeName))
            {
                string errorMessage = String.Format("Could not find type {0}", clepsVariableType.GetTypeName());
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                return null;
            }

            LLVMValueRef llvmConstructor = LLVM.GetNamedFunction(Module, constructorToCall);
            LLVMValueRef variablePtr = LLVM.BuildCall(Builder, llvmConstructor, new LLVMValueRef[0], variableName + "Ptr");
            LLVMValueRef variable = LLVM.BuildLoad(Builder, variablePtr, variableName);
            return variable;
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

            LLVMTypeRef llvmType = LLVM.Int32TypeInContext(Context);
            LLVMRegister ret = GetConstantIntRegisterOfClepsType(context, llvmType, value, "int32" /* friendly type name */);
            return ret;
        }

        public override LLVMRegister VisitBooleanAssignments([NotNull] ClepsParser.BooleanAssignmentsContext context)
        {
            bool boolValue = context.TRUE() != null;
            ulong value = boolValue ? 1u : 0u;
            LLVMTypeRef llvmType = LLVM.Int1TypeInContext(Context);
            LLVMRegister ret = GetConstantIntRegisterOfClepsType(context, llvmType, value, "bool" /* friendly type name */);
            return ret;
        }

        /// <summary>
        /// Return an LLVM variable defined on the stack. 
        /// The value of the initialized to this variable needs to be a constant bool, int or long
        /// This native llvm type is then mapped to the appropriate cleps type (which is specified in code by the rawtypemap statement) and returned
        /// </summary>
        /// <param name="context"></param>
        /// <param name="llvmType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private LLVMRegister GetConstantIntRegisterOfClepsType(ParserRuleContext context, LLVMTypeRef llvmType, ulong value, string friendlyTypeName)
        {
            ClepsType clepsType = ClepsLLVMTypeConvertorInst.GetClepsNativeLLVMType(llvmType);

            ClepsClass mappedClass;
            if (!ClassManager.RawLLVMTypeMappingClasses.TryGetValue(clepsType, out mappedClass))
            {
                string errorMessage = String.Format("Could not find a raw mapping for type {0}", clepsType.GetTypeName());
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                return null;
            }

            LLVMValueRef register = LLVM.ConstInt(llvmType, value, false);
            ClepsType mappedClassType = ClepsType.GetBasicType(mappedClass.FullyQualifiedName, 0);

            LLVMValueRef? constructorReturn = CallConstructorForType(context, mappedClassType, friendlyTypeName + "Inst");
            if (constructorReturn == null)
            {
                return null;
            }

            LLVMValueRef inst = constructorReturn.Value;
            LLVMValueRef instPtr = LLVM.BuildAlloca(Builder, LLVM.TypeOf(inst), friendlyTypeName + "InstPtr");
            LLVM.BuildStore(Builder, inst, instPtr);

            //the mapped type is always the first field
            LLVMValueRef instField = LLVM.BuildStructGEP(Builder, instPtr, 0, friendlyTypeName + "InstField");
            LLVMTypeRef instFieldType = LLVM.TypeOf(instField);

            LLVM.BuildStore(Builder, register, instField);

            LLVMRegister ret = new LLVMRegister(mappedClassType, inst);
            return ret;
        }

        #endregion Right Hand Expressions Implementations
    }
}
