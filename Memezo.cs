// MemezoScript - Embeded scripting environment.
// Based on https://github.com/Timu5/BasicSharp

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Suconbu.Scripting.Memezo
{
    public enum ValueType { Number, String }

    public class Interpreter
    {
        public delegate Value FunctionHandler(List<Value> args);
        public event EventHandler<string> PrintValue = delegate { };

        public Dictionary<string, Value> Vars { get; private set; } = new Dictionary<string, Value>();
        public ErrorInfo Error { get; private set; }
        public int DeferedClauseCount { get; private set; }
        public int TotalStatementCount { get; private set; }
        public int TotalTokenCount { get { return this.lexer.TotalTokenCount; } }

        Lexer lexer;
        Location statementLocation;
        bool exit;
        Value returnValue;

        readonly Stack<Clause> clauses = new Stack<Clause>();
        readonly Dictionary<string, FunctionHandler> functions = new Dictionary<string, FunctionHandler>();
        readonly Dictionary<Token, int> operatorProcs = new Dictionary<Token, int>()
        {
            { Token.Exponent, 0 },
            { Token.Multiply, 1 }, {Token.Division, 1 }, {Token.FloorDivision, 1 }, {Token.Remainder, 1 },
            { Token.Plus, 2 }, { Token.Minus, 2 },
            { Token.Equal, 3 }, { Token.NotEqual, 3 }, { Token.Less, 3 }, { Token.More, 3 }, { Token.LessEqual, 3 },  { Token.MoreEqual, 3 },
            { Token.Not, 4 },
            { Token.And, 5 },
            { Token.Or, 6 }
        };

        public Interpreter()
        {
            BuiltinFunction.InstallAll(this);
        }

        public void AddFunction(string name, FunctionHandler function)
        {
            this.functions[name] = function;
        }

        public bool Run(string input)
        {
            var result = false;
            try
            {
                this.Initialize();
                this.lexer = new Lexer(input);
                this.lexer.ReadToken();
                while (!this.exit) this.Statement();
                result = true;
            }
            catch (Exception ex)
            {
                this.Error = new ErrorInfo(ex.Message, this.lexer.TokenLocation);
            }
            return result;
        }

        public bool RunAsInteractive(string input)
        {
            if (this.lexer == null)
            {
                this.Initialize();
                this.lexer = new Lexer(input + "\n");
            }
            else
            {
                this.lexer.AddSource(input + "\n");
            }

            if (Regex.IsMatch(input, @"\b(if|for)\b.*:")) this.DeferedClauseCount++;
            else if (input.Trim() == "end") this.DeferedClauseCount--;
            if (this.DeferedClauseCount > 0) return true;

            var clauseCount = this.clauses.Count;
            var result = false;
            try
            {
                this.exit = false;
                this.lexer.ReadToken();
                while (!this.exit) this.Statement();
                result = true;
            }
            catch (Exception ex)
            {
                this.Error = new ErrorInfo(ex.Message, this.lexer.TokenLocation);
                while (this.clauses.Count > clauseCount) this.clauses.Pop();
            }
            // Consider to case of return in the middle.
            while (this.lexer.ReadToken() != Token.EOF) ;
            return result;
        }

        void Initialize()
        {
            this.returnValue = Value.Zero;
            this.exit = false;
            this.TotalStatementCount = 0;
            this.clauses.Clear();
        }

        void Statement()
        {
            this.TotalStatementCount++;
            while (this.lexer.Token == Token.Unkown || this.lexer.Token == Token.NewLine)
                this.lexer.ReadToken();
            var keyword = this.lexer.Token;
            this.statementLocation = this.lexer.TokenLocation;
            //Debug.WriteLine($"Statement keyword:{keyword}");
            switch (keyword)
            {
                case Token.Print: this.Print(); break;
                case Token.If: this.If(); break;
                case Token.Elif: this.IfSkip(); break;
                case Token.Else: this.IfSkip(); break;
                case Token.For: this.For(); break;
                case Token.End: this.End(); break;
                case Token.Return: this.Return(); break;
                case Token.Identifer: this.Identifier(); break;
                case Token.EOF: this.Eof(); break;
                default: this.RiseError($"UnexpectedToken: {keyword}"); break;
            }
        }

        void Print()
        {
            this.lexer.ReadToken();
            this.PrintValue(this, this.Expr().ToString());
        }

        void If()
        {
        Start:
            bool result = true;
            if (this.lexer.Token == Token.If || this.lexer.Token == Token.Elif)
            {
                if (this.lexer.Token == Token.If)
                    this.clauses.Push(new Clause(Token.If, this.statementLocation, null));
                this.lexer.ReadToken();
                result = (this.Expr().BinaryOperation(Value.Zero, Token.Equal).Number == 0);
            }
            else if (this.lexer.Token == Token.Else)
            {
                this.lexer.ReadToken();
            }
            else
                this.RiseError($"UnexpectedToken: {this.lexer.Token}");
            this.VerifyToken(this.lexer.Token, Token.Colon);
            Debug.WriteLine($"{this.lexer.TokenLocation.Line + 1}: {this.lexer.Token} {result}");

            if (!result)
            {
                // Condition is not satisfied.
                int count = 0;
                while (this.lexer.ReadToken() != Token.EOF)
                {
                    if (this.HasClause(this.lexer.Token))
                    {
                        count++;
                    }
                    else if (this.lexer.Token == Token.Elif || this.lexer.Token == Token.Else)
                    {
                        if (count == 0) goto Start;
                    }
                    else if (this.lexer.Token == Token.End)
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

        void IfSkip()
        {
            // After if clause executed.
            if (this.clauses.Count <= 0 || this.clauses.Peek().Token != Token.If)
                this.RiseError($"UnexpectedToken: {this.lexer.Token}");
            int count = 0;
            while (this.lexer.ReadToken() != Token.EOF)
            {
                if (this.HasClause(this.lexer.Token))
                {
                    count++;
                }
                else if (this.lexer.Token == Token.End)
                {
                    if (count-- == 0) break;
                }
            }
        }


        void For()
        {
            this.VerifyToken(this.lexer.ReadToken(), Token.Identifer);
            var name = this.lexer.Identifer;

            this.VerifyToken(this.lexer.ReadToken(), Token.Assign);

            this.lexer.ReadToken();
            Value fromValue = this.Expr();
            if (this.clauses.Count == 0 || this.clauses.Peek().Var != name)
            {
                this.Vars[name] = fromValue;
                this.clauses.Push(new Clause(Token.For, this.statementLocation, name));
            }

            this.VerifyToken(this.lexer.Token, Token.To);

            this.lexer.ReadToken();
            var toValue = this.Expr();

            this.VerifyToken(this.lexer.Token, Token.Colon);

            Debug.WriteLine($"{this.lexer.TokenLocation.Line + 1}: For {this.Vars[name]} to {toValue}");

            if (this.Vars[name].BinaryOperation(toValue, Token.More).Number == 1)
            {
                int counter = 0;
                while (counter >= 0)
                {
                    this.lexer.ReadToken();
                    if (this.HasClause(this.lexer.Token)) counter++;
                    else if (this.lexer.Token == Token.End) counter--;
                }
                this.clauses.Pop();
            }
            this.lexer.ReadToken();
        }

        void End()
        {
            if (this.clauses.Count <= 0) this.RiseError($"UnexpectedToken: {Token.End}");
            Debug.WriteLine($"{this.lexer.TokenLocation.Line + 1}: End");
            var clause = this.clauses.Peek();
            if (clause.Token == Token.If)
                this.EndIf(clause);
            else if (clause.Token == Token.For)
                this.EndFor(clause);
            else
                this.RiseError($"UnexpectedClauseToken: {clause.Token}");
            this.lexer.ReadToken();
        }

        void EndIf(Clause clause)
        {
            this.clauses.Pop();
        }

        void EndFor(Clause clause)
        {
            this.Vars[clause.Var] = this.Vars[clause.Var].BinaryOperation(new Value(1), Token.Plus);
            this.lexer.Move(clause.Location);
        }

        void Return()
        {
            this.lexer.ReadToken();
            if (this.lexer.Token != Token.NewLine && this.lexer.Token != Token.EOF)
                this.returnValue = this.Expr();
            this.exit = true;
        }

        void Identifier()
        {
            var token = this.lexer.ReadToken();
            if (token == Token.Assign) this.Assign();
            else if (token == Token.LeftParen) this.Invoke();
            else this.RiseError($"UnexpectedIdentifier: {this.lexer.Identifer}");//this.Expr();
        }

        void Eof()
        {
            if (this.clauses.Count > 0) this.RiseError($"MissingToken: {Token.End}");
            this.exit = true;
        }

        void Assign()
        {
            // Current:Token.Assign
            var name = this.lexer.Identifer;
            this.lexer.ReadToken();
            this.Vars[name] = this.Expr();
            Debug.WriteLine($"{this.lexer.TokenLocation.Line + 1}: Assing {name}={this.Vars[name].ToString()}");
        }

        void Invoke()
        {
            // Current:Token.LParen
            var name = this.lexer.Identifer;
            var args = this.ReadArguments();
            this.lexer.ReadToken();
            if (this.functions.TryGetValue(name, out var function))
                function(args);
            else
                this.RiseError($"UndeclaredIdentifier: {name}");
            Debug.WriteLine($"{this.lexer.TokenLocation.Line + 1}: Invoke {name}({string.Join(",",args.ConvertAll(v=>v.ToString()))})");
        }

        Value Expr(int lowestPrec = int.MaxValue - 1)
        {
            var lhs = this.Primary();
            this.lexer.ReadToken();
            while (true)
            {
                if (!this.IsOperator(this.lexer.Token)) break;
                if (!this.operatorProcs.TryGetValue(this.lexer.Token, out var prec)) prec = int.MaxValue;
                if (prec >= lowestPrec) break;

                var op = this.lexer.Token;
                this.lexer.ReadToken();
                var rhs = this.Expr(prec);
                lhs = lhs.BinaryOperation(rhs, op);
            }

            return lhs;
        }

        Value Primary()
        {
            var primary = Value.Zero;

            if (this.lexer.Token == Token.Value)
            {
                primary = this.lexer.Value;
            }
            else if (this.lexer.Token == Token.Identifer)
            {
                if (this.Vars.ContainsKey(this.lexer.Identifer))
                {
                    primary = this.Vars[this.lexer.Identifer];
                }
                else if (this.functions.ContainsKey(this.lexer.Identifer))
                {
                    var name = this.lexer.Identifer;
                    this.VerifyToken(this.lexer.ReadToken(), Token.LeftParen);
                    var args = this.ReadArguments();
                    primary = this.functions[name](args);
                }
                else
                {
                    this.RiseError($"UndeclaredIdentifier: {this.lexer.Identifer}");
                }
            }
            else if (this.lexer.Token == Token.LeftParen)
            {
                this.lexer.ReadToken();
                primary = this.Expr();
                this.VerifyToken(this.lexer.Token, Token.RightParen);
            }
            else if (this.lexer.Token == Token.Plus || this.lexer.Token == Token.Minus)
            {
                var op = this.lexer.Token;
                this.lexer.ReadToken();
                primary = Value.Zero.BinaryOperation(this.Primary(), op); // we dont realy have a unary operators
            }
            else if (this.lexer.Token == Token.Not)
            {
                this.lexer.ReadToken();
                primary = Value.Zero.BinaryOperation(this.Primary(), Token.Equal);
            }
            else
            {
                this.RiseError($"UnexpectedToken: {this.lexer.Token}");
            }

            return primary;
        }

        void VerifyToken(Token token, Token expectedToken)
        {
            if (token != expectedToken) this.RiseError($"MissingToken: {expectedToken}");
        }

        List<Value> ReadArguments()
        {
            var args = new List<Value>();
            while (true)
            {
                if (this.lexer.ReadToken() != Token.RightParen)
                {
                    args.Add(this.Expr());
                    if (this.lexer.Token == Token.Comma) continue;
                }
                break;
            }
            return args;
        }

        bool HasClause(Token token)
        {
            return this.lexer.Token == Token.If || this.lexer.Token == Token.For;
        }

        bool IsOperator(Token token)
        {
            return Token.OperatorBegin <= token && token <= Token.OperatorEnd;
        }

        void RiseError(string message)
        {
            throw new Exception(message);
        }
    }

    public struct ErrorInfo
    {
        public string Message { get; private set; }
        public int LineNo { get; private set; }
        public int ColumnNo { get; private set; }

        internal ErrorInfo(string message, Location location)
        {
            this.Message = message;
            this.LineNo = location.Line + 1;
            this.ColumnNo = location.Column + 1;
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(this.Message) ? $"'{this.Message}' at line:{this.LineNo} column:{this.ColumnNo}" : string.Empty;
        }
    }

    public struct Value
    {
        public static readonly Value Zero = new Value(0);
        public ValueType Type { get; set; }
        public double Number { get; set; }
        public string String { get; set; }

        public Value(double number) : this()
        {
            this.Type = ValueType.Number;
            this.Number = number;
        }

        public Value(string str) : this()
        {
            this.Type = ValueType.String;
            this.String = str;
        }

        public Value Convert(ValueType type)
        {
            if (this.Type != type)
            {
                switch (type)
                {
                    case ValueType.Number:
                        this.Number = double.Parse(this.String);
                        this.Type = ValueType.Number;
                        break;
                    case ValueType.String:
                        this.String = this.Number.ToString();
                        this.Type = ValueType.String;
                        break;
                }
            }
            return this;
        }

        public override string ToString()
        {
            return (this.Type == ValueType.Number) ? this.Number.ToString() : this.String;
        }

        internal Value BinaryOperation(Value b, Token token)
        {
            var a = this;
            if (a.Type != b.Type)
            {
                if (a.Type > b.Type)
                    b = b.Convert(a.Type);
                else
                    a = a.Convert(b.Type);
            }

            if (token == Token.Plus)
            {
                return (a.Type == ValueType.Number) ?
                    new Value(a.Number + b.Number) :
                    new Value(a.String + b.String);
            }
            else if (token == Token.Equal)
            {
                return (a.Type == ValueType.Number) ?
                    new Value(a.Number == b.Number ? 1 : 0) :
                    new Value(a.String == b.String ? 1 : 0);
            }
            else if (token == Token.NotEqual)
            {
                return (a.Type == ValueType.Number) ?
                    new Value(a.Number == b.Number ? 0 : 1) :
                    new Value(a.String == b.String ? 0 : 1);
            }
            else
            {
                if (a.Type == ValueType.String)
                    throw new Exception($"CannotSupportOperationForString: {token}");

                switch (token)
                {
                    case Token.Minus: return new Value(a.Number - b.Number);
                    case Token.Multiply: return new Value(a.Number * b.Number);
                    case Token.Division: return new Value(a.Number / b.Number);
                    case Token.FloorDivision: return new Value(Math.Floor(a.Number / b.Number));
                    case Token.Remainder: return new Value(a.Number % b.Number);
                    case Token.Exponent: return new Value(Math.Pow(a.Number, b.Number));
                    case Token.Less: return new Value(a.Number < b.Number ? 1 : 0);
                    case Token.More: return new Value(a.Number > b.Number ? 1 : 0);
                    case Token.LessEqual: return new Value(a.Number <= b.Number ? 1 : 0);
                    case Token.MoreEqual: return new Value(a.Number >= b.Number ? 1 : 0);
                    case Token.And: return new Value(a.Number != 0.0 && b.Number != 0.0 ? 1 : 0);
                    case Token.Or: return new Value(a.Number != 0.0 || b.Number != 0.0 ? 1 : 0);
                }
            }
            throw new Exception($"UnknownBinaryOperator: {token}");
        }
    }

    class BuiltinFunction
    {
        public static void InstallAll(Interpreter interpreter)
        {
            interpreter.AddFunction("abs", Abs);
            interpreter.AddFunction("min", Min);
            interpreter.AddFunction("max", Max);
        }

        public static Value Abs(List<Value> args)
        {
            if (args.Count != 1) throw new ArgumentException();
            return new Value(Math.Abs(args[0].Number));
        }

        public static Value Min(List<Value> args)
        {
            if (args.Count < 2) throw new ArgumentException();
            var min = double.MaxValue;
            foreach (var arg in args)
                min = Math.Min(min, arg.Number);
            return new Value(min);
        }

        public static Value Max(List<Value> args)
        {
            if (args.Count < 2) throw new ArgumentException();
            var max = double.MinValue;
            foreach (var arg in args)
                max = Math.Max(max, arg.Number);
            return new Value(max);
        }
    }

    /// <summary>
    /// Lexical analyzyer
    /// </summary>
    class Lexer
    {
        public Token Token { get; private set; }
        public Location TokenLocation { get; private set; }
        public string Identifer { get; private set; }
        public Value Value { get; private set; }
        public int TotalTokenCount { get; private set; }

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

        public void AddSource(string input)
        {
            this.source += input;
            this.Move(this.currentLocation);
        }

        public void Move(Location location)
        {
            this.currentLocation = location;
            this.currentChar = this.GetCharAt(location.CharIndex);
            this.nextChar = this.GetCharAt(location.CharIndex + 1);
        }

        public Token ReadToken()
        {
            while (this.currentChar != '\n' && char.IsWhiteSpace(this.currentChar))
                this.ReadChar();

            this.TokenLocation = this.currentLocation;

            var token = Token.Unkown;
            if (this.currentChar == (char)0) token = Token.EOF;
            else if (this.currentChar == '#') token = this.ReadComment();
            else if (this.IsLetterOrUnderscore(this.currentChar)) token = this.ReadIdentifier();
            else if (char.IsDigit(this.currentChar)) token = this.ReadNumber();
            else if (this.IsStringEnclosure(this.currentChar)) token = this.ReadString(this.currentChar);
            else token = this.ReadOperator();
            this.Token = token;
            this.TotalTokenCount++;
            return token;
        }

        Token ReadComment()
        {
            this.ReadChar();
            while (this.currentChar != '\n' && this.currentChar != (char)0) this.ReadChar();
            var token = (this.currentChar == '\n') ? Token.NewLine : Token.EOF;
            this.ReadChar();
            return token;
        }

        Token ReadIdentifier()
        {
            this.Identifer = this.currentChar.ToString();
            while (this.IsLetterOrDigitOrUnderscore(this.ReadChar())) this.Identifer += this.currentChar;
            //Debug.WriteLine($"GetToken Identifier:{this.Identifer}");
            var token = Token.Identifer;
            switch (this.Identifer)
            {
                case "p": token = Token.Print; break;
                case "if": token = Token.If; break;
                case "elif": token = Token.Elif; break;
                case "else": token = Token.Else; break;
                case "end": token = Token.End; break;
                case "for": token = Token.For; break;
                case "to": token = Token.To; break;
                case "return": token = Token.Return; break;
                case "and": token = Token.And; break;
                case "or": token = Token.Or; break;
                case "not": token = Token.Not; break;
            }
            return token;
        }

        Token ReadNumber()
        {
            var s = new StringBuilder();
            while (char.IsDigit(this.currentChar) || this.currentChar == '.')
            {
                s.Append(this.currentChar);
                this.ReadChar();
            } 
            if (!double.TryParse(s.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                throw new Exception($"InvalidNumber: {s}");
            this.Value = new Value(number);
            return Token.Value;
        }

        Token ReadOperator()
        {
            var token = Token.Unkown;
            if (this.currentChar == '\n') token = Token.NewLine;
            else if (this.currentChar == ':') token = Token.Colon;
            else if (this.currentChar == ',') token = Token.Comma;
            else if (this.currentChar == '=' && this.nextChar == '=') { token = Token.Equal; this.ReadChar(); }
            else if (this.currentChar == '=') token = Token.Assign;
            else if (this.currentChar == '!' && this.nextChar == '=') { token = Token.NotEqual; this.ReadChar(); }
            else if (this.currentChar == '+') token = Token.Plus;
            else if (this.currentChar == '-') token = Token.Minus;
            else if (this.currentChar == '*' && this.nextChar == '*') { token = Token.Exponent; this.ReadChar(); }
            else if (this.currentChar == '*') token = Token.Multiply;
            else if (this.currentChar == '/' && this.nextChar == '/') { token = Token.FloorDivision; this.ReadChar(); }
            else if (this.currentChar == '/') token = Token.Division;
            else if (this.currentChar == '%') token = Token.Remainder;
            else if (this.currentChar == '(') token = Token.LeftParen;
            else if (this.currentChar == ')') token = Token.RightParen;
            else if (this.currentChar == '<' && this.nextChar == '=') { token = Token.LessEqual; this.ReadChar(); }
            else if (this.currentChar == '<') token = Token.Less;
            else if (this.currentChar == '>' && this.nextChar == '=') { token = Token.MoreEqual; this.ReadChar(); }
            else if (this.currentChar == '>') token = Token.More;
            else token = Token.Unkown;
            this.ReadChar();
            return token;
        }

        Token ReadString(char enclosure)
        {
            var s = new StringBuilder();
            while (this.ReadChar() != enclosure)
            {
                if (this.currentChar == (char)0) throw new Exception($"InvalidString: {s}");
                if (this.currentChar == '\\')
                {
                    // Escape sequence
                    var c = char.ToLower(this.ReadChar());
                    if (c == 'n') s.Append('\n');
                    else if (c == 'r') s.Append('\r');
                    else if (c == 't') s.Append('\t');
                    else if (c == '\\') s.Append('\\');
                    else if (c == enclosure) s.Append(enclosure);
                    else s.Append(c);
                }
                else
                {
                    s.Append(this.currentChar);
                }
            }
            this.ReadChar();
            this.Value = new Value(s.ToString());
            return Token.Value;
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
        public Token Token;
        public Location Location;
        public string Var;

        public Clause(Token token, Location location, string var)
        {
            this.Token = token;
            this.Location = location;
            this.Var = var;
        }
    }

    enum Token
    {
        Unkown,

        Identifer, Value,

        // Keyword
        Print, If, Elif, Else, For, To, End, Return,

        // Symbol
        NewLine, Colon, Comma, Assign, LeftParen, RightParen,

        OperatorBegin,

        // Arithmetic operator
        Plus, Minus, Multiply, Division, FloorDivision, Remainder, Exponent,

        // Comparison operator
        Equal, Less, More, NotEqual, LessEqual, MoreEqual,

        // Logical operator
        Or, And, Not,

        OperatorEnd,

        EOF = -1
    }
}
