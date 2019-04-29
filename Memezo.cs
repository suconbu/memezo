// MemezoScript - The script language for automated operation.
// Based on https://github.com/Timu5/BasicSharp

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Suconbu.Scripting.Memezo
{
    public enum ValueType { Number, String }

    public class Interpreter
    {
        public delegate Value FunctionHandler(List<Value> args);
        public delegate void ActionHandler(List<Value> args);
        public ErrorInfo Error { get; private set; }
        public int TotalStatementCount { get; private set; }
        public int TotalTokenCount { get; private set; }

        Lexer lex;
        Token prevToken;
        Token lastToken;
        Location statementLocation;
        Location lastTokenLocation;
        bool exit;

        readonly Dictionary<string, Value> vars = new Dictionary<string, Value>();
        readonly Dictionary<string, Location> labels = new Dictionary<string, Location>();
        readonly Stack<Loop> loops = new Stack<Loop>();

        readonly Dictionary<string, FunctionHandler> functions = new Dictionary<string, FunctionHandler>();
        readonly Dictionary<string, ActionHandler> actions = new Dictionary<string, ActionHandler>();

        public Interpreter(string input)
        {
            this.lex = new Lexer(input);
            BuiltinFunction.InstallAll(this);
        }

        public bool TryGetVar(string name, out Value value)
        {
            value = Value.Zero;
            if (this.vars.ContainsKey(name)) return false;
            value = this.vars[name];
            return true;
        }

        public void SetVar(string name, Value val)
        {
            if (!this.vars.ContainsKey(name)) this.vars.Add(name, val);
            else this.vars[name] = val;
        }

        public void AddFunction(string name, FunctionHandler function)
        {
            this.functions[name] = function;
        }

        public void AddAction(string name, ActionHandler action)
        {
            this.actions[name] = action;
        }

        public bool Run()
        {
            bool result = false;
            try
            {
                this.Initialize();
                this.GetNextToken();
                while (!this.exit) this.Statement();
                result = true;
            }
            catch (Exception ex)
            {
                this.Error = new ErrorInfo(ex.Message, this.lastTokenLocation);
            }
            return result;
        }

        void Initialize()
        {
            this.exit = false;
            this.TotalStatementCount = 0;
            this.TotalTokenCount = 0;
        }

        void Statement()
        {
            this.TotalStatementCount++;

            this.statementLocation = this.lex.TokenLocation;

            Token keyword = this.lastToken;
            var token = this.GetNextToken();
            switch (keyword)
            {
                case Token.Goto: this.Goto(); break;
                case Token.If: this.If(); break;
                case Token.Elif: this.Else(); break;
                case Token.Else: this.Else(); break;
                case Token.EndIf: break;
                case Token.For: this.For(); break;
                case Token.EndFor: this.EndFor(); break;
                case Token.End: this.End(); break;
                case Token.Identifer:
                    if (token == Token.Let) this.Let();
                    else if (token == Token.Colon) this.Label();
                    else if (token == Token.LParen) this.Invoke();
                    else this.Expr();
                    break;
                case Token.NewLine:
                    break;
                case Token.EOF:
                    this.exit = true;
                    break;
                default:
                    this.RiseError($"UnexpectedToken: {keyword}");
                    break;
            }
        }

        void RiseError(string message)
        {
            throw new Exception(message);
        }

        void Match(Token token)
        {
            if (this.lastToken != token) this.RiseError($"MissingToken: {token}");
        }

        Token GetNextToken()
        {
            this.TotalTokenCount++;

            this.prevToken = this.lastToken;
            this.lastToken = this.lex.GetToken();
            this.lastTokenLocation = this.lex.TokenLocation;

            return this.lastToken;
        }

        void Goto()
        {
            this.Match(Token.Identifer);
            string name = this.lex.Identifer;

            if (!this.labels.ContainsKey(name))
            {
                while (true)
                {
                    if (this.GetNextToken() == Token.Colon && this.prevToken == Token.Identifer)
                    {
                        if (!this.labels.ContainsKey(this.lex.Identifer))
                            this.labels.Add(this.lex.Identifer, this.lex.CurrentLocation);
                        if (this.lex.Identifer == name)
                            break;
                    }
                    if (this.lastToken == Token.EOF) this.RiseError($"CannotFindLabel: {name}");
                }
            }
            this.lex.Move(this.labels[name]);
            this.lastToken = Token.NewLine;
        }

        void If()
        {
            bool result = (this.Expr().BinaryOperation(new Value(0), Token.Equal).Number == 1);
            if (result)
            {
                // Condition is not satisfied.
                int depth = 0;
                while (true)
                {
                    if (this.lastToken == Token.If)
                    {
                        depth++;
                    }
                    else if (this.lastToken == Token.Elif)
                    {
                        if (depth == 0)
                        {
                            this.GetNextToken();
                            this.If(); // Recursive
                            return;
                        }
                    }
                    else if (this.lastToken == Token.Else)
                    {
                        if (depth == 0)
                        {
                            this.GetNextToken();
                            return;
                        }
                    }
                    else if (this.lastToken == Token.EndIf)
                    {
                        if (depth == 0)
                        {
                            this.GetNextToken();
                            return;
                        }
                        depth--;
                    }
                    if (this.GetNextToken() == Token.EOF) break;
                }
            }
        }

        void Else()
        {
            // After if clause executed.

            int depth = 0;
            while (true)
            {
                if (this.lastToken == Token.If)
                {
                    depth++;
                }
                else if (this.lastToken == Token.EndIf)
                {
                    if (depth == 0)
                    {
                        this.GetNextToken();
                        break;
                    }
                    depth--;
                }
                if (this.GetNextToken() == Token.EOF) break;
            }
        }

        void Label()
        {
            string name = this.lex.Identifer;
            if (!this.labels.ContainsKey(name)) this.labels.Add(name, this.lex.CurrentLocation);
            this.GetNextToken();
            this.Match(Token.NewLine);
        }

        void End()
        {
            this.exit = true;
        }

        void Let()
        {
            if (this.lastToken != Token.Let)
            {
                this.Match(Token.Identifer);
                this.GetNextToken();
                this.Match(Token.Let);
            }

            string id = this.lex.Identifer;

            this.GetNextToken();

            this.SetVar(id, this.Expr());
        }

        void Invoke()
        {
            string name = this.lex.Identifer;
            List<Value> args = new List<Value>();
            while (true)
            {
                if (this.GetNextToken() != Token.RParen)
                {
                    args.Add(this.Expr());
                    if (this.lastToken == Token.Comma)
                        continue;
                }
                break;
            }
            this.Match(Token.RParen);
            if (this.functions.TryGetValue(name, out var function)) function(args);
            else if (this.actions.TryGetValue(name, out var action)) action(args);
            else this.RiseError($"UndeclaredIdentifier: {name}");
            this.GetNextToken();
        }

        void For()
        {
            this.Match(Token.Identifer);
            string var = this.lex.Identifer;

            this.GetNextToken();
            this.Match(Token.Let);

            this.GetNextToken();
            Value v = this.Expr();

            if (this.loops.Count == 0 || this.loops.Peek().Var != var)
            {
                this.SetVar(var, v);
                this.loops.Push(new Loop(this.statementLocation, var));
            }

            this.Match(Token.To);

            this.GetNextToken();
            v = this.Expr();

            if (this.vars[var].BinaryOperation(v, Token.More).Number == 1)
            {
                int counter = 0;
                while (counter >= 0)
                {
                    this.GetNextToken();
                    if (this.lastToken == Token.For) ++counter;
                    else if (this.lastToken == Token.EndFor) --counter;
                }
                this.loops.Pop();
                this.GetNextToken();
                //this.Match(Token.NewLine);
            }
        }

        void EndFor()
        {
            if(this.loops.Count <= 0) this.RiseError($"TooManyEndFor");

            var loop = this.loops.Peek();
            this.vars[loop.Var] = this.vars[loop.Var].BinaryOperation(new Value(1), Token.Plus);
            this.lex.Move(loop.Location);
            this.lastToken = Token.NewLine;
        }

        Value Expr(int min = 0)
        {
            Dictionary<Token, int> precedens = new Dictionary<Token, int>()
            {
                { Token.Or, 0 }, { Token.And, 0 },
                { Token.Equal, 1 }, { Token.NotEqual, 1 },
                { Token.Less, 1 }, { Token.More, 1 },
                { Token.LessEqual, 1 },  { Token.MoreEqual, 1 },
                { Token.Plus, 2 }, { Token.Minus, 2 },
                { Token.Asterisk, 3 }, {Token.Slash, 3 },
                { Token.Caret, 4 }
            };

            Value lhs = this.Primary();

            while (true)
            {
                if (this.lastToken < Token.Plus || this.lastToken > Token.And) break;
                if ((precedens.TryGetValue(this.lastToken, out var p) ? p : -1) < min) break;

                Token op = this.lastToken;
                int prec = precedens[this.lastToken];
                int assoc = 0; // 0 left, 1 right
                int nextmin = assoc == 0 ? prec : prec + 1;
                this.GetNextToken();
                Value rhs = this.Expr(nextmin);
                lhs = lhs.BinaryOperation(rhs, op);
            }

            return lhs;
        }

        Value Primary()
        {
            Value prim = Value.Zero;

            if (this.lastToken == Token.Value)
            {
                prim = this.lex.Value;
                this.GetNextToken();
            }
            else if (this.lastToken == Token.Identifer)
            {
                if (this.vars.ContainsKey(this.lex.Identifer))
                {
                    prim = this.vars[this.lex.Identifer];
                }
                else if (this.functions.ContainsKey(this.lex.Identifer))
                {
                    string name = this.lex.Identifer;
                    List<Value> args = new List<Value>();
                    this.GetNextToken();
                    this.Match(Token.LParen);

                    while (true)
                    {
                        if (this.GetNextToken() != Token.RParen)
                        {
                            args.Add(this.Expr());
                            if (this.lastToken == Token.Comma) continue;
                        }
                        break;
                    }

                    prim = this.functions[name](args);
                }
                else
                {
                    this.RiseError($"UndeclaredIdentifier: {this.lex.Identifer}");
                }
                this.GetNextToken();
            }
            else if (this.lastToken == Token.LParen)
            {
                this.GetNextToken();
                prim = this.Expr();
                this.Match(Token.RParen);
                this.GetNextToken();
            }
            else if (this.lastToken == Token.Plus || this.lastToken == Token.Minus)
            {
                Token op = this.lastToken;
                this.GetNextToken();
                prim = Value.Zero.BinaryOperation(this.Primary(), op); // we dont realy have a unary operators
            }
            else
            {
                this.RiseError($"UnexpectedToken: {this.lastToken}");
            }

            return prim;
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
            Value a = this;
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
        public Location TokenLocation { get; private set; }
        public Location CurrentLocation { get { return this.currentLocation; } }
        public string Identifer { get; private set; }
        public Value Value { get; private set; }

        readonly string source;
        Location currentLocation;
        char currentChar;

        public Lexer(string input)
        {
            this.source = input;
            this.currentLocation = new Location();
            this.currentChar = (this.source.Length > 0) ? this.source[0] : (char)0;
        }

        public void Move(Location location)
        {
            this.currentLocation = location;
            this.currentChar = this.source[location.CharIndex];
        }

        public Token GetToken()
        {
            while (this.currentChar == ' ' || this.currentChar == '\t' || this.currentChar == '\r')
                this.GetChar();

            this.TokenLocation = this.currentLocation;

            if (this.currentChar == '/' && this.GetChar() == '/')
            {
                // Line comment
                while (this.currentChar != '\n') this.GetChar();
                return Token.NewLine;
            }

            if (char.IsLetter(this.currentChar))
            {
                this.Identifer = this.currentChar.ToString();
                while (this.IsLetterOrDigitOrUnderscore(this.GetChar())) this.Identifer += this.currentChar;
                //Debug.Print(this.Identifer);
                switch (this.Identifer.ToUpper())
                {
                    case "IF": return Token.If;
                    case "ELIF": return Token.Elif;
                    case "ELSE": return Token.Else;
                    case "ENDIF": return Token.EndIf;
                    case "FOR": return Token.For;
                    case "TO": return Token.To;
                    case "ENDFOR": return Token.EndFor;
                    case "GOTO": return Token.Goto;
                    case "END": return Token.End;
                    default:
                        return Token.Identifer;
                }
            }

            if (char.IsDigit(this.currentChar))
            {
                string num = "";
                do { num += this.currentChar; } while (char.IsDigit(this.GetChar()) || this.currentChar == '.');

                double real;
                if (!double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out real))
                    throw new Exception("ERROR while parsing number");
                this.Value = new Value(real);
                return Token.Value;
            }

            Token token = Token.Unkown;
            switch (this.currentChar)
            {
                case '\n': token = Token.NewLine; break;
                case ':': token = Token.Colon; break;
                case ';': token = Token.Semicolon; break;
                case ',': token = Token.Comma; break;
                case '=':
                    this.GetChar();
                    if (this.currentChar == '=') token = Token.Equal;
                    else return Token.Let;
                    break;
                case '!':
                    this.GetChar();
                    if (this.currentChar == '=') token = Token.NotEqual;
                    else return Token.Unkown;
                    break;
                case '+': token = Token.Plus; break;
                case '-': token = Token.Minus; break;
                case '/': token = Token.Slash; break;
                case '*': token = Token.Asterisk; break;
                case '^': token = Token.Caret; break;
                case '(': token = Token.LParen; break;
                case ')': token = Token.RParen; break;
                case '\'':
                    while (this.currentChar != '\n') this.GetChar();
                    this.GetChar();
                    return this.GetToken();
                case '<':
                    this.GetChar();
                    if (this.currentChar == '=') token = Token.LessEqual;
                    else return Token.Less;
                    break;
                case '>':
                    this.GetChar();
                    if (this.currentChar == '=') token = Token.MoreEqual;
                    else return Token.More;
                    break;
                case '"':
                    string str = "";
                    while (this.GetChar() != '"')
                    {
                        if (this.currentChar == 0) return Token.EOF;
                        if (this.currentChar == '\\')
                        {
                            switch (char.ToLower(this.GetChar()))
                            {
                                case 'n': str += '\n'; break;
                                case 't': str += '\t'; break;
                                case '\\': str += '\\'; break;
                                case '"': str += '"'; break;
                            }
                        }
                        else
                        {
                            str += this.currentChar;
                        }
                    }
                    this.Value = new Value(str);
                    token = Token.Value;
                    break;
                case '&':
                    this.GetChar();
                    if (this.currentChar == '&') token = Token.And;
                    else return Token.Unkown;
                    break;
                case '|':
                    this.GetChar();
                    if (this.currentChar == '|') token = Token.Or;
                    else return Token.Unkown;
                    break;
                case (char)0:
                    return Token.EOF;
            }

            this.GetChar();
            return token;
        }

        char GetChar()
        {
            this.currentLocation.CharIndex++;
            if (this.currentChar == '\n')
            {
                this.currentLocation.Column = 0;
                this.currentLocation.Line++;
            }
            else
            {
                this.currentLocation.Column++;
            }

            this.currentChar = (this.currentLocation.CharIndex < this.source.Length) ?
                this.source[this.currentLocation.CharIndex] : (char)0;

            return this.currentChar;
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
        Goto,
        End,

        NewLine,
        Colon,
        Semicolon,
        Comma,

        Plus,
        Minus,
        Slash,
        Asterisk,
        Caret,
        Let,
        Equal,
        Less,
        More,
        NotEqual,
        LessEqual,
        MoreEqual,
        Or,
        And,
        //Not,

        LParen,
        RParen,

        EOF = -1   //End Of File
    }
}
