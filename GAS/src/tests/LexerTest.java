package tests;

import fi.quanfoxes.DataType;
import fi.quanfoxes.DataTypeDatabase;
import fi.quanfoxes.KeywordDatabase;
import fi.quanfoxes.Lexer.*;
import org.junit.jupiter.api.Test;

import java.util.Arrays;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertIterableEquals;
import static org.junit.jupiter.api.Assertions.assertSame;

public class LexerTest {

    public void assertTokenArea (String input, int start, Lexer.TextType exceptedType, int exceptedStart, int exceptedEnd) throws Exception {
        Lexer.TokenArea area = Lexer.getNextTokenArea(input, start);

        assertSame(exceptedType, area.type);
        assertSame(exceptedStart, area.start);
        assertSame(exceptedEnd, area.end);
    }

    @Test
    public void tokenArea_typeDetectionStart() throws Exception {
        assertTokenArea("num a = 0;", 0, Lexer.TextType.TEXT, 0, 3);
    }

    @Test
    public void tokenArea_numberDetection() throws Exception {
        assertTokenArea("num a = 1234;", 8, Lexer.TextType.NUMBER, 8, 12);
    }

    @Test
    public void tokenArea_decimalNumberDetection() throws Exception {
        assertTokenArea("num a = 1.234;", 8, Lexer.TextType.NUMBER, 8, 13);
    }
    @Test
    public void tokenArea_assignOperator() throws Exception {
        assertTokenArea("num a = 1.234;", 5, Lexer.TextType.OPERATOR, 6, 7);
    }

    @Test
    public void tokenArea_simpleFunction() throws Exception {
        assertTokenArea("apple() * apple();", 0, Lexer.TextType.TEXT, 0, 5);
    }

    @Test
    public void tokenArea_functionWithParameters() throws Exception {
        assertTokenArea("num a = banana(1 + 2 * (3 - 4));", 7, Lexer.TextType.TEXT, 8, 14);
    }

    @Test
    public void tokenArea_richFunctionName() throws Exception {
        assertTokenArea("a = this_Is_Very_Weird_Function(apple() + banana() * 3 / 2) % 2;", 3, Lexer.TextType.TEXT,4, 31);
    }

    @Test
    public void tokenArea_hexadecimal() throws Exception {
        assertTokenArea("0xFF;", 0, Lexer.TextType.NUMBER, 0, 4);
    }

    @Test
    public void tokenArea_simpleContent() throws Exception {
        assertTokenArea("decimal b = apple() + (4 * banana() + 5) & orange();", 21, Lexer.TextType.CONTENT, 22, 40);
    }

    @Test
    public void tokenArea_operatorAndContent() throws Exception {
        assertTokenArea("c = 5*(4+a);", 5, Lexer.TextType.OPERATOR, 5, 6);
    }

    @Test
    public void tokenArea_noClosingParenthesis()  {
        try {
            assertTokenArea("c = (banana() + apple(orange() * 5) % 3", 4, Lexer.TextType.CONTENT, 4, 35);
        }
        catch (Exception e) {
            assertSame(1, 1);
            return;
        }

        assertSame(1, 2);
    }

    @Test
    public void tokenArea_missingOperator()  {
        try {
            assertTokenArea("a = 4(3/a);", 4, Lexer.TextType.NUMBER, 4, 5);
        }
        catch (Exception e) {
            assertSame(1, 1);
            return;
        }

        assertSame(1, 2);
    }

    @Test
    public void tokens_math () throws Exception {
        DataTypeDatabase.initialize();

        List<Token> actual = Lexer.getTokens("num a = 2 * b");
        List<Token> excepted = Arrays.asList
        (
            new DataTypeToken("num"),
            new NameToken("a"),
            new OperatorToken(OperatorType.ASSIGN),
            new NumberToken((byte)2),
            new OperatorToken(OperatorType.MULTIPLY),
            new NameToken("b")
        );

        assertIterableEquals(excepted, actual);
    }

    @Test
    public void tokens_math_functions () throws Exception {
        DataTypeDatabase.initialize();

        List<Token> actual = Lexer.getTokens("num a = banana() + apple(5 % b)");
        List<Token> excepted = Arrays.asList
        (
                new DataTypeToken("num"),
                new NameToken("a"),
                new OperatorToken(OperatorType.ASSIGN),
                new FunctionToken(new NameToken("banana"), new ContentToken("()")),
                new OperatorToken(OperatorType.ADD),
                new FunctionToken(new NameToken("apple"), new ContentToken(
                    new NumberToken((byte)5),
                    new OperatorToken(OperatorType.MODULUS),
                    new NameToken("b")
                ))
        );

        assertIterableEquals(excepted, actual);
    }

