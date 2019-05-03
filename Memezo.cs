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

        public Dictionary<string, Value> Vars { get; private set; } = new Dictionary<string, Value>();
        public ErrorInfo Error { get; private set; }
        public int TotalStatementCount { get; private set; }
        public int TotalTokenCount { get; private set; }
        public Value? LastResultValue { get; private set; }

        Lexer lex;
        Token prevToken;
        Token currentToken;
        Location statementLocation;
        //Location prevTokenLocation;
        Location currentTokenLocation;
        bool exit;

        //readonly Dictionary<string, Value> vars = new Dictionary<string, Value>();
        readonly Dictionary<string, Location> labels = new Dictionary<string, Location>();
        readonly Stack<Loop> loops = new Stack<Loop>();

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
            bool result = false;
            try
            {
                this.Initialize();
                this.lex = new Lexer(input);
                while (!this.exit) this.Statement();
                result = true;
            }
            catch (Exception ex)
            {
                this.Error = new ErrorInfo(ex.Message, this.currentTokenLocation);
            }
            return result;
        }

        void Initialize()
        {
            this.exit = false;
            this.TotalStatementCount = 0;
            this.TotalTokenCount = 0;
            this.LastResultValue = null;
        }

        void Statement()
        {
            this.TotalStatementCount++;

            this.statementLocation = this.lex.TokenLocation;

            Token keyword = this.GetNextToken();
            //Debug.WriteLine($"Statement keyword:{keyword}");
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
                case Token.Identifer: this.Identifier(); break;
                case Token.Value: this.DirectValue(); break;
                case Token.NewLine: break;
                case Token.EOF: this.exit = true; break;
                default: this.RiseError($"UnexpectedToken: {keyword}"); break;
            }
        }

        void RiseError(string message)
        {
            throw new Exception(message);
        }

        void VerifyLastToken(Token token)
        {
            if (this.currentToken != token) this.RiseError($"MissingToken: {token}");
        }

        Token GetNextToken(Token expectedToken)
        {
            var token = this.GetNextToken();
            if (token != expectedToken) this.RiseError($"MissingToken: {expectedToken}");
            return token;
        }

        Token GetNextToken()
        {
            this.TotalTokenCount++;

            this.prevToken = this.currentToken;
            //this.prevTokenLocation = this.lex.TokenLocation;
            this.currentToken = this.lex.GetToken();
            this.currentTokenLocation = this.lex.TokenLocation;

            return this.currentToken;
        }

        void Goto()
        {
            this.GetNextToken(Token.Identifer);

            string name = this.lex.Identifer;
            Debug.WriteLine($"{this.lex.CurrentLocation.Line + 1}: Goto {name}");
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
                    if (this.currentToken == Token.EOF) this.RiseError($"CannotFindLabel: {name}");
                }
            }
            this.lex.Move(this.labels[name]);
            this.currentToken = Token.NewLine;
        }

        void If()
        {
            this.GetNextToken();
            bool result = (this.Expr().BinaryOperation(Value.Zero, Token.Equal).Number == 0);
            this.VerifyLastToken(Token.Colon);
            Debug.WriteLine($"{this.lex.CurrentLocation.Line + 1}: If {result}");
            if (!result)
            {
                // Condition is not satisfied.
                int depth = 0;
                while (this.GetNextToken() != Token.EOF)
                {
                    if (this.currentToken == Token.If)
                    {
                        depth++;
                    }
                    else if (this.currentToken == Token.Elif)
                    {
                        if (depth == 0)
                        {
                            this.If(); // Recursive
                            break;
                        }
                    }
                    else if (this.currentToken == Token.Else)
                    {
                        if (depth == 0)
                        {
                            this.GetNextToken(Token.Colon);
                            break;
                        }
                    }
                    else if (this.currentToken == Token.EndIf)
                    {
                        if (depth == 0) break;
                        depth--;
                    }
                }
            }
        }

        void Else()
        {
            // After if clause executed.

            int depth = 0;
            while (this.GetNextToken() != Token.EOF)
            {
                if (this.currentToken == Token.If)
                {
                    depth++;
                }
                else if (this.currentToken == Token.EndIf)
                {
                    if (depth == 0) break;
                    depth--;
                }
            }
        }

        void Label()
        {
            this.GetNextToken(Token.Colon);
            string name = this.lex.Identifer;
            if (!this.labels.ContainsKey(name)) this.labels.Add(name, this.lex.CurrentLocation);
            this.GetNextToken(Token.NewLine);
        }

        void End()
        {
            this.exit = true;
        }

        void Identifier()
        {
            var token = this.GetNextToken();
            //var token = this.lex.PeekToken();
            if (token == Token.Assign) this.Assign();
            else if (token == Token.Colon) this.Label();
            else if (token == Token.LParen) this.Invoke();
            else this.Expr();
        }

        void DirectValue()
        {
            this.Expr();
        }

        void Assign()
        {
            //this.GetNextToken(Token.Identifer);
            //if (this.currentToken != Token.Assign)
            //{
            //    this.Match(Token.Identifer);
            //    this.GetNextToken();
            //    this.Match(Token.Assign);
            //}
            string id = this.lex.Identifer;
            this.GetNextToken();
            this.Vars[id] = this.Expr();
            Debug.WriteLine($"{this.lex.CurrentLocation.Line + 1}: Assing {id}={this.Vars[id].ToString()}");
        }

        void Invoke()
        {
            //this.GetNextToken();
            string name = this.lex.Identifer;
            List<Value> args = new List<Value>();
            while (true)
            {
                if (this.GetNextToken() != Token.RParen)
                {
                    args.Add(this.Expr());
                    if (this.currentToken == Token.Comma)
                        continue;
                }
                break;
            }
            this.VerifyLastToken(Token.RParen);
            if (this.functions.TryGetValue(name, out var function)) this.LastResultValue = function(args);
            else if (this.actions.TryGetValue(name, out var action)) action(args);
            else this.RiseError($"UndeclaredIdentifier: {name}");
            Debug.WriteLine($"{this.lex.CurrentLocation.Line + 1}: Invoke {name}){string.Join(",",args.ConvertAll(v=>v.ToString()))})");
        }

        void For()
        {
            this.GetNextToken(Token.Identifer);
            string var = this.lex.Identifer;

            this.GetNextToken(Token.Assign);

            this.GetNextToken();
            Value fromValue = this.Expr();

            if (this.loops.Count == 0 || this.loops.Peek().Var != var)
            {
                this.Vars[var] = fromValue;
                this.loops.Push(new Loop(this.statementLocation, var));
            }

            this.VerifyLastToken(Token.To);

            this.GetNextToken();
            var toValue = this.Expr();

            this.VerifyLastToken(Token.Colon);

            Debug.WriteLine($"{this.lex.CurrentLocation.Line + 1}: For {fromValue} to {toValue}");

            if (this.Vars[var].BinaryOperation(toValue, Token.More).Number == 1)
            {
                int counter = 0;
                while (counter >= 0)
                {
                    this.GetNextToken();
                    if (this.currentToken == Token.For) ++counter;
                    else if (this.currentToken == Token.EndFor) --counter;
                }
                this.loops.Pop();
            }
        }

        void EndFor()
        {
            if(this.loops.Count <= 0) this.RiseError($"UnexpectedEndFor");
            Debug.WriteLine($"{this.lex.CurrentLocation.Line + 1}: EndFor");

            var loop = this.loops.Peek();
            this.Vars[loop.Var] = this.Vars[loop.Var].BinaryOperation(new Value(1), Token.Plus);
            this.lex.Move(loop.Location);
            this.currentToken = Token.NewLine;
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
                if (this.currentToken < Token.Plus || this.currentToken > Token.And) break;
                if ((precedens.TryGetValue(this.currentToken, out var p) ? p : -1) < min) break;

                Token op = this.currentToken;
                int prec = precedens[this.currentToken];
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

            if (this.currentToken == Token.Value)
            {
                prim = this.lex.Value;
                this.GetNextToken();
            }
            else if (this.currentToken == Token.Identifer)
            {
                if (this.Vars.ContainsKey(this.lex.Identifer))
                {
                    prim = this.Vars[this.lex.Identifer];
                }
                else if (this.functions.ContainsKey(this.lex.Identifer))
                {
                    string name = this.lex.Identifer;
                    List<Value> args = new List<Value>();
                    this.GetNextToken();
                    this.VerifyLastToken(Token.LParen);

                    while (true)
                    {
                        if (this.GetNextToken() != Token.RParen)
                        {
                            args.Add(this.Expr());
                            if (this.currentToken == Token.Comma) continue;
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
            else if (this.currentToken == Token.LParen)
            {
                this.GetNextToken();
                prim = this.Expr();
                this.VerifyLastToken(Token.RParen);
                this.GetNextToken();
            }
            else if (this.currentToken == Token.Plus || this.currentToken == Token.Minus)
            {
                Token op = this.currentToken;
                this.GetNextToken();
                prim = Value.Zero.BinaryOperation(this.Primary(), op); // we dont realy have a unary operators
            }
            else
            {
                this.RiseError($"UnexpectedToken: {this.currentToken}");
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
                this.GetChar();
                return Token.NewLine;
            }

            if (char.IsLetter(this.currentChar))
            {
                this.Identifer = this.currentChar.ToString();
                while (this.IsLetterOrDigitOrUnderscore(this.GetChar())) this.Identifer += this.currentChar;
                //Debug.WriteLine($"GetToken Identifier:{this.Identifer}");
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
                    else return Token.Assign;
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

        public Token PeekToken()
        {
            var location = this.currentLocation;
            var identifier = this.Identifer;
            var value = this.Value;
            var token = this.GetToken();
            this.Move(location);
            this.Identifer = identifier;
            this.Value = value;
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

        LParen,
        RParen,

        EOF = -1   //End Of File
    }
}
