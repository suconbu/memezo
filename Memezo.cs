﻿// MemezoScript - Embeded scripting environment.
// Based on https://github.com/Timu5/BasicSharp

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Suconbu.Scripting.Memezo
{
    public enum ValueType { Number, String }
    public enum ErrorType
    {
        UnexpectedToken, UnknownToken, MissingToken, UndeclaredIdentifier,
        NotSupportedOperation, UnknownOperator, InvalidNumberOfArguments, InvalidDataType,
        InvalidNumberFormat, InvalidStringLiteral, UnknownError
    }
    public delegate Value Function(List<Value> args);

    public class Interpreter
    {
        public event EventHandler<string> Output = delegate { };
        public event EventHandler<ErrorInfo> ErrorOccurred = delegate { };

        public Dictionary<string, Function> Functions { get; private set; } = new Dictionary<string, Function>();
        public Dictionary<string, Value> Vars { get; private set; } = new Dictionary<string, Value>();
        public ErrorInfo LastError { get; private set; }
        public int TotalStatementCount { get; private set; }
        public int TotalTokenCount { get; private set; }

        bool exit;
        Lexer lexer;
        Location statementLocation;
        int nestingLevelOfDeferredSource;
        readonly StringBuilder deferredSource = new StringBuilder();
        readonly Stack<Clause> clauses = new Stack<Clause>();
        readonly Dictionary<TokenType, int> operatorPrecs = new Dictionary<TokenType, int>()
        {
            { TokenType.Exponent, 0 },
            { TokenType.Multiply, 1 }, {TokenType.Division, 1 }, {TokenType.FloorDivision, 1 }, {TokenType.Remainder, 1 },
            { TokenType.Plus, 2 }, { TokenType.Minus, 2 },
            { TokenType.Equal, 3 }, { TokenType.NotEqual, 3 }, { TokenType.Less, 3 }, { TokenType.Greater, 3 }, { TokenType.LessEqual, 3 },  { TokenType.GreaterEqual, 3 },
            { TokenType.Not, 4 },
            { TokenType.And, 5 },
            { TokenType.Or, 6 }
        };

        public Interpreter()
        {
            BuiltinFunction.InstallAll(this);
        }

        public bool BatchRun(string source)
        {
            return this.RunInternal(source);
        }

        public bool InteractiveRun(string source, out bool deferred)
        {
            deferred = true;
            this.deferredSource.AppendLine(source);
            var tokens = Lexer.SplitTokens(source);
            this.nestingLevelOfDeferredSource += tokens.Count(t => t.HasClause());
            this.nestingLevelOfDeferredSource -= tokens.Count(t => t.Type == TokenType.End);
            if (this.nestingLevelOfDeferredSource > 0) return true;

            var result = this.RunInternal(this.deferredSource.ToString());

            this.deferredSource.Clear();
            this.nestingLevelOfDeferredSource = this.clauses.Count;
            deferred = (this.nestingLevelOfDeferredSource > 0);

            return result;
        }

        bool RunInternal(string source)
        {
            var result = false;
            try
            {
                this.exit = false;
                this.lexer = new Lexer(source);
                this.lexer.TokenRead += (s, e) => this.TotalTokenCount++;
                this.lexer.ReadToken();
                while (!this.exit)
                {
                    this.Statement();
                    this.TotalStatementCount++;
                }
                result = true;
            }
            catch (Exception ex)
            {
                var errorType = (ex as InternalErrorException)?.ErrorType ?? ErrorType.UnknownError;
                this.LastError = new ErrorInfo(errorType, ex.Message, this.lexer.Token.Location);
                this.ErrorOccurred(this, this.LastError);
                this.clauses.Clear();
            }
            return result;
        }

        void Statement()
        {
            while (this.lexer.Token.Type == TokenType.NewLine) this.lexer.ReadToken();

            this.statementLocation = this.lexer.Token.Location;
            var type = this.lexer.Token.Type;
            var nextType = this.lexer.NextToken.Type;

            if (type == TokenType.Unkown) throw new InternalErrorException(ErrorType.UnknownToken, $"{this.lexer.Token}");
            else if (type == TokenType.If) this.OnIf();
            else if (type == TokenType.Elif) this.OnAflterIf();
            else if (type == TokenType.Else) this.OnAflterIf();
            else if (type == TokenType.For) this.OnFor();
            else if (type == TokenType.End) this.OnEnd();
            else if (type == TokenType.Exit) this.OnExit();
            else if (type == TokenType.Eof) this.OnEof();
            else if (type == TokenType.Identifer && nextType == TokenType.Assign) this.OnAssign();
            else this.Output(this, this.Expr().ToQuotedString());
        }

        void OnIf()
        {
        Start:
            bool result = true;
            if (this.lexer.Token.Type == TokenType.If || this.lexer.Token.Type == TokenType.Elif)
            {
                if (this.lexer.Token.Type == TokenType.If)
                    this.clauses.Push(new Clause(TokenType.If, this.statementLocation, null));
                this.lexer.ReadToken();
                result = this.Expr().Boolean();
            }
            else if (this.lexer.Token.Type == TokenType.Else)
            {
                this.lexer.ReadToken();
            }
            else
            {
                throw new InternalErrorException(ErrorType.UnexpectedToken, $"{this.lexer.Token}");
            }

            this.VerifyToken(this.lexer.Token, TokenType.Colon);
            this.DebugLog($"{this.lexer.Token.Location.Line + 1}: {this.lexer.Token} {result}");

            if (!result)
            {
                int count = 0;
                while (this.lexer.ReadToken().Type != TokenType.Eof)
                {
                    if (this.lexer.Token.HasClause())
                    {
                        count++;
                    }
                    else if (this.lexer.Token.Type == TokenType.Elif || this.lexer.Token.Type == TokenType.Else)
                    {
                        if (count == 0) goto Start;
                    }
                    else if (this.lexer.Token.Type == TokenType.End)
                    {
                        if (count-- == 0) break;
                    }
                }
            }
            else
            {
                this.lexer.ReadToken();
            }
        }

        void OnAflterIf()
        {
            if (this.clauses.Count <= 0 || this.clauses.Peek().Token != TokenType.If)
                throw new InternalErrorException(ErrorType.UnexpectedToken, $"{this.lexer.Token}");

            int count = 0;
            while (this.lexer.ReadToken().Type != TokenType.Eof)
            {
                if (this.lexer.Token.HasClause())
                {
                    count++;
                }
                else if (this.lexer.Token.Type == TokenType.End)
                {
                    if (count-- == 0) break;
                }
            }
        }


        void OnFor()
        {
            this.VerifyToken(this.lexer.ReadToken(), TokenType.Identifer);
            var name = this.lexer.Token.Text;

            this.VerifyToken(this.lexer.ReadToken(), TokenType.Assign);

            this.lexer.ReadToken();
            Value fromValue = this.Expr();
            if (this.clauses.Count == 0 || this.clauses.Peek().Var != name)
            {
                this.Vars[name] = fromValue;
                this.clauses.Push(new Clause(TokenType.For, this.statementLocation, name));
            }

            this.VerifyToken(this.lexer.Token, TokenType.To);

            this.lexer.ReadToken();
            var toValue = this.Expr();

            this.VerifyToken(this.lexer.Token, TokenType.Colon);

            this.DebugLog($"{this.lexer.Token.Location.Line + 1}: For {this.Vars[name]} to {toValue}");

            if (this.Vars[name].BinaryOperation(toValue, TokenType.Greater).Number == 1)
            {
                int counter = 0;
                while (counter >= 0)
                {
                    this.lexer.ReadToken();
                    if (this.lexer.Token.HasClause()) counter++;
                    else if (this.lexer.Token.Type == TokenType.End) counter--;
                }
                this.clauses.Pop();
            }
            this.lexer.ReadToken();
        }

        void OnEnd()
        {
            if (this.clauses.Count <= 0)
                throw new InternalErrorException(ErrorType.UnexpectedToken, $"{TokenType.End}");

            this.DebugLog($"{this.lexer.Token.Location.Line + 1}: End");
            var clause = this.clauses.Peek();
            if (clause.Token == TokenType.If)
                this.EndIf(clause);
            else if (clause.Token == TokenType.For)
                this.EndFor(clause);
            else
                throw new InternalErrorException(ErrorType.UnexpectedToken, $"{clause.Token}");

            this.lexer.ReadToken();
        }

        void EndIf(Clause clause)
        {
            this.clauses.Pop();
        }

        void EndFor(Clause clause)
        {
            this.Vars[clause.Var] = this.Vars[clause.Var].BinaryOperation(new Value(1), TokenType.Plus);
            this.lexer.Move(clause.Location);
        }

        void OnExit()
        {
            this.exit = true;
        }

        void OnEof()
        {
            if (this.clauses.Count > 0) throw new InternalErrorException(ErrorType.MissingToken, $"{TokenType.End}");
            this.exit = true;
        }

        void OnAssign()
        {
            var name = this.lexer.Token.Text;
            this.VerifyToken(this.lexer.ReadToken(), TokenType.Assign);
            this.lexer.ReadToken();
            this.Vars[name] = this.Expr();
            this.DebugLog($"{this.lexer.Token.Location.Line + 1}: Assign {name}={this.Vars[name].ToString()}");
        }

        Value Expr(int lowestPrec = int.MaxValue - 1)
        {
            var lhs = this.Primary();
            this.lexer.ReadToken();
            while (true)
            {
                if (!this.lexer.Token.IsOperator()) break;
                if (!this.operatorPrecs.TryGetValue(this.lexer.Token.Type, out var prec)) prec = int.MaxValue;
                if (prec >= lowestPrec) break;

                var type = this.lexer.Token.Type;
                this.lexer.ReadToken();
                var rhs = this.Expr(prec);
                lhs = lhs.BinaryOperation(rhs, type);
            }

            return lhs;
        }

        Value Primary()
        {
            var primary = Value.Zero;

            if (this.lexer.Token.Type == TokenType.Value)
            {
                primary = this.lexer.Token.Value;
            }
            else if (this.lexer.Token.Type == TokenType.Plus || this.lexer.Token.Type == TokenType.Minus)
            {
                var type = this.lexer.Token.Type;
                this.lexer.ReadToken();
                primary = Value.Zero.BinaryOperation(this.Primary(), type); // we dont realy have a unary operators
            }
            else if (this.lexer.Token.Type == TokenType.Not)
            {
                this.lexer.ReadToken();
                primary = new Value(this.Primary().Boolean() ? 0.0 : 1.0);
            }
            else if (this.lexer.Token.Type == TokenType.LeftParen)
            {
                this.lexer.ReadToken();
                primary = this.Expr();
                this.VerifyToken(this.lexer.Token, TokenType.RightParen);
            }
            else if (this.lexer.Token.Type == TokenType.Identifer)
            {
                var identifier = this.lexer.Token.Text;
                if (this.Vars.ContainsKey(identifier))
                {
                    primary = this.Vars[identifier];
                }
                else if (this.Functions.ContainsKey(identifier))
                {
                    var name = identifier;
                    this.VerifyToken(this.lexer.ReadToken(), TokenType.LeftParen);
                    var args = this.ReadArguments();
                    primary = this.Functions[name](args);
                }
                else
                {
                    throw new InternalErrorException(ErrorType.UndeclaredIdentifier, $"{identifier}");
                }
            }
            else
            {
                throw new InternalErrorException(ErrorType.UnexpectedToken, $"{this.lexer.Token}");
            }

            return primary;
        }

        void VerifyToken(Token token, TokenType expectedType)
        {
            if (token.Type != expectedType) throw new InternalErrorException(ErrorType.MissingToken, $"{expectedType}");
        }

        List<Value> ReadArguments()
        {
            var args = new List<Value>();
            while (true)
            {
                if (this.lexer.ReadToken().Type != TokenType.RightParen)
                {
                    args.Add(this.Expr());
                    if (this.lexer.Token.Type == TokenType.Comma) continue;
                }
                break;
            }
            return args;
        }

        void DebugLog(string s)
        {
            //Debug.WriteLine(s);
        }
    }

    public struct ErrorInfo
    {
        public ErrorType Type { get; private set; }
        public string Message { get; private set; }
        public int LineNo { get; private set; }
        public int ColumnNo { get; private set; }

        internal ErrorInfo(ErrorType type, string message, Location location)
        {
            this.Type = type;
            this.Message = message;
            this.LineNo = location.Line + 1;
            this.ColumnNo = location.Column + 1;
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(this.Message) ? $"{this.Message} at line:{this.LineNo} column:{this.ColumnNo}" : string.Empty;
        }
    }

    public struct Value
    {
        public static readonly Value Zero = new Value(0);

        public ValueType Type { get; private set; }
        public double Number { get; private set; }
        public string String { get; private set; }

        public Value(double n) : this()
        {
            this.Type = ValueType.Number;
            this.Number = n;
        }

        public Value(string s) : this()
        {
            this.Type = ValueType.String;
            this.String = s;
        }

        public override string ToString()
        {
            return (this.Type == ValueType.Number) ? this.Number.ToString() : this.String;
        }

        public string ToQuotedString()
        {
            return (this.Type == ValueType.Number) ? this.Number.ToString() : $"'{this.String}'";
        }

        internal bool Boolean()
        {
            return (this.Type == ValueType.Number) ? (this.Number != 0.0) : (this.String != string.Empty);
        }

        internal Value BinaryOperation(Value b, TokenType tokenType)
        {
            var a = this;

            if (tokenType == TokenType.Multiply)
            {
                return
                    (a.Type == ValueType.Number && b.Type == ValueType.Number) ?
                        new Value(a.Number * b.Number) :
                    (a.Type == ValueType.String && b.Type == ValueType.Number) ?
                        new Value((new StringBuilder().Insert(0, a.String, (int)Math.Max(b.Number, 0.0))).ToString()) :
                    (a.Type == ValueType.Number && b.Type == ValueType.String) ?
                        new Value((new StringBuilder().Insert(0, b.String, (int)Math.Max(a.Number, 0.0))).ToString()) :
                    throw new InternalErrorException(ErrorType.NotSupportedOperation, $"{tokenType} for {a.Type}");
            }

            if (a.Type != b.Type)
            {
                if (a.Type == ValueType.Number && b.Type == ValueType.String)
                    a = new Value(a.ToString());
                else if (a.Type == ValueType.String && b.Type == ValueType.Number)
                    b = new Value(b.ToString());
                else
                    throw new InternalErrorException(ErrorType.InvalidDataType, $"{a.Type} x {b.Type}");
            }

            if (tokenType == TokenType.Plus)
            {
                return
                    (a.Type == ValueType.Number) ? new Value(a.Number + b.Number) :
                    (a.Type == ValueType.String) ? new Value(a.String + b.String) :
                    throw new InternalErrorException(ErrorType.NotSupportedOperation, $"{tokenType} for {a.Type}");
            }
            else if (tokenType == TokenType.Equal)
            {
                return
                    (a.Type == ValueType.Number) ? new Value(a.Number == b.Number ? 1 : 0) :
                    (a.Type == ValueType.String) ? new Value(a.String == b.String ? 1 : 0) :
                    throw new InternalErrorException(ErrorType.NotSupportedOperation, $"{tokenType} for {a.Type}");
            }
            else if (tokenType == TokenType.NotEqual)
            {
                return
                    (a.Type == ValueType.Number) ? new Value(a.Number != b.Number ? 1 : 0) :
                    (a.Type == ValueType.String) ? new Value(a.String != b.String ? 1 : 0) :
                    throw new InternalErrorException(ErrorType.NotSupportedOperation, $"{tokenType} for {a.Type}");
            }
            else
            {
                if (a.Type != ValueType.Number) throw new InternalErrorException(ErrorType.NotSupportedOperation, $"{tokenType} for {a.Type}");

                return
                    (tokenType == TokenType.Minus) ? new Value(a.Number - b.Number) :
                    (tokenType == TokenType.Division) ? new Value(a.Number / b.Number) :
                    (tokenType == TokenType.FloorDivision) ? new Value(Math.Floor(a.Number / b.Number)) :
                    (tokenType == TokenType.Remainder) ? new Value(a.Number % b.Number) :
                    (tokenType == TokenType.Exponent) ? new Value(Math.Pow(a.Number, b.Number)) :
                    (tokenType == TokenType.Less) ? new Value(a.Number < b.Number ? 1 : 0) :
                    (tokenType == TokenType.Greater) ? new Value(a.Number > b.Number ? 1 : 0) :
                    (tokenType == TokenType.LessEqual) ? new Value(a.Number <= b.Number ? 1 : 0) :
                    (tokenType == TokenType.GreaterEqual) ? new Value(a.Number >= b.Number ? 1 : 0) :
                    (tokenType == TokenType.And) ? new Value(a.Number != 0.0 && b.Number != 0.0 ? 1 : 0) :
                    (tokenType == TokenType.Or) ? new Value(a.Number != 0.0 || b.Number != 0.0 ? 1 : 0) :
                    throw new InternalErrorException(ErrorType.UnknownOperator, $"{tokenType}");
            }
        }
    }

    class InternalErrorException : Exception
    {
        public ErrorType ErrorType { get; private set; }

        public InternalErrorException(ErrorType type, string message) : base($"{type}:{message}")
        {
            this.ErrorType = type;
        }
        public InternalErrorException() : base() { }
        public InternalErrorException(string message) : base(message) { }
        public InternalErrorException(string message, Exception inner) : base(message, inner) { }
    }

    class BuiltinFunction
    {
        public static void InstallAll(Interpreter interpreter)
        {
            interpreter.Functions["str"] = Str;
            interpreter.Functions["int"] = Int;
            interpreter.Functions["float"] = Float;
            interpreter.Functions["abs"] = Abs;
            interpreter.Functions["min"] = Min;
            interpreter.Functions["max"] = Max;
        }

        public static Value Str(List<Value> args)
        {
            if (args.Count != 1) throw new ArgumentException("InvalidNumberOfArguments");
            return new Value(args[0].ToString());
        }

        public static Value Int(List<Value> args)
        {
            if (args.Count != 1) throw new ArgumentException("InvalidNumberOfArguments");
            var value = args[0];
            return
                (value.Type == ValueType.String) ? new Value(long.Parse(value.String)) :
                (value.Type == ValueType.Number) ? new Value((long)value.Number) :
                throw new ArgumentException("InvalidDataType");
        }

        public static Value Float(List<Value> args)
        {
            if (args.Count != 1) throw new ArgumentException("InvalidNumberOfArguments");
            var value = args[0];
            return
                (value.Type == ValueType.String) ? new Value(double.Parse(value.String)) :
                (value.Type == ValueType.Number) ? new Value(value.Number) :
                throw new ArgumentException("InvalidDataType");
        }

        public static Value Abs(List<Value> args)
        {
            if (args.Count != 1) throw new ArgumentException("InvalidNumberOfArguments");
            if(args[0].Type != ValueType.Number) throw new ArgumentException("InvalidDataType");
            return new Value(Math.Abs(args[0].Number));
        }

        public static Value Min(List<Value> args)
        {
            if (args.Count < 2) throw new ArgumentException("InvalidNumberOfArguments");
            var min = double.MaxValue;
            foreach (var arg in args)
            {
                if (arg.Type != ValueType.Number) throw new ArgumentException("InvalidDataType");
                min = Math.Min(min, arg.Number);
            }
            return new Value(min);
        }

        public static Value Max(List<Value> args)
        {
            if (args.Count < 2) throw new ArgumentException("InvalidNumberOfArguments");
            var max = double.MinValue;
            foreach (var arg in args)
            {
                if (arg.Type != ValueType.Number) throw new ArgumentException("InvalidDataType");
                max = Math.Max(max, arg.Number);
            }
            return new Value(max);
        }
    }

    struct Token
    {
        public static Token None = new Token() { Type = TokenType.None };

        public TokenType Type { get; internal set; }
        public Location Location { get; internal set; }
        public string Text { get; internal set; }
        public Value Value { get; internal set; }

        public bool HasClause()
        {
            return this.Type == TokenType.If || this.Type == TokenType.For;
        }

        public bool IsOperator()
        {
            return TokenType.OperatorBegin <= this.Type && this.Type <= TokenType.OperatorEnd;
        }

        public override string ToString()
        {
            return $"'{this.Text.Replace("\n", "\\n")}'({this.Type})";
        }
    }

    class Lexer
    {
        public event EventHandler<Token> TokenRead = delegate { };

        public Token Token { get; private set; }
        public Token NextToken { get; private set; }

        string source;
        Location currentLocation;
        char currentChar;
        char nextChar;

        public Lexer(string input)
        {
            this.source = input;
            this.Move(new Location());
        }

        Lexer() { }

        public static List<Token> SplitTokens(string input)
        {
            var tokens = new List<Token>();
            var lexer = new Lexer(input);
            while (lexer.ReadToken().Type != TokenType.Eof)
                tokens.Add(lexer.Token);
            return tokens;
        }

        public void Move(Location location)
        {
            this.currentLocation = location;
            this.currentChar = this.GetCharAt(location.CharIndex);
            this.nextChar = this.GetCharAt(location.CharIndex + 1);
            this.Token = Token.None;
            this.NextToken = Token.None;
        }

        public Token ReadToken()
        {
            if (this.NextToken.Type == TokenType.None)
                this.NextToken = this.ReadTokenInternal();
            this.Token = this.NextToken;
            this.NextToken = this.ReadTokenInternal();
            return this.Token;
        }

        Token ReadTokenInternal()
        {
            while (this.currentChar != '\n' && char.IsWhiteSpace(this.currentChar))
                this.ReadChar();

            Token token;
            if (this.currentChar == (char)0) token = new Token() { Type = TokenType.Eof };
            else if (this.currentChar == '#') token = this.ReadComment();
            else if (this.IsLetterOrUnderscore(this.currentChar)) token = this.ReadIdentifier();
            else if (char.IsDigit(this.currentChar)) token = this.ReadNumber();
            else if (this.IsStringEnclosure(this.currentChar)) token = this.ReadString(this.currentChar);
            else token = this.ReadOperator();

            this.TokenRead(this, token);
            return token;
        }

        Token ReadComment()
        {
            var location = this.currentLocation;
            this.ReadChar();
            while (this.currentChar != '\n' && this.currentChar != (char)0) this.ReadChar();
            var type = (this.currentChar == '\n') ? TokenType.NewLine : TokenType.Eof;
            this.ReadChar();
            return new Token() { Type = type, Location = location };
        }

        Token ReadIdentifier()
        {
            var location = this.currentLocation;
            var identifier = this.currentChar.ToString();
            while (this.IsLetterOrDigitOrUnderscore(this.ReadChar())) identifier += this.currentChar;
            var type =
                (identifier == "if") ? TokenType.If :
                (identifier == "elif") ? TokenType.Elif :
                (identifier == "else") ? TokenType.Else :
                (identifier == "end") ? TokenType.End :
                (identifier == "for") ? TokenType.For :
                (identifier == "to") ? TokenType.To :
                (identifier == "exit") ? TokenType.Exit :
                (identifier == "and") ? TokenType.And :
                (identifier == "or") ? TokenType.Or :
                (identifier == "not") ? TokenType.Not :
                TokenType.Identifer;
            return new Token() { Type = type, Location = location, Text = identifier };
        }

        Token ReadNumber()
        {
            var location = this.currentLocation;
            var sb = new StringBuilder();
            while (char.IsDigit(this.currentChar) || this.currentChar == '.')
            {
                sb.Append(this.currentChar);
                this.ReadChar();
            }
            var s = sb.ToString();
            if (!double.TryParse(s, out var n))
                throw new InternalErrorException(ErrorType.InvalidNumberFormat, "{sb}");
            return new Token() { Type = TokenType.Value, Location = location, Text = s, Value = new Value(n) };
        }

        Token ReadString(char enclosure)
        {
            var location = this.currentLocation;
            var sb = new StringBuilder();
            while (this.ReadChar() != enclosure)
            {
                if (this.currentChar == (char)0) throw new InternalErrorException(ErrorType.InvalidStringLiteral, "EOF while scanning string literal");
                if (this.currentChar == '\n') throw new InternalErrorException(ErrorType.InvalidStringLiteral, "NewLine while scanning string literal");
                if (this.currentChar == '\\')
                {
                    // Escape sequence
                    var c = char.ToLower(this.ReadChar());
                    if (c == 'n') sb.Append('\n');
                    else if (c == 'r') sb.Append('\r');
                    else if (c == 't') sb.Append('\t');
                    else if (c == '\\') sb.Append('\\');
                    else if (c == enclosure) sb.Append(enclosure);
                    else sb.Append(c);
                }
                else
                {
                    sb.Append(this.currentChar);
                }
            }
            this.ReadChar();
            var s = sb.ToString();
            return new Token() { Type = TokenType.Value, Location = location, Text = s, Value = new Value(s) };
        }

        Token ReadOperator()
        {
            var location = this.currentLocation;
            var index = this.currentLocation.CharIndex;
            TokenType type;
            if (this.currentChar == '\n') type = TokenType.NewLine;
            else if (this.currentChar == ':') type = TokenType.Colon;
            else if (this.currentChar == ',') type = TokenType.Comma;
            else if (this.currentChar == '=' && this.nextChar == '=') { type = TokenType.Equal; this.ReadChar(); }
            else if (this.currentChar == '=') type = TokenType.Assign;
            else if (this.currentChar == '!' && this.nextChar == '=') { type = TokenType.NotEqual; this.ReadChar(); }
            else if (this.currentChar == '+') type = TokenType.Plus;
            else if (this.currentChar == '-') type = TokenType.Minus;
            else if (this.currentChar == '*' && this.nextChar == '*') { type = TokenType.Exponent; this.ReadChar(); }
            else if (this.currentChar == '*') type = TokenType.Multiply;
            else if (this.currentChar == '/' && this.nextChar == '/') { type = TokenType.FloorDivision; this.ReadChar(); }
            else if (this.currentChar == '/') type = TokenType.Division;
            else if (this.currentChar == '%') type = TokenType.Remainder;
            else if (this.currentChar == '(') type = TokenType.LeftParen;
            else if (this.currentChar == ')') type = TokenType.RightParen;
            else if (this.currentChar == '<' && this.nextChar == '=') { type = TokenType.LessEqual; this.ReadChar(); }
            else if (this.currentChar == '<') type = TokenType.Less;
            else if (this.currentChar == '>' && this.nextChar == '=') { type = TokenType.GreaterEqual; this.ReadChar(); }
            else if (this.currentChar == '>') type = TokenType.Greater;
            else type = TokenType.Unkown;
            this.ReadChar();
            return new Token() { Type = type, Location = location, Text = this.source.Substring(index, this.currentLocation.CharIndex - index) };
        }

        char ReadChar()
        {
            this.AdvanceLocation();
            this.currentChar = this.nextChar;
            this.nextChar = this.GetCharAt(this.currentLocation.CharIndex + 1);
            return this.currentChar;
        }

        void AdvanceLocation()
        {
            if (this.currentChar == '\n')
            {
                this.currentLocation.Column = 0;
                this.currentLocation.Line++;
            }
            else
            {
                this.currentLocation.Column++;
            }
            this.currentLocation.CharIndex++;
        }

        char GetCharAt(int index)
        {
            return (0 <= index && index < this.source.Length) ? this.source[index] : (char)0;
        }

        bool IsLetterOrUnderscore(char c)
        {
            return char.IsLetter(c) || c == '_';
        }

        bool IsLetterOrDigitOrUnderscore(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        bool IsStringEnclosure(char c)
        {
            return c == '"' || c == '\'';
        }
    }

    /// <summary>
    /// The location in source code.
    /// </summary>
    struct Location
    {
        public int CharIndex { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    struct Clause
    {
        public TokenType Token;
        public Location Location;
        public string Var;

        public Clause(TokenType token, Location location, string var)
        {
            this.Token = token;
            this.Location = location;
            this.Var = var;
        }
    }

    enum TokenType
    {
        None, Unkown,

        Identifer, Value,

        // Statement keyword
        If, Elif, Else, For, To, End, Exit,

        // Symbol
        NewLine, Colon, Comma, Assign, LeftParen, RightParen,

        OperatorBegin,

        // Arithmetic operator
        Plus, Minus, Multiply, Division, FloorDivision, Remainder, Exponent,

        // Comparison operator
        Equal, Less, Greater, NotEqual, LessEqual, GreaterEqual,

        // Logical operator
        Or, And, Not,

        OperatorEnd,

        Eof = -1
    }
}