    @Test
    public void tokens_math_functions_and_content () throws Exception {
        DataTypeDatabase.initialize();

        List<Token> actual = Lexer.getTokens("num variable = banana() * ( apple() & 3 | 55 ^ 777 )");
        List<Token> excepted = Arrays.asList
        (
                new DataTypeToken("num"),
                new NameToken("variable"),
                new OperatorToken(OperatorType.ASSIGN),
                new FunctionToken(new NameToken("banana"), new ContentToken("()")),
                new OperatorToken(OperatorType.MULTIPLY),
                new ContentToken(
                    new FunctionToken(new NameToken("apple"), new ContentToken("()")),
                    new OperatorToken(OperatorType.BITWISE_AND),
                    new NumberToken(3),
                    new OperatorToken(OperatorType.BITWISE_OR),
                    new NumberToken(55),
                    new OperatorToken(OperatorType.BITWISE_XOR),
                    new NumberToken(777)
                )
        );

        assertIterableEquals(excepted, actual);
    }

    @Test
    public void tokens_keyword () throws Exception {
        DataTypeDatabase.initialize();
        KeywordDatabase.initialize();

        List<Token> actual = Lexer.getTokens("type banana");
        List<Token> excepted = Arrays.asList
        (
            new KeywordToken(KeywordDatabase.get("type")),
            new NameToken("banana")
        );

        assertIterableEquals(excepted, actual);
    }

    @Test
    public void tokens_loop () throws Exception {
        DataTypeDatabase.initialize();
        KeywordDatabase.initialize();

        List<Token> actual = Lexer.getTokens("loop (3)");
        List<Token> excepted = Arrays.asList
        (
            new KeywordToken(KeywordDatabase.get("loop")),
            new ContentToken(new NumberToken(3))
        );

        assertIterableEquals(excepted, actual);
    }

    @Test
    public void tokens_if () throws Exception {
        DataTypeDatabase.initialize();
        KeywordDatabase.initialize();

        List<Token> actual = Lexer.getTokens("if (a <= b)");
        List<Token> excepted = Arrays.asList
        (
            new KeywordToken(KeywordDatabase.get("if")),
            new ContentToken(
                    new NameToken("a"),
                    new OperatorToken(OperatorType.LESS_OR_EQUAL),
                    new NameToken("b"))
        );

        assertIterableEquals(excepted, actual);
    }

    @Test
    public void tokens_advanced_if () throws Exception {
        DataTypeDatabase.initialize();
        KeywordDatabase.initialize();

        List<Token> actual = Lexer.getTokens("if (a > b && (a < (c + apple(d / e, f % banana()))))");
        List<Token> excepted = Arrays.asList
        (
                new KeywordToken(KeywordDatabase.get("if")),
                new ContentToken(
                        new NameToken("a"),
                        new OperatorToken(OperatorType.GREATER_THAN),
                        new NameToken("b"),
                        new OperatorToken(OperatorType.AND),
                        new ContentToken(
                                new NameToken("a"),
                                new OperatorToken(OperatorType.LESS_THAN),
                                new ContentToken(
                                        new NameToken("c"),
                                        new OperatorToken(OperatorType.ADD),
                                        new FunctionToken(new NameToken("apple"),
                                                new ContentToken(
                                                        new ContentToken(
                                                                new NameToken("d"),
                                                                new OperatorToken(OperatorType.DIVIDE),
                                                                new NameToken("e")),
                                                new ContentToken(
                                                        new NameToken("f"),
                                                        new OperatorToken(OperatorType.MODULUS),
                                                        new FunctionToken(new NameToken("banana"),
                                                                new ContentToken("()"))))))))
        );

        assertIterableEquals(excepted, actual);
    }

    @Test
    public void tokens_unrecognized_token () throws Exception {
        DataTypeDatabase.add(new DataType("num"));

        try {
            Lexer.getTokens("num a = b ` c");
        }
        catch (Exception e) {
            assertSame(e.getMessage(), "Unrecognized token");
            return;
        }

        assertSame(1, 2);
    }
}
