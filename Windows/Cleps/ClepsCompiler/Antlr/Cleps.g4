grammar Cleps;

COMMENT_SINGLELINE : '//' ~[\r\n]* -> channel(HIDDEN);
COMMENT_MULTILINE : '/*' .*? ('/*' .*? '*/')* .*? '*/' -> channel(HIDDEN);
WS : [ \t\r\n]+ -> channel(HIDDEN);
USING : 'using';
END : ';';
NAMESPACE : 'namespace';
NEW : 'new';
CLASS : 'class';
STATIC : 'static';
PUBLIC : 'public';
INTERNAL : 'internal';
FUNC : 'fn';
VOID : 'void';
TRUE : 'true';
FALSE : 'false';
NULL : 'null';
IF : 'if';
FOR : 'for';
DO : 'do';
WHILE : 'while';
RETURN : 'return';
RAWTYPEMAP : 'rawtypemap';
ASSIGNMENT : 'assignment';
ASSIGNMENT_OPERATOR : '=';
PASCALCASE_ID : [A-Z] [a-zA-Z0-9_]*;
ID : [a-zA-Z] [a-zA-Z0-9_]*;
NUMERIC_TOKEN : [0-9]+ ('.' [0-9]+)?;
STRING : ID? '"' ('\\"'|.)*? '"'
	 |	ID? '\'\'' ('\\\''|.)*? '\'\''
	;
OPERATOR : ('+'|'-'|'*'|'/')+ 
	| '`' ('+'|'-'|'*'|'/'|[a-zA-Z0-9_])+ '`'
	;

///////////////////////////////////////////////////////

variable : '@' VariableName=(ID|PASCALCASE_ID);
nestedIdentifier : PASCALCASE_ID ('.' PASCALCASE_ID)*;
numeric : NumericValue=NUMERIC_TOKEN NumericType=ID?;
classOrMemberName : PASCALCASE_ID;

visibilityModifier : PUBLIC | INTERNAL;
typename : RawTypeName=nestedIdentifier (PtrIndirectionLevel+='*')* '!'?;
typenameAndVoid : typename | VOID;

///////////////////////////////////////////////////////

compilationUnit : namespaceBlockStatement;

namespaceBlockStatement : (NAMESPACE NamespaceName=nestedIdentifier '{' usingNamespaceStatements*)( namespaceBlockStatement | classDeclarationStatements)*('}');

usingNamespaceStatement : USING STATIC? nestedIdentifier END;
usingNamespaceStatements : usingNamespaceStatement+;

classDeclarationStatements : (visibilityModifier CLASS ClassName=classOrMemberName '{') classBodyStatements ('}');
classBodyStatements : 
(
		classDeclarationStatements
	|	memberVariableDeclarationStatement
	|	memberFunctionDeclarationStatement
	|	memberAssignmentFunctionDeclarationStatement
	|	rawTypeMapStatment
)*;

rawTypeMapStatment : RAWTYPEMAP typename END;

memberVariableDeclarationStatement : visibilityModifier STATIC? typename FieldName=classOrMemberName (ASSIGNMENT_OPERATOR rightHandExpression)? END;
memberFunctionDeclarationStatement : visibilityModifier STATIC? functionDeclarationStatement;
memberAssignmentFunctionDeclarationStatement : visibilityModifier STATIC? assignmentFunctionDeclarationStatement;

///////////////////////////////////////////////////////

rightHandExpression : 
	rightHandExpressionSimple # SimpleExpression
	| OPERATOR rightHandExpression # PreOperatorOnExpression
	| rightHandExpression OPERATOR rightHandExpression # BinaryOperatorOnExpression
	| rightHandExpression OPERATOR # PostOperatorOnExpression
	| rightHandExpression '.' functionCall # FunctionCallOnExpression
	| rightHandExpression '.' FieldName=classOrMemberName # FieldAccessOnExpression
	| '(' rightHandExpression ')' # BracketedExpression;

rightHandExpressionSimple : stringAssignments | numericAssignments | nullAssignment | booleanAssignments | functionCallAssignment | variableAssignment | typenameAssignment | classInstanceAssignment;
numericAssignments : numeric;
nullAssignment : NULL;
booleanAssignments : TRUE|FALSE;
stringAssignments : STRING;
functionCallAssignment : functionCall;
variableAssignment : variable;
fieldAssignment : classOrMemberName;
typenameAssignment : typename;
classInstanceAssignment : NEW typename '(' (FunctionParameters+=rightHandExpression (',' FunctionParameters+=rightHandExpression)*)? ')';

functionCall : FunctionName=classOrMemberName '(' (FunctionParameters+=rightHandExpression (',' FunctionParameters+=rightHandExpression)*)? ')';

/////////////////////////////////////////////////////////////

functionStatement : functionReturnStatement | functionVariableDeclarationStatement | functionFieldAssignmentStatement | functionVariableAssigmentStatement | functionDeclarationStatement | functionCallStatement | ifStatement | doWhileStatement;

functionReturnStatement : RETURN rightHandExpression? END;

functionVariableDeclarationStatement : variableDeclarationStatement;
variableDeclarationStatement : typename variable (ASSIGNMENT_OPERATOR rightHandExpression)? END;
functionVariableAssigmentStatement : variable ASSIGNMENT_OPERATOR rightHandExpression END;
functionFieldAssignmentStatement : LeftExpression=rightHandExpression '.' FieldName=classOrMemberName ASSIGNMENT_OPERATOR RightExpression=rightHandExpression END;

functionDeclarationStatement : FUNC FunctionName=classOrMemberName (ASSIGNMENT_OPERATOR FUNC FunctionReturnType=typenameAndVoid '(' functionParametersList ')' statementBlock)? END;
assignmentFunctionDeclarationStatement : ASSIGNMENT FunctionName=ASSIGNMENT_OPERATOR FUNC FunctionReturnType=typenameAndVoid '(' functionParametersList ')' statementBlock END;

functionParametersList : (FunctionParameterTypes+=typename FunctionParameterNames+=variable (',' FunctionParameterTypes+=typename FunctionParameterNames+=variable)*)?;
statementBlock : '{' functionStatement* '}';

functionCallStatement : (rightHandExpression '.')? functionCall END;

ifStatement : IF '(' rightHandExpression ')' statementBlock;

doWhileStatement : DO statementBlock WHILE '(' TerminalCondition=rightHandExpression ')' END;
