using System.Collections.Generic;

public class UnarySignPattern : Pattern
{
	public const int PRIORITY = 18;

	public const int SIGN = 0;
	public const int OBJECT = 1;

	// Pattern 1: - $value
	// Pattern 2: + $value
	public UnarySignPattern() : base
	(
		TokenType.OPERATOR,
		TokenType.OBJECT
	) { }

	public override int GetPriority(List<Token> tokens)
	{
		return PRIORITY;
	}

	public override bool Passes(Context context, PatternState state, List<Token> tokens)
	{
		var sign = tokens[SIGN].To<OperatorToken>().Operator;

		if (sign != Operators.ADD && sign != Operators.SUBTRACT)
		{
			return false;
		}

		return state.Start == 0 || state.Tokens[state.Start - 1].Is(TokenType.OPERATOR, TokenType.KEYWORD);
	}

	public override Node? Build(Context context, PatternState state, List<Token> tokens)
	{
		var target = Singleton.Parse(context, tokens[OBJECT]);
		var sign = tokens[SIGN].To<OperatorToken>().Operator;

		if (target is NumberNode number)
		{
			if (sign == Operators.SUBTRACT) number.Negate();
			return number;
		}

		return sign == Operators.SUBTRACT ? new NegateNode(target, tokens[SIGN].Position) : target;
	}
}