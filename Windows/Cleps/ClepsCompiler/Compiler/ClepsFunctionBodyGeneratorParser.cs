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
                .ForEach(p => {
                    LLVMValueRef functionParamPtr = LLVM.BuildAlloca(Builder, LLVM.TypeOf(p.ParamRegister), p.ParamName);
                    VariableManager.AddLocalVariable(p.ParamName, p.ParamType, functionParamPtr);
                });

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

        // Note - the return of statements are not really used. It is more used in the expression returns, which is in the next section

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
                var returnValuePtr = Visit(context.rightHandExpression());
                returnValueRegister = LLVM.BuildLoad(Builder, returnValuePtr.LLVMPtrValueRef, "returnValue");
                LLVM.BuildRet(Builder, returnValueRegister);
                returnType = returnValuePtr.VariableType;
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
                variable = LLVM.BuildAlloca(Builder, primitiveTypeOrNull.Value, variableName + "Ptr");
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

                variable = LLVM.BuildAlloca(Builder, pointerType.Value, variableName + "Ptr");
            }
            else
            {
                LLVMValueRef? constructorReturn = CallConstructorAllocaForType(context, clepsVariableType, variableName);
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

        private LLVMValueRef? CallConstructorAllocaForType(ParserRuleContext context, ClepsType clepsVariableType, string variableName)
        {
            string constructorToCall = String.Format("{0}.new", clepsVariableType.RawTypeName);
            if (!ClassManager.IsClassLoaded(clepsVariableType.RawTypeName))
            {
                string errorMessage = String.Format("Could not find type {0}", clepsVariableType.GetTypeName());
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                return null;
            }

            LLVMValueRef llvmConstructor = LLVM.GetNamedFunction(Module, constructorToCall);
            LLVMValueRef variable = LLVM.BuildCall(Builder, llvmConstructor, new LLVMValueRef[0], variableName);
            LLVMValueRef variablePtr = LLVM.BuildAlloca(Builder, LLVM.TypeOf(variable), variableName + "Ptr");
            LLVM.BuildStore(Builder, variable, variablePtr);
            return variablePtr;
        }

        public override LLVMRegister VisitIfStatement([NotNull] ClepsParser.IfStatementContext context)
        {
            ClepsParser.RightHandExpressionContext condition = context.rightHandExpression();
            LLVMRegister expressionValue = Visit(condition);

            ClepsType nativeBooleanType = ClepsType.GetBasicType("System.LLVMTypes.I1", 0 /* ptr indirection level */);
            LLVMValueRef? conditionRegisterPtr = null;

            //handle native llvm boolean type
            if (expressionValue.VariableType == nativeBooleanType)
            {
                conditionRegisterPtr = expressionValue.LLVMPtrValueRef;
            }
            //handle cleps llvm boolean type
            else if (ClassManager.RawLLVMTypeMappingClasses.ContainsKey(nativeBooleanType))
            {
                ClepsClass mappedBooleanClass = ClassManager.RawLLVMTypeMappingClasses[nativeBooleanType];
                ClepsType mappedBooleanType = ClepsType.GetBasicType(mappedBooleanClass.FullyQualifiedName, 0 /* ptr indirection level */);

                if (expressionValue.VariableType == mappedBooleanType)
                {
                    //if the mapped type exists, then below can never be null, so call value automatically
                    LLVMTypeRef mappedBooleanTypeInLLVM = ClepsLLVMTypeConvertorInst.GetLLVMTypeOrNull(mappedBooleanType).Value;
                    //get the first field in the mapped type - see rawtypemap for more details
                    conditionRegisterPtr = LLVM.BuildStructGEP(Builder, expressionValue.LLVMPtrValueRef, 0, "ifCondBooleanFieldPtr");
                }
            }

            if(conditionRegisterPtr == null)
            {
                string errorMessage = String.Format("The condition expression in the if condition returns type {0} instead of a boolean expression. ", expressionValue.VariableType.GetTypeName());
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                //just assume this is condition is true to avoid stalling the compilation
                conditionRegisterPtr = LLVM.ConstInt(LLVM.Int1TypeInContext(Context), (ulong)1, false);
            }

            LLVMValueRef conditionRegister = LLVM.BuildLoad(Builder, conditionRegisterPtr.Value, "ifCondBooleanField");
            LLVMValueRef currentFunction = LLVM.GetInsertBlock(Builder).GetBasicBlockParent();
            LLVMBasicBlockRef ifThenBlock = LLVM.AppendBasicBlockInContext(Context, currentFunction, "ifthen");
            LLVMBasicBlockRef ifEndBlock = LLVM.AppendBasicBlockInContext(Context, currentFunction, "ifend");

            LLVM.BuildCondBr(Builder, conditionRegister, ifThenBlock, ifEndBlock);
            LLVM.PositionBuilderAtEnd(Builder, ifThenBlock);
            Visit(context.statementBlock());
            LLVM.BuildBr(Builder, ifEndBlock);
            LLVM.PositionBuilderAtEnd(Builder, ifEndBlock);

            return expressionValue;
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
            string numericType = context.NumericType != null ? context.NumericType.Text : "";

            ulong value;
            if(!ulong.TryParse(valueString, out value))
            {
                throw new Exception(String.Format("Numeric value {0} not a valid int", valueString));
            }


            LLVMTypeRef llvmType = LLVM.Int32TypeInContext(Context);

            //hardcoded identifiers for llvm i.e. native types
            if (numericType == "ni")
            {
                LLVMValueRef valueToRet = LLVM.ConstInt(llvmType, value, false);
                LLVMValueRef valuePtr = LLVM.BuildAlloca(Builder, LLVM.TypeOf(valueToRet), "nativeIntValuePtr");
                LLVM.BuildStore(Builder, valueToRet, valuePtr);
                LLVMRegister nativeRet = new LLVMRegister(ClepsLLVMTypeConvertorInst.GetClepsNativeLLVMType(llvmType), valuePtr);
                return nativeRet;
            }

            if (!String.IsNullOrEmpty(numericType))
            {
                //for now other numeric types are not supported
                string errorMessage = String.Format("Numerics of type {0} was not found. Make sure the class that defined {0} was imported.", numericType);
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                //just assume this is numericType is "" to avoid stalling the compilation
            }

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

            LLVMValueRef? instPtr = CallConstructorAllocaForType(context, mappedClassType, friendlyTypeName + "Inst");
            if (instPtr == null)
            {
                return null;
            }

            //the mapped type is always the first field
            LLVMValueRef instField = LLVM.BuildStructGEP(Builder, instPtr.Value, 0, friendlyTypeName + "InstField");
            LLVMTypeRef instFieldType = LLVM.TypeOf(instField);

            LLVM.BuildStore(Builder, register, instField);

            LLVMRegister ret = new LLVMRegister(mappedClassType, instPtr.Value);
            return ret;
        }

        #endregion Right Hand Expressions Implementations
    }
}
