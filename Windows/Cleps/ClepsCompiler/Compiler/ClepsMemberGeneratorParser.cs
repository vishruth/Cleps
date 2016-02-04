using Antlr4.Runtime;
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
    class ClepsMemberGeneratorParser : ClepsAbstractParser<int>
    {
        private ClassManager ClassManager;
        private CompileStatus Status;
        private LLVMContextRef Context;
        private LLVMModuleRef Module;
        private LLVMBuilderRef Builder;
        private ClepsLLVMTypeConvertor ClepsLLVMTypeConvertorInst;

        private List<string> CurrentNamespaceAndClass;

        public ClepsMemberGeneratorParser(ClassManager classManager, CompileStatus status, LLVMContextRef context, LLVMModuleRef module, LLVMBuilderRef builder, ClepsLLVMTypeConvertor clepsLLVMTypeConvertor)
        {
            ClassManager = classManager;
            Status = status;
            Context = context;
            Module = module;
            Builder = builder;
            ClepsLLVMTypeConvertorInst = clepsLLVMTypeConvertor;
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

            string className = String.Join(".", CurrentNamespaceAndClass);
            ClepsClass classDetails = ClassManager.LoadedClassesAndMembers[className];

            ClepsType classType = ClepsType.GetBasicType(className, 0 /* ptrIndirectionLevel */);
            LLVMTypeRef? structType = ClepsLLVMTypeConvertorInst.GetLLVMTypeOrNull(classType);

            Debug.Assert(structType != null);

            LLVMTypeRef[] memberTypes = GetLLVMTypesArrFromValidClepsTypesList(context, classDetails.MemberVariables.Values.ToList());
            LLVM.StructSetBody(structType.Value, memberTypes, false);

            ValidateClass(context, classDetails);
            AddConstructor(structType.Value, className);

            CurrentNamespaceAndClass.RemoveAt(CurrentNamespaceAndClass.Count - 1);
            return ret;
        }

        private LLVMTypeRef[] GetLLVMTypesArrFromValidClepsTypesList(ParserRuleContext context, List<ClepsType> list)
        {
            List<LLVMTypeRef> memberTypes = new List<LLVMTypeRef>(list.Count);

            foreach (ClepsType clepsMemberType in list)
            {
                LLVMTypeRef? llvmMemberType = ClepsLLVMTypeConvertorInst.GetLLVMTypeOrNull(clepsMemberType);
                if (llvmMemberType == null)
                {
                    string errorMessage = String.Format("Type {0} was not found", clepsMemberType.GetTypeName());
                    Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                    //If the type is not found, try to continue by assuming int32. Compiler will still show an error
                    llvmMemberType = ClepsLLVMTypeConvertorInst.GetLLVMTypeOrNull(ClepsType.GetBasicType("System.LLVMTypes.I32", 0 /* pointer indirection level */));
                }

                memberTypes.Add(llvmMemberType.Value);
            }

            return memberTypes.ToArray();
        }

        private void ValidateClass(ParserRuleContext context, ClepsClass clepsClass)
        {
            ClepsType rawLLVMTypeMap = clepsClass.RawLLVMTypeMap;

            if (rawLLVMTypeMap != null)
            {
                ClepsType firstMember = clepsClass.MemberVariables.Values.FirstOrDefault();
                if(firstMember != rawLLVMTypeMap)
                {
                    string errorMessage = String.Format("Class {0} is mapped to the raw llvm type {1}. However the first member of this class is of type {2}", clepsClass.FullyQualifiedName, clepsClass.RawLLVMTypeMap.GetTypeName(), firstMember.GetTypeName());
                    Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                }
            }
        }

        private void AddConstructor(LLVMTypeRef structType, string className)
        {
            LLVMTypeRef structTypePtr = LLVM.PointerType(structType, 0 /* Global address space*/);
            LLVMTypeRef constructorType = LLVM.FunctionType(structTypePtr, new LLVMTypeRef[] { }, false);
            string constructorName = String.Format("{0}.new", className);
            LLVMValueRef constructor = LLVM.AddFunction(Module, constructorName, constructorType);
            LLVMBasicBlockRef block = LLVM.AppendBasicBlockInContext(Context, constructor, "entry");
            LLVM.PositionBuilderAtEnd(Builder, block);

            LLVMValueRef instance = LLVM.BuildAlloca(Builder, structType, "Inst");
            LLVM.BuildRet(Builder, instance);
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

            LLVMTypeRef? llvmReturnType = ClepsLLVMTypeConvertorInst.GetLLVMTypeOrNull(clepsReturnType);

            if (llvmReturnType == null)
            {
                string errorMessage = String.Format("Type {0} was not found", clepsReturnType.GetTypeName());
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));

                //If the return type is not found, try to continue by assuming a void return. Compiler will still show an error
                clepsReturnType = ClepsType.GetVoidType();
                llvmReturnType = ClepsLLVMTypeConvertorInst.GetLLVMTypeOrNull(clepsReturnType).Value;
            }

            LLVMTypeRef[] llvmParameterTypes = GetLLVMTypesArrFromValidClepsTypesList(context, clepsParameterTypes);
            LLVMTypeRef funcType = LLVM.FunctionType(llvmReturnType.Value, llvmParameterTypes, false);
            LLVMValueRef newFunc = LLVM.AddFunction(Module, fullyQualifiedName, funcType);

            LLVMBasicBlockRef blockRef = LLVM.AppendBasicBlockInContext(Context, newFunc, "entry");
            LLVM.PositionBuilderAtEnd(Builder, blockRef);

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

            // only static members are defined immediately. member variables are defined at the at end of parsing a class
            if (isStatic)
            {
                string fullyQualifiedName = String.Format("{0}.{1}", className, variableName);
                LLVMTypeRef? llvmMemberType = ClepsLLVMTypeConvertorInst.GetLLVMTypeOrNull(clepsVariableType);

                if (llvmMemberType == null)
                {
                    string errorMessage = String.Format("Type {0} was not found", clepsVariableType.GetTypeName());
                    Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                    return -1;
                }

                LLVM.AddGlobal(Module, llvmMemberType.Value, fullyQualifiedName);
            }

            ClassManager.AddNewMember(className, variableName, isStatic, clepsVariableType);
            return 0;            
        }

        public override int VisitRawTypeMapStatment([NotNull] ClepsParser.RawTypeMapStatmentContext context)
        {
            string className = String.Join(".", CurrentNamespaceAndClass);
            ClepsType rawLLVMType = ClepsType.GetBasicType(context.typename());

            //make sure this maps to an llvm type
            LLVMTypeRef? llvmType = ClepsLLVMTypeConvertorInst.GetPrimitiveLLVMTypeOrNull(rawLLVMType);
            if(llvmType == null)
            {
                string errorMessage = String.Format("Class {0} has a raw llvm type mapping to {1} which is not a valid llvm type", className, rawLLVMType.GetTypeName());
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                return -1;
            }

            if (ClassManager.ClassContainsRawLLVMTypeMapping(className))
            {
                string errorMessage = String.Format("Class {0} already has a raw llvm type mapping to {1}. Cannot add another raw type mapping to {2}", className, ClassManager.LoadedClassesAndMembers[className].RawLLVMTypeMap.GetTypeName(), rawLLVMType.GetTypeName());
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                return -1;
            }

            if(ClassManager.RawLLVMTypeMappingExists(rawLLVMType))
            {
                string errorMessage = String.Format("Raw llvm type {0} already has a mapping to {1}. Cannot add another raw type mapping to {2}", rawLLVMType.GetTypeName(), ClassManager.RawLLVMTypeMappingClasses[rawLLVMType].FullyQualifiedName, className);
                Status.AddError(new CompilerError(FileName, context.Start.Line, context.Start.Column, errorMessage));
                return -1;
            }

            ClassManager.AddRawLLVMTypeMapping(className, rawLLVMType);
            return 0;
        }
    }
}
