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
ASSIGNMENT : '=';
ID : [a-zA-Z] [a-zA-Z0-9_]*;
NUMERIC : [0-9]+ ('.' [0-9]+)? ID?;
STRING : ID? '"' ('\\"'|.)*? '"'
	 |	ID? '\'\'' ('\\\''|.)*? '\'\''
	;
OPERATOR : ('+'|'-'|'*'|'/')+ 
	| '`' ('+'|'-'|'*'|'/'|[a-zA-Z0-9_])+ '`'
	;

///////////////////////////////////////////////////////

nestedIdentifier : ID ('.' ID)*;

compilationUnit : namespaceBlockStatement;

namespaceBlockStatement : (NAMESPACE NamespaceName=nestedIdentifier '{' usingNamespaceStatements*)( namespaceBlockStatement | classDeclarationStatements)*('}');

usingNamespaceStatement : USING STATIC? nestedIdentifier END;
usingNamespaceStatements : usingNamespaceStatement+;

visibilityModifier : PUBLIC | INTERNAL;

classDeclarationStatements : (visibilityModifier CLASS ClassName=ID '{') classBodyStatements ('}');
classBodyStatements : 
(
		classDeclarationStatements
	|	memberDeclarationStatement
)*;

typename : '$' RawTypeName=nestedIdentifier '!'?;
typenameAndVoid : typename | VOID;

///////////////////////////////////////////////////////

memberDeclarationStatement : visibilityModifier STATIC? declarationStatement;

declarationStatement : memberVariableDeclarationStatement | memberFunctionDeclarationStatement;

memberVariableDeclarationStatement : variableDeclarationStatement;
memberFunctionDeclarationStatement : functionDeclarationStatement;

///////////////////////////////////////////////////////

rightHandExpression : 
	rightHandExpressionSimple # SimpleExpression
	| OPERATOR rightHandExpression # PreOperatorOnExpression
	| rightHandExpression OPERATOR rightHandExpression # BinaryOperatorOnExpression
	| rightHandExpression OPERATOR # PostOperatorOnExpression
	| rightHandExpression '.' functionCall # FunctionCallOnExpression
	| rightHandExpression '.' ID # FieldAccessOnExpression
	| '(' rightHandExpression ')' # BracketedExpression;

rightHandExpressionSimple : stringAssignments | numericAssignments | nullAssignment | booleanAssignments | functionCallAssignment | variableAssignment | typenameAssignment | classInstanceAssignment;
numericAssignments : NUMERIC;
nullAssignment : NULL;
booleanAssignments : TRUE|FALSE;
stringAssignments : STRING;
functionCallAssignment : functionCall;
variableAssignment : VariableName=ID;
typenameAssignment : typename;
classInstanceAssignment : NEW typename '(' (FunctionParameters+=rightHandExpression (',' FunctionParameters+=rightHandExpression)*)? ')';

functionCall : FunctionName=ID '(' (FunctionParameters+=rightHandExpression (',' FunctionParameters+=rightHandExpression)*)? ')';

/////////////////////////////////////////////////////////////

functionStatement : functionVariableDeclarationStatement | functionFieldAssignmentStatement | functionVariableAssigmentStatement | functionDeclarationStatement | functionCallStatement | ifStatement | doWhileStatement;

functionVariableDeclarationStatement : variableDeclarationStatement;
variableDeclarationStatement : typename VariableName=ID (ASSIGNMENT rightHandExpression)? END;
functionVariableAssigmentStatement : VariableName=ID ASSIGNMENT rightHandExpression END;
functionFieldAssignmentStatement : LeftExpression=rightHandExpression '.' VariableName=ID ASSIGNMENT RightExpression=rightHandExpression END;

functionDeclarationStatement : FUNC typenameAndVoid FunctionName=ID (ASSIGNMENT '(' functionParametersList ')' statementBlock)? END;
functionParametersList : (typename nestedIdentifier (',' typename nestedIdentifier)*)?;
statementBlock : '{' functionStatement* '}';

functionCallStatement : (rightHandExpression '.')? functionCall END;

ifStatement : IF '(' rightHandExpression ')' statementBlock;

doWhileStatement : DO statementBlock WHILE '(' TerminalCondition=rightHandExpression ')' END;
