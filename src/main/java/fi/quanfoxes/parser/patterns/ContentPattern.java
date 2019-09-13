package fi.quanfoxes.parser.patterns;

import fi.quanfoxes.lexer.ContentToken;
import fi.quanfoxes.lexer.ParenthesisType;
import fi.quanfoxes.lexer.Token;
import fi.quanfoxes.lexer.TokenType;
import fi.quanfoxes.parser.Context;
import fi.quanfoxes.parser.Node;
import fi.quanfoxes.parser.Pattern;
import fi.quanfoxes.parser.Singleton;

import java.util.List;

public class ContentPattern extends Pattern {
    public static final int PRIORITY = 16;

    private static final int CONTENT = 0;

    public ContentPattern() {
        super(TokenType.CONTENT);
    }

    @Override
    public int priority(List<Token> tokens) {
        return PRIORITY;
    }

    @Override
    public boolean passes(List<Token> tokens) {
        // Only content with parenthesis type of '()' or '[]' can be automatically parsed
        ContentToken content = (ContentToken)tokens.get(CONTENT);
        return content.getParenthesisType() == ParenthesisType.PARENTHESIS;
    }

    @Override
    public Node build(Context context, List<Token> tokens) throws Exception {
        return Singleton.getContent(context, (ContentToken)tokens.get(CONTENT));
    }
}
