The Cleps Programming Language
------------------------------

**Name**
C - C family (cross platform, curly braces, semi colons and so on)
L - Language
E - for Ease (easy to write, with high level constructs like C# or python, code generation natively supported etc.)
P - Performance (maintain the performance of programs written in Assembly C, C++)
S - and Security (guard against the ability to write code that can be exploited via overflows, thread timing etc. etc via contracts, refinement types and static analysis within the compiler)

To contribute
-------------
Add your name to contributors.txt in your first pull request

High level goals (in order of preference)
-----------------------------------------

The language may or may not meet all of the goals yet - Let's see how it pans out

	• Helps write bug free programs
		○ With all the guarantees of a managed language such as bounds and overflow checking etc.
		○ Contracts and Refinement types to help avoid errors
		○ Testing should as short and simple as possible. Language should be built to support easy testing.
		○ A permission model for the application built in to the language
_

	• Performance
		○ Compiled version should be tweaked to support maximum performance - built time will be sacrificed for run time perf
		○ Maximum tree shaking and inlining and restructuring of code
		○ PGO story should be strong and supported out of the box - auto memoization on functions based on PGO
		○ Avoid redundant array size checks
_

	• Clean and concise syntax
		○ Keep the syntax close to well-known object oriented languages and making changes where needed
_

	• Multi-threaded support
		○ Multi-threaded support should be built in to the language from the get go.
		○ Use language constructs to make it difficult to avoid race conditions
		○ Enforce strong isolation in threads where possible
		○ Moonshot - Automatic parallelization of code based on dependency checks and PGOs
_

	• Effective Memory management
		○ Overhead of running a garbage collector (both in terms of CPU and janking) should be avoided or minimized
		○ Language should avoid usage of heap as much as possible.
		○ Don't hide pointers from the programmer, but type safety should be enforced on pointers as well (unlike the more free approach of c and c++)
_

	• Runs in many environments
		○ Capable of running as a script, as well as natively on each platform
		○ Additionally, tentatively looking at compiling to existing widely used intermediate -> java byte code or CLR
		○ We should have a platform specific api layer to give each platform a unified layer like java. However, this should not be maintained as a dependency, rather this should be included in the compiled program.
_

	• Avoid fallback to alternate DSLs outside the language for niche scenarios
		○ Language should be very flexible so that a library can create a DSL within the language which will be checked during compilation - includes the definition of new arbitrary operators
		○ Computations done at build time should use this same language (Template Metaprogramming is often used as a build time as a way to compute during build time constant values in C++ to optimize performance - this is great except this feels like you learning a new language just to precompute factorial<5>)
		○ Build aka make files should be written in the same language
_

	• Developer Productivity
		○ Support rapid iterations of write and run - not be bogged by building and compilation
		○ Built in support for code generation - rather than external tooling for better integration of codegen in the language
		○ Compilation of a program should give strong guarantees about its correctness
		○ The IDE features also need attention
		○ Try to keep things similar to existing languages where possible making minimum changes. Though not explicitly a goal, due to the widespread use of object oriented languages - we will keep things object oriented and throw in functional concepts as needed

