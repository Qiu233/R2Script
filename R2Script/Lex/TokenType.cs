using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Lex
{
	public enum TokenType
	{
		TK_FIRST = 128,
		

		TK_NAME,
		TK_NULL,
		TK_NUMBER,
		TK_STRING,




		TK_OP_LE,       //<=
		TK_OP_GE,       //>=
		TK_OP_EQ,       //==
		TK_OP_NE,       //!=
		TK_OP_L_AND,    //&&
		TK_OP_L_OR,     //||
		TK_OP_LSHIFT,   //<<
		TK_OP_RSHIFT,   //>>


		TK_DE_PLUS_EQ,      //+=
		TK_DE_MINUS_EQ,     //-=
		TK_DE_MUL_EQ,       //*=
		TK_DE_DIV_EQ,       ///=
		TK_DE_LSHIFT_EQ,    //<<=
		TK_DE_RSHIFT_EQ,    //>>=
		
		TK_KW_RETURN,       //return
		TK_KW_IF,           //if
		TK_KW_ELSEIF,           //elif
		TK_KW_ELSE,     //else
		TK_KW_FOR,           //for
		TK_KW_WHILE,           //while
		TK_KW_BREAK,           //break
		TK_KW_CONTINUE,           //break
		TK_KW_FUNCTION,           //function
		TK_KW_NAKED,           //naked
		TK_KW_VAR,           //var

		TK_SEG_ASM,

	}
}
