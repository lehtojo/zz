﻿using System.Collections.Generic;
using System.Linq;

class ReturnPattern : Pattern
{
	public const int PRIORITY = 0;

	public const int RETURN = 0;
	public const int VALUE = 1;

	// Pattern: => ...
	public ReturnPattern() : base
	(
		TokenType.OPERATOR, TokenType.OBJECT
	) { }

	public override int GetPriority(List<Token> tokens)
	{
		return PRIORITY;
	}

	public override bool Passes(Context context, PatternState state, List<Token> tokens)
	{
		return tokens[RETURN].To<OperatorToken>().Operator == Operators.HEAVY_ARROW;
	}

	public override Node Build(Context context, PatternState state, List<Token> tokens)
	{
		var value = Singleton.Parse(context, tokens[VALUE]);

		return new ReturnNode(value, tokens[RETURN].Position);
	}
}