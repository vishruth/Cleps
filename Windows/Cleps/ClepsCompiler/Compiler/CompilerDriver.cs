using Antlr4.Runtime;
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
            CompileStatus status = new CompileStatus();

            LLVMContextRef context = LLVM.ContextCreate();
            LLVMModuleRef module = LLVM.ModuleCreateWithNameInContext(OutputFileName, context);
            LLVMBuilderRef builder = LLVM.CreateBuilderInContext(context);

            try
            {
                ClepsClassSkeletonGenerator classSkeletonGenerator = new ClepsClassSkeletonGenerator(context, module, builder);

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
                    status.ThrowOnError();

                    classSkeletonGenerator.Visit(parsedFile);
                    status.ThrowOnError();
                }

                VerifyModule(module, status);
                status.ThrowOnError();

                PrintModuleToFile(module, status);
                status.ThrowOnError();
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

        private void VerifyModule(LLVMModuleRef module, CompileStatus status)
        {
            IntPtr llvmErrorMessagePtr;
            LLVMBool llvmSuccess = LLVM.VerifyModule(module, LLVMVerifierFailureAction.LLVMReturnStatusAction, out llvmErrorMessagePtr);
            string errorMessage = Marshal.PtrToStringAnsi(llvmErrorMessagePtr);
            LLVM.DisposeMessage(llvmErrorMessagePtr);

            if (!llvmSuccess)
            {
                status.AddError(new CompilerError(OutputFileName, 0, 0, "Module Verification failed : " + errorMessage));
            }
        }

        private void PrintModuleToFile(LLVMModuleRef module, CompileStatus status)
        {
            IntPtr llvmErrorMessagePtr;
            LLVMBool llvmSuccess = LLVM.PrintModuleToFile(module, OutputFileName, out llvmErrorMessagePtr);
            string errorMessage = Marshal.PtrToStringAnsi(llvmErrorMessagePtr);
            LLVM.DisposeMessage(llvmErrorMessagePtr);

            if (!llvmSuccess)
            {
                status.AddError(new CompilerError(OutputFileName, 0, 0, "Module Output failed : " + errorMessage));
            }
        }
    }
}
