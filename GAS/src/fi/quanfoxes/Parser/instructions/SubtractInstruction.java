package fi.quanfoxes.Parser.instructions;

import fi.quanfoxes.Lexer.Token;

public class SubtractInstruction extends OperatorInstruction {
    public SubtractInstruction(Token source, Token destination) {
        super(source, destination);
    }
}
