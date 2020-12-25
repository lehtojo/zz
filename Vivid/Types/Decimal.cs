﻿public class Decimal : Number
{
	public Decimal() : base(Format.DECIMAL, 32, false, "decimal") { }

	public override void AddDefinition(Mangle mangle)
	{
		mangle.Value += Parser.Size.Bits == 64 ? "d" : "f";
	}

	public override int GetReferenceSize()
	{
		return Parser.Bytes;
	}

	public override int GetContentSize()
	{
		return Parser.Bytes;
	}
}