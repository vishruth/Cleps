using Antlr4.Runtime;
using ClepsCompiler.CompilerHelpers;
using LLVMSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.Compiler
{
    /// <summary>
    /// This is the main driver/controller of the compilation process
    /// </summary>
    class CompilerDriver
    {
        private List<string> Files;
        private string OutputFileName;

        public CompilerDriver(List<string> files, string outputFileName)
        {
            Files = files;
            OutputFileName = outputFileName;
        }

        public CompileStatus CompileFiles()
        {
            ClassManager classManager = new ClassManager();
            CompileStatus status = new CompileStatus(true /* exit on first error */);

            LLVMContextRef context = LLVM.ContextCreate();
            LLVMModuleRef module = LLVM.ModuleCreateWithName(OutputFileName);
            LLVMBuilderRef builder = LLVM.CreateBuilder();

            try
            {
                //Byte code is generated in multiple passes so that all member variables and functions are stubbed out before they are referred to in function bodies
                //This allows functions on the top of a file to call functions on the bottom of a file as well

                {
                    ClepsClassNamesParser classSkeletonGenerator = new ClepsClassNamesParser(classManager, status);
                    ParseFilesWithGenerator(classSkeletonGenerator, status);
                }
                {
                    ClepsMemberParser memberGenerator = new ClepsMemberParser(classManager, status);
                    ParseFilesWithGenerator(memberGenerator, status);
                }

                GenerateClassAndFunctionSkeletons(classManager, status, context, module, builder);
                AddEntryPoint(classManager, status, context, module, builder);

                {
                    ClepsFunctionBodyGeneratorParser functionBodyGenerator = new ClepsFunctionBodyGeneratorParser(context, module, builder);
                    ParseFilesWithGenerator(functionBodyGenerator, status);
                }

                VerifyModule(module, status);

                PrintModuleToFile(module, status);
            }
            catch (CompilerErrorException)
            {
                //Supress compiler errors
            }
            finally
            {
                LLVM.DisposeBuilder(builder);
                LLVM.DisposeModule(module);
                LLVM.ContextDispose(context);
            }

            return status;
        }

        private void GenerateClassAndFunctionSkeletons(ClassManager classManager, CompileStatus status, LLVMContextRef context, LLVMModuleRef module, LLVMBuilderRef builder)
        {
            foreach (KeyValuePair<string, ClepsClass> classNameAndDetails in classManager.LoadedClassesAndMembers)
            {
                string className = classNameAndDetails.Key;
                ClepsClass classDetails = classNameAndDetails.Value;

                LLVMTypeRef[] memberTypes = classDetails.MemberVariables.Values.Select(clepsType => ClepsLLVMTypeConvertor.GetPrimitiveLLVMTypeOrNull(clepsType).Value).ToArray();
                LLVMTypeRef structType = LLVM.StructCreateNamed(context, className);
                LLVM.StructSetBody(structType, memberTypes, false);

                foreach(KeyValuePair<string, ClepsType> functionNameAndDetails in classDetails.MemberMethods)
                {
                    string functionName = functionNameAndDetails.Key;
                    ClepsType functionDetails = functionNameAndDetails.Value;

                    string fullyQualifiedName = String.Format("{0}.{1}", className, functionName);
                    LLVMTypeRef llvmReturnType = ClepsLLVMTypeConvertor.GetPrimitiveLLVMTypeOrNull(functionDetails.FunctionReturnType).Value;
                    LLVMTypeRef[] llvmParameterTypes = functionDetails.FunctionParameters.Select(c => ClepsLLVMTypeConvertor.GetPrimitiveLLVMTypeOrNull(c).Value).ToArray();
                    LLVMTypeRef funcType = LLVM.FunctionType(llvmReturnType, llvmParameterTypes, false);
                    LLVMValueRef newFunc = LLVM.AddFunction(module, fullyQualifiedName, funcType);

                    LLVMBasicBlockRef blockRef = LLVM.AppendBasicBlock(newFunc, "entry");
                    LLVM.PositionBuilderAtEnd(builder, blockRef);

                    List<LLVMValueRef> paramValueRegisters = newFunc.GetParams().ToList();

                    //Debug.Assert(paramNames.Count() == llvmParameterTypes.Count());
                    //Debug.Assert(paramNames.Count() == paramValueRegisters.Count());
                }
            }
        }

        private void ParseFilesWithGenerator<T>(ClepsAbstractParser<T> generator, CompileStatus status)
        {
            foreach (string file in Files)
            {
                LexerParserErrorHandler lexerParserErrorHandler = new LexerParserErrorHandler(file, status);
                var data = File.ReadAllText(file);

                AntlrInputStream s = new AntlrInputStream(data);
                ClepsLexer lexer = new ClepsLexer(s);
                CommonTokenStream tokens = new CommonTokenStream(lexer);
                ClepsParser parser = new ClepsParser(tokens);

                parser.RemoveErrorListeners();
                parser.AddErrorListener(lexerParserErrorHandler);
                var parsedFile = parser.compilationUnit();

                generator.ParseFile(file, parsedFile);
            }
        }

        private void AddEntryPoint(ClassManager classManager, CompileStatus status, LLVMContextRef context, LLVMModuleRef module, LLVMBuilderRef builder)
        {
            LLVMTypeRef functionType = LLVM.FunctionType(LLVM.Int32Type(), new LLVMTypeRef[] { }, false);
            LLVMValueRef functionValue = LLVM.AddFunction(module, "main", functionType);
            LLVMBasicBlockRef blockValue = LLVM.AppendBasicBlockInContext(context, functionValue, "entry");
            LLVM.PositionBuilderAtEnd(builder, blockValue);

            LLVMValueRef intRet = LLVM.ConstInt(LLVM.Int32Type(), 0, false);

            if (classManager.MainFunctionFullNames.Count < 1)
            {
                status.AddError(new CompilerError("", 0, 0, "No main functions found in the program"));
            }
            else if (classManager.MainFunctionFullNames.Count > 1)
            {
                status.AddError(new CompilerError("", 0, 0, "Multiple main functions found in the program: " + String.Join(",", classManager.MainFunctionFullNames)));
            }
            else
            {
                LLVMValueRef functionToCall = LLVM.GetNamedFunction(module, classManager.MainFunctionFullNames.First());
                intRet = LLVM.BuildCall(builder, functionToCall, new LLVMValueRef[0], "entryPointCall");
            }

            LLVM.BuildRet(builder, intRet);
        }

        private void VerifyModule(LLVMModuleRef module, CompileStatus status)
        {
            IntPtr llvmErrorMessagePtr;
            //VerifyModule returns an inverted result...
            LLVMBool llvmFailure = LLVM.VerifyModule(module, LLVMVerifierFailureAction.LLVMReturnStatusAction, out llvmErrorMessagePtr);
            string errorMessage = Marshal.PtrToStringAnsi(llvmErrorMessagePtr);
            LLVM.DisposeMessage(llvmErrorMessagePtr);

            if (llvmFailure)
            {
                status.AddError(new CompilerError(OutputFileName, 0, 0, "Module Verification failed : " + errorMessage));
            }
        }

        private void PrintModuleToFile(LLVMModuleRef module, CompileStatus status)
        {
            IntPtr llvmErrorMessagePtr;
            LLVMBool llvmFailure = LLVM.PrintModuleToFile(module, OutputFileName, out llvmErrorMessagePtr);
            string errorMessage = Marshal.PtrToStringAnsi(llvmErrorMessagePtr);
            LLVM.DisposeMessage(llvmErrorMessagePtr);

            if (llvmFailure)
            {
                status.AddError(new CompilerError(OutputFileName, 0, 0, "Module Output failed : " + errorMessage));
            }
        }
    }
}
