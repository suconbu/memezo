// MemezoScript - The script language for automated operation.
// Based on https://github.com/Timu5/BasicSharp

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Suconbu.Scripting.Memezo
{
    public enum ValueType { Number, String }

    public class Interpreter
    {
        public delegate Value FunctionHandler(List<Value> args);
        public delegate void ActionHandler(List<Value> args);

        public Dictionary<string, Value> Vars { get; private set; } = new Dictionary<string, Value>();
        public ErrorInfo Error { get; private set; }
        public Value? LastResultValue { get; private set; }
        public int TotalStatementCount { get; private set; }
        public int TotalTokenCount { get { return this.lexer.TotalTokenCount; } }

        Lexer lexer;
        //Token currentToken;
        Location statementLocation;
        bool exit;

        readonly Stack<Loop> loops = new Stack<Loop>();
        int ifCount;

        readonly Dictionary<string, FunctionHandler> functions = new Dictionary<string, FunctionHandler>();
        readonly Dictionary<string, ActionHandler> actions = new Dictionary<string, ActionHandler>();

        public Interpreter()
        {
            BuiltinFunction.InstallAll(this);
        }

        public void AddFunction(string name, FunctionHandler function)
        {
            this.functions[name] = function;
        }

        public void AddAction(string name, ActionHandler action)
        {
            this.actions[name] = action;
        }

        public bool Run(string input)
        {
            var result = false;
            try
            {
                this.Initialize();
                this.lexer = new Lexer(input);
                while (!this.exit) this.Statement();
                result = true;
            }
            catch (Exception ex)
            {
                this.Error = new ErrorInfo(ex.Message, this.lexer.TokenLocation);
            }
            return result;
        }

        void Initialize()
        {
            this.exit = false;
            this.LastResultValue = null;
            this.TotalStatementCount = 0;
            this.loops.Clear();
            this.ifCount = 0;
        }

        void Statement()
        {
            this.TotalStatementCount++;

            var keyword = this.lexer.ReadToken();
            this.statementLocation = this.lexer.TokenLocation;
            //Debug.WriteLine($"Statement keyword:{keyword}");
            switch (keyword)
            {
                case Token.Print: this.Print(); break;
                case Token.If: this.If(); break;
                case Token.Elif: this.ElifOrElse(); break;
                case Token.Else: this.ElifOrElse(); break;
                case Token.EndIf: this.EndIf(); break;
                case Token.For: this.For(); break;
                case Token.EndFor: this.EndFor(); break;
                case Token.Exit: this.Exit(); break;
                case Token.Identifer: this.Identifier(); break;
                case Token.NewLine: break;
                case Token.EOF: this.Eof(); break;
                default: this.RiseError($"UnexpectedToken: {keyword}"); break;
            }
        }

        void RiseError(string message)
        {
            throw new Exception(message);
        }

        void VerifyToken(Token expectedToken)
        {
            if (this.lexer.CurrentToken != expectedToken) this.RiseError($"MissingToken: {expectedToken}");
        }

        //Token ReadToken(Token expectedToken)
        //{
        //    var token = this.ReadToken();
        //    if (token != expectedToken) this.RiseError($"MissingToken: {expectedToken}");
        //    return token;
        //}

        //Token ReadToken()
        //{
        //    this.TotalTokenCount++;
        //    this.currentToken = this.lex.ReadToken();
        //    return this.currentToken;
        //}

        void If()
        {
            this.lexer.ReadToken();
            var result = (this.Expr().BinaryOperation(Value.Zero, Token.Equal).Number == 0);
            this.VerifyToken(Token.Colon);
            Debug.WriteLine($"{this.lexer.CurrentLocation.Line + 1}: If {result}");
            if (!result)
            {
                // Condition is not satisfied.
                int count = this.ifCount;
                while (this.lexer.ReadToken() != Token.EOF)
                {
                    if (this.lexer.CurrentToken == Token.If)
                    {
                        count++;
                    }
                    else if (this.lexer.CurrentToken == Token.Elif)
                    {
                        if (count == this.ifCount)
                        {
                            this.If();
                            break;
                        }
                    }
                    else if (this.lexer.CurrentToken == Token.Else)
                    {
                        if (count == this.ifCount)
                        {
                            this.lexer.ReadToken();
                            this.VerifyToken(Token.Colon);
                            this.ifCount++;
                            break;
                        }
                    }
                    else if (this.lexer.CurrentToken == Token.EndIf)
                    {
                        if (count-- == this.ifCount) break;
                    }
                }
            }
            else
            {
                this.ifCount++;
            }
        }

        void ElifOrElse()
        {
            // After if clause executed.
            if (this.ifCount <= 0) this.RiseError("UnexpectedToken: Else/Elif");
            int count = this.ifCount;
            while (this.lexer.ReadToken() != Token.EOF)
            {
                if (this.lexer.CurrentToken == Token.If)
                {
                    count++;
                }
                else if (this.lexer.CurrentToken == Token.EndIf)
                {
                    if (count-- == this.ifCount) break;
                }
            }
            this.ifCount--;
        }

        void EndIf()
        {
            if (--this.ifCount < 0) this.RiseError("UnexpectedEndIf");
        }

        void Exit()
        {
            this.exit = true;
        }

        void Identifier()
        {
            var token = this.lexer.ReadToken();
            if (token == Token.Assign) this.Assign();
            else if (token == Token.LeftParen) this.Invoke();
            else this.RiseError($"UnexpectedIdentifier: {this.lexer.Identifer}");//this.Expr();
        }

        void Print()
        {
            this.lexer.ReadToken();
            this.LastResultValue = this.Expr();
        }

        void Eof()
        {
            if (this.loops.Count > 0) this.RiseError("MissingEndFor");
            if (this.ifCount > 0) this.RiseError("MissingEndIf");
            this.exit = true;
        }

        void Assign()
        {
            // Current:Token.Assign
            var name = this.lexer.Identifer;
            this.lexer.ReadToken();
            this.Vars[name] = this.Expr();
            Debug.WriteLine($"{this.lexer.CurrentLocation.Line + 1}: Assing {name}={this.Vars[name].ToString()}");
        }

        void Invoke()
        {
            // Current:Token.LParen
            var name = this.lexer.Identifer;
            List<Value> args = new List<Value>();
            while (true)
            {
                if (this.lexer.ReadToken() != Token.RightParen)
                {
                    args.Add(this.Expr());
                    if (this.lexer.CurrentToken == Token.Comma) continue;
                }
                break;
            }
            this.VerifyToken(Token.RightParen);
            if (this.functions.TryGetValue(name, out var function)) this.LastResultValue = function(args);
            else if (this.actions.TryGetValue(name, out var action)) action(args);
            else this.RiseError($"UndeclaredIdentifier: {name}");
            Debug.WriteLine($"{this.lexer.CurrentLocation.Line + 1}: Invoke {name}({string.Join(",",args.ConvertAll(v=>v.ToString()))})");
        }

        void For()
        {
            this.lexer.ReadToken();
            this.VerifyToken(Token.Identifer);
            var name = this.lexer.Identifer;

            this.lexer.ReadToken();
            this.VerifyToken(Token.Assign);

            this.lexer.ReadToken();
            Value fromValue = this.Expr();

            if (this.loops.Count == 0 || this.loops.Peek().Var != name)
            {
                this.Vars[name] = fromValue;
                this.loops.Push(new Loop(this.statementLocation, name));
            }

            this.VerifyToken(Token.To);

            this.lexer.ReadToken();
            var toValue = this.Expr();

            this.VerifyToken(Token.Colon);

            Debug.WriteLine($"{this.lexer.CurrentLocation.Line + 1}: For {this.Vars[name]} to {toValue}");

            if (this.Vars[name].BinaryOperation(toValue, Token.More).Number == 1)
            {
                int counter = 0;
                while (counter >= 0)
                {
                    this.lexer.ReadToken();
                    if (this.lexer.CurrentToken == Token.For) counter++;
                    else if (this.lexer.CurrentToken == Token.EndFor) counter--;
                }
                this.loops.Pop();
            }
        }

        void EndFor()
        {
            if(this.loops.Count <= 0) this.RiseError($"UnexpectedEndFor");
            Debug.WriteLine($"{this.lexer.CurrentLocation.Line + 1}: EndFor");

            var loop = this.loops.Peek();
            this.Vars[loop.Var] = this.Vars[loop.Var].BinaryOperation(new Value(1), Token.Plus);
            this.lexer.Move(loop.Location);
        }

        Value Expr(int minPrec = 0)
        {
            Dictionary<Token, int> precs = new Dictionary<Token, int>()
            {
                { Token.Or, 0 }, { Token.And, 0 },
                { Token.Equal, 1 }, { Token.NotEqual, 1 },
                { Token.Less, 1 }, { Token.More, 1 },
                { Token.LessEqual, 1 },  { Token.MoreEqual, 1 },
                { Token.Plus, 2 }, { Token.Minus, 2 },
                { Token.Asterisk, 3 }, {Token.Slash, 3 },
                { Token.Caret, 4 }
            };

            var lhs = this.Primary();
            this.lexer.ReadToken();
            while (true)
            {
                if (!this.IsOperator(this.lexer.CurrentToken)) break;
                if (!precs.TryGetValue(this.lexer.CurrentToken, out var prec)) prec = -1;
                if (prec < minPrec) break;

                var op = this.lexer.CurrentToken;
                int assoc = 0; // 0 left, 1 right
                int nextMinPrec = assoc == 0 ? prec : prec + 1;
                this.lexer.ReadToken();
                var rhs = this.Expr(nextMinPrec);
                lhs = lhs.BinaryOperation(rhs, op);
            }

            return lhs;
        }

        Value Primary()
        {
            var primary = Value.Zero;

            if (this.lexer.CurrentToken == Token.Value)
            {
                primary = this.lexer.Value;
            }
            else if (this.lexer.CurrentToken == Token.Identifer)
            {
                if (this.Vars.ContainsKey(this.lexer.Identifer))
                {
                    primary = this.Vars[this.lexer.Identifer];
                }
                else if (this.functions.ContainsKey(this.lexer.Identifer))
                {
                    var name = this.lexer.Identifer;
                    this.lexer.ReadToken();
                    this.VerifyToken(Token.LeftParen);

                    var args = new List<Value>();
                    while (true)
                    {
                        if (this.lexer.ReadToken() != Token.RightParen)
                        {
                            args.Add(this.Expr());
                            if (this.lexer.CurrentToken == Token.Comma) continue;
                        }
                        break;
                    }
                    primary = this.functions[name](args);
                }
                else
                {
                    this.RiseError($"UndeclaredIdentifier: {this.lexer.Identifer}");
                }
            }
            else if (this.lexer.CurrentToken == Token.LeftParen)
            {
                this.lexer.ReadToken();
                primary = this.Expr();
                this.VerifyToken(Token.RightParen);
            }
            else if (this.lexer.CurrentToken == Token.Plus || this.lexer.CurrentToken == Token.Minus)
            {
                var op = this.lexer.CurrentToken;
                this.lexer.ReadToken();
                primary = Value.Zero.BinaryOperation(this.Primary(), op); // we dont realy have a unary operators
            }
            else
            {
                this.RiseError($"UnexpectedToken: {this.lexer.CurrentToken}");
            }

            return primary;
        }

        bool IsOperator(Token token)
        {
            return Token.Plus <= token && token <= Token.And;
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
                    case Token.Asterisk: return new Value(a.Number * b.Number);
                    case Token.Slash: return new Value(a.Number / b.Number);
                    case Token.Caret: return new Value(Math.Pow(a.Number, b.Number));
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
            interpreter.AddFunction("str", Str);
            interpreter.AddFunction("num", Num);
            interpreter.AddFunction("abs", Abs);
            interpreter.AddFunction("min", Min);
            interpreter.AddFunction("max", Max);
            interpreter.AddFunction("not", Not);
        }

        public static Value Str(List<Value> args)
        {
            if (args.Count < 1) throw new ArgumentException();
            return args[0].Convert(ValueType.String);
        }

        public static Value Num(List<Value> args)
        {
            if (args.Count < 1) throw new ArgumentException();
            return args[0].Convert(ValueType.Number);
        }

        public static Value Abs(List<Value> args)
        {
            if (args.Count < 1) throw new ArgumentException();
            return new Value(Math.Abs(args[0].Number));
        }

        public static Value Min(List<Value> args)
        {
            if (args.Count < 2) throw new ArgumentException();
            return new Value(Math.Min(args[0].Number, args[1].Number));
        }

        public static Value Max(List<Value> args)
        {
            if (args.Count < 2) throw new ArgumentException();
            return new Value(Math.Max(args[0].Number, args[1].Number));
        }

        public static Value Not(List<Value> args)
        {
            if (args.Count < 1) throw new ArgumentException();
            return new Value(args[0].Number == 0 ? 1 : 0);
        }
    }

    /// <summary>
    /// Lexical analyzyer
    /// </summary>
    class Lexer
    {
        public Token CurrentToken { get; private set; }
        public Location TokenLocation { get; private set; }
        public Location CurrentLocation { get { return this.currentLocation; } }
        public string Identifer { get; private set; }
        public Value Value { get; private set; }
        public int TotalTokenCount { get; private set; }

        readonly string source;
        Location currentLocation;
        char currentChar;
        char nextChar;

        public Lexer(string input)
        {
            this.source = input;
            this.Move(new Location());
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
            else if (this.currentChar == '/' && this.nextChar == '/') token = this.ReadComment();
            else if (this.IsLetterOrUnderscore(this.currentChar)) token = this.ReadKeyword();
            else if (char.IsDigit(this.currentChar)) token = this.ReadNumber();
            else token = this.ReadOperator();
            this.CurrentToken = token;
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

        Token ReadKeyword()
        {
            this.Identifer = this.currentChar.ToString();
            while (this.IsLetterOrDigitOrUnderscore(this.ReadChar())) this.Identifer += this.currentChar;
            //Debug.WriteLine($"GetToken Identifier:{this.Identifer}");
            var token = Token.Identifer;
            switch (this.Identifer.ToLower())
            {
                case "p": token = Token.Print; break;
                case "if": token = Token.If; break;
                case "elif": token = Token.Elif; break;
                case "else": token = Token.Else; break;
                case "endif": token = Token.EndIf; break;
                case "for": token = Token.For; break;
                case "to": token = Token.To; break;
                case "endfor": token = Token.EndFor; break;
                case "exit": token = Token.Exit; break;
                case "and": token = Token.And; break;
                case "or": token = Token.Or; break;
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
            else if (this.currentChar == ';') token = Token.Semicolon;
            else if (this.currentChar == ',') token = Token.Comma;
            else if (this.currentChar == '=' && this.nextChar == '=') { token = Token.Equal; this.ReadChar(); }
            else if (this.currentChar == '=') token = Token.Assign;
            else if (this.currentChar == '!' && this.nextChar == '=') { token = Token.NotEqual; this.ReadChar(); }
            else if (this.currentChar == '+') token = Token.Plus;
            else if (this.currentChar == '-') token = Token.Minus;
            else if (this.currentChar == '/') token = Token.Slash;
            else if (this.currentChar == '*') token = Token.Asterisk;
            else if (this.currentChar == '^') token = Token.Caret;
            else if (this.currentChar == '(') token = Token.LeftParen;
            else if (this.currentChar == ')') token = Token.RightParen;
            else if (this.currentChar == '\'')
            {
                while (this.currentChar != '\n') this.ReadChar();
                this.ReadChar();
                token = this.ReadToken();
            }
            else if (this.currentChar == '<' && this.nextChar == '=') { token = Token.LessEqual; this.ReadChar(); }
            else if (this.currentChar == '<') token = Token.Less;
            else if (this.currentChar == '>' && this.nextChar == '=') { token = Token.MoreEqual; this.ReadChar(); }
            else if (this.currentChar == '>') token = Token.More;
            else if (this.currentChar == '"')
            {
                this.Value = this.ReadString();
                token = Token.Value;
            }
            else token = Token.Unkown;
            this.ReadChar();
            return token;
        }

        Value ReadString()
        {
            var s = new StringBuilder();
            while (this.ReadChar() != '"')
            {
                if (this.currentChar == (char)0) throw new Exception($"InvalidString: {s}");
                if (this.currentChar == '\\')
                {
                    // Escape sequence
                    var c = char.ToLower(this.ReadChar());
                    if (c == 'n') s.Append('\n');
                    else if (c == 'n') s.Append('\n');
                    else if (c == 't') s.Append('\t');
                    else if (c == '\\') s.Append('\\');
                    else if (c == '"') s.Append('"');
                    else s.Append(c);
                }
                else
                {
                    s.Append(this.currentChar);
                }
            }
            return new Value(s.ToString());
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

    struct Loop
    {
        public Location Location;
        public string Var;

        public Loop(Location location, string var)
        {
            this.Location = location;
            this.Var = var;
        }
    }

    enum Token
    {
        Unkown,

        Identifer,
        Value,

        //Keywords
        Print,
        If,
        Elif,
        EndIf,
        Else,
        For,
        To,
        EndFor,
        Exit,

        NewLine,
        Colon,
        Semicolon,
        Comma,

        Plus,
        Minus,
        Slash,
        Asterisk,
        Caret,
        Assign,
        Equal,
        Less,
        More,
        NotEqual,
        LessEqual,
        MoreEqual,
        Or,
        And,
        //Not,

        LeftParen,
        RightParen,

        EOF = -1   //End Of File
    }
}
