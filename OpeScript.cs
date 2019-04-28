// OpeScript
// Script language for automated operation.
// Based on https://github.com/Timu5/BasicSharp

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Suconbu.Scripting
{
    public class OpeScript
    {
        private Lexer lex;
        private Token prevToken;
        private Token lastToken;

        private Dictionary<string, Value> vars;
        private Dictionary<string, Marker> labels;
        private Dictionary<string, Marker> loops;

        public delegate Value OpeFunction(OpeScript interpreter, List<Value> args);
        private Dictionary<string, OpeFunction> functions;

        public delegate void OpeAction(OpeScript interpreter, List<Value> args);
        private Dictionary<string, OpeAction> actions;

        private int ifcounter;

        private Marker lineMarker;

        private bool exit;

        public OpeScript(string input)
        {
            this.lex = new Lexer(input);
            this.vars = new Dictionary<string, Value>();
            this.labels = new Dictionary<string, Marker>();
            this.loops = new Dictionary<string, Marker>();
            this.functions = new Dictionary<string, OpeFunction>();
            this.actions = new Dictionary<string, OpeAction>();
            this.ifcounter = 0;
            BuiltIns.InstallAll(this);
        }

        public Value GetVar(string name)
        {
            if (!this.vars.ContainsKey(name))
                throw new Exception("Variable with name " + name + " does not exist.");
            return this.vars[name];
        }

        public void SetVar(string name, Value val)
        {
            if (!this.vars.ContainsKey(name)) this.vars.Add(name, val);
            else this.vars[name] = val;
        }

        public void AddFunction(string name, OpeFunction function)
        {
            this.functions[name] = function;
        }

        public void AddAction(string name, OpeAction action)
        {
            this.actions[name] = action;
        }

        public void Run()
        {
            this.exit = false;
            this.GetNextToken();
            while (!this.exit) this.Line();
        }

        void Error(string text)
        {
            throw new Exception(text + " at line: " + this.lineMarker.Line);
        }

        void Match(Token tok)
        {
            if (this.lastToken != tok)
                this.Error("Expect " + tok.ToString() + " got " + this.lastToken.ToString());
        }

        Token GetNextToken()
        {
            this.prevToken = this.lastToken;
            this.lastToken = this.lex.GetToken();

            if (this.lastToken == Token.EOF && this.prevToken == Token.EOF)
                this.Error("Unexpected end of file");

            return this.lastToken;
        }

        void Line()
        {
            while (this.lastToken == Token.NewLine) this.GetNextToken();

            if (this.lastToken == Token.EOF)
            {
                this.exit = true;
                return;
            }

            this.lineMarker = this.lex.TokenMarker;
            this.Statment();

            if (this.lastToken != Token.NewLine && this.lastToken != Token.EOF)
                this.Error("Expect new line got " + this.lastToken.ToString());
        }

        void Statment()
        {
            Token keyword = this.lastToken;
            var token = this.GetNextToken();
            switch (keyword)
            {
                //case Token.Print: Print(); break;
                //case Token.Input: Input(); break;
                case Token.Goto: this.Goto(); break;
                case Token.If: this.If(); break;
                case Token.Else: this.Else(); break;
                case Token.EndIf: break;
                case Token.For: this.For(); break;
                case Token.Next: this.Next(); break;
                //case Token.Let: Let(); break;
                case Token.End: this.End(); break;
                case Token.Identifer:
                    if (token == Token.Let)
                    {
                        this.Let();
                    }
                    else if (token == Token.Colon)
                    {
                        this.Label();
                    }
                    else
                    {
                        if (token == Token.LParen &&
                            (this.functions.ContainsKey(this.lex.Identifer) || this.actions.ContainsKey(this.lex.Identifer)))
                        {
                            this.Invoke();
                        }
                        else
                        {
                            this.Expr();
                        }
                    }
                    break;
                case Token.EOF:
                    this.exit = true;
                    break;
                default:
                    this.Error("Expect keyword got " + keyword.ToString());
                    break;
            }
            if (this.lastToken == Token.Colon)
            {
                this.GetNextToken();
                this.Statment();
            }
        }

        //void Print()
        //{
        //    Console.WriteLine(Expr().ToString());
        //}

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
                            this.labels.Add(this.lex.Identifer, this.lex.TokenMarker);
                        if (this.lex.Identifer == name)
                            break;
                    }
                    if (this.lastToken == Token.EOF)
                    {
                        this.Error("Cannot find label named " + name);
                    }
                }
            }
            this.lex.GoTo(this.labels[name]);
            this.lastToken = Token.NewLine;
        }

        void If()
        {
            bool result = (this.Expr().BinOp(new Value(0), Token.Equal).Real == 1);

            this.Match(Token.Then);
            this.GetNextToken();

            if (result)
            {
                int i = this.ifcounter;
                while (true)
                {
                    if (this.lastToken == Token.If)
                    {
                        i++;
                    }
                    else if (this.lastToken == Token.Else)
                    {
                        if (i == this.ifcounter)
                        {
                            this.GetNextToken();
                            return;
                        }
                    }
                    else if (this.lastToken == Token.EndIf)
                    {
                        if (i == this.ifcounter)
                        {
                            this.GetNextToken();
                            return;
                        }
                        i--;
                    }
                    this.GetNextToken();
                }
            }
        }

        void Else()
        {
            int i = this.ifcounter;
            while (true)
            {
                if (this.lastToken == Token.If)
                {
                    i++;
                }
                else if (this.lastToken == Token.EndIf)
                {
                    if (i == this.ifcounter)
                    {
                        this.GetNextToken();
                        return;
                    }
                    i--;
                }
                this.GetNextToken();
            }
        }

        void Label()
        {
            string name = this.lex.Identifer;
            if (!this.labels.ContainsKey(name)) this.labels.Add(name, this.lex.TokenMarker);

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
            if (this.functions.TryGetValue(name, out var function)) function(this, args);
            else if (this.actions.TryGetValue(name, out var action)) action(this, args);
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

            if (this.loops.ContainsKey(var))
            {
                this.loops[var] = this.lineMarker;
            }
            else
            {
                this.SetVar(var, v);
                this.loops.Add(var, this.lineMarker);
            }

            this.Match(Token.To);

            this.GetNextToken();
            v = this.Expr();

            if (this.vars[var].BinOp(v, Token.More).Real == 1)
            {
                while (true)
                {
                    while (!(this.GetNextToken() == Token.Identifer && this.prevToken == Token.Next)) ;
                    if (this.lex.Identifer == var)
                    {
                        this.loops.Remove(var);
                        this.GetNextToken();
                        this.Match(Token.NewLine);
                        break;
                    }
                }
            }

        }

        void Next()
        {
            this.Match(Token.Identifer);
            string var = this.lex.Identifer;
            this.vars[var] = this.vars[var].BinOp(new Value(1), Token.Plus);
            this.lex.GoTo(new Marker(this.loops[var].Pointer - 1, this.loops[var].Line, this.loops[var].Column - 1));
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
                lhs = lhs.BinOp(rhs, op);
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

                start:
                    if (this.GetNextToken() != Token.RParen)
                    {
                        args.Add(this.Expr());
                        if (this.lastToken == Token.Comma)
                            goto start;
                    }

                    prim = this.functions[name](this, args);
                }
                else
                {
                    this.Error("Undeclared variable " + this.lex.Identifer);
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
                prim = Value.Zero.BinOp(this.Primary(), op); // we dont realy have a unary operators
            }
            else
            {
                this.Error("Unexpexted token in primary!");
            }

            return prim;
        }
    }

    class BuiltIns
    {
        public static void InstallAll(OpeScript interpreter)
        {
            interpreter.AddFunction("str", Str);
            interpreter.AddFunction("num", Num);
            interpreter.AddFunction("abs", Abs);
            interpreter.AddFunction("min", Min);
            interpreter.AddFunction("max", Max);
            interpreter.AddFunction("not", Not);
        }

        public static Value Str(OpeScript interpreter, List<Value> args)
        {
            if (args.Count < 1)
                throw new ArgumentException();

            return args[0].Convert(ValueType.String);
        }

        public static Value Num(OpeScript interpreter, List<Value> args)
        {
            if (args.Count < 1)
                throw new ArgumentException();

            return args[0].Convert(ValueType.Real);
        }

        public static Value Abs(OpeScript interpreter, List<Value> args)
        {
            if (args.Count < 1)
                throw new ArgumentException();

            return new Value(Math.Abs(args[0].Real));
        }

        public static Value Min(OpeScript interpreter, List<Value> args)
        {
            if (args.Count < 2)
                throw new ArgumentException();

            return new Value(Math.Min(args[0].Real, args[1].Real));
        }

        public static Value Max(OpeScript interpreter, List<Value> args)
        {
            if (args.Count < 1)
                throw new ArgumentException();

            return new Value(Math.Max(args[0].Real, args[1].Real));
        }

        public static Value Not(OpeScript interpreter, List<Value> args)
        {
            if (args.Count < 1)
                throw new ArgumentException();

            return new Value(args[0].Real == 0 ? 1 : 0);
        }
    }

    public class Lexer
    {
        private readonly string source;
        private Marker sourceMarker;
        private char lastChar;

        public Marker TokenMarker { get; set; }

        public string Identifer { get; set; }
        public Value Value { get; set; }

        public Lexer(string input)
        {
            this.source = input;
            this.sourceMarker = new Marker(0, 1, 1);
            this.lastChar = this.source[0];
        }

        public void GoTo(Marker marker)
        {
            this.sourceMarker = marker;
        }

        char GetChar()
        {
            this.sourceMarker.Column++;
            this.sourceMarker.Pointer++;

            if (this.sourceMarker.Pointer >= this.source.Length)
                return this.lastChar = (char)0;

            if ((this.lastChar = this.source[this.sourceMarker.Pointer]) == '\n')
            {
                this.sourceMarker.Column = 1;
                this.sourceMarker.Line++;
            }
            return this.lastChar;
        }

        public Token GetToken()
        {
            while (this.lastChar == ' ' || this.lastChar == '\t' || this.lastChar == '\r')
                this.GetChar();

            this.TokenMarker = this.sourceMarker;

            if (this.lastChar == '/' && this.GetChar() == '/')
            {
                // Comment
                while (this.lastChar != '\n') this.GetChar();
                return Token.NewLine;
            }

            if (char.IsLetter(this.lastChar))
            {
                this.Identifer = this.lastChar.ToString();
                while (this.IsLetterOrDigitOrUnderscore(this.GetChar())) this.Identifer += this.lastChar;
                Debug.Print(this.Identifer);
                switch (this.Identifer.ToUpper())
                {
                    //case "PRINT": return Token.Print;
                    case "IF": return Token.If;
                    case "ENDIF": return Token.EndIf;
                    case "THEN": return Token.Then;
                    case "ELSE": return Token.Else;
                    case "FOR": return Token.For;
                    case "TO": return Token.To;
                    case "NEXT": return Token.Next;
                    case "GOTO": return Token.Goto;
                    //case "INPUT": return Token.Input;
                    //case "LET": return Token.Let;
                    //case "GOSUB": return Token.Gosub;
                    //case "RETURN": return Token.Return;
                    case "END": return Token.End;
                    //case "OR": return Token.Or;
                    //case "AND": return Token.And;
                    default:
                        return Token.Identifer;
                }
            }

            if (char.IsDigit(this.lastChar))
            {
                string num = "";
                do { num += this.lastChar; } while (char.IsDigit(this.GetChar()) || this.lastChar == '.');

                double real;
                if (!double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out real))
                    throw new Exception("ERROR while parsing number");
                this.Value = new Value(real);
                return Token.Value;
            }

            Token tok = Token.Unkown;
            switch (this.lastChar)
            {
                case '\n': tok = Token.NewLine; break;
                case ':': tok = Token.Colon; break;
                case ';': tok = Token.Semicolon; break;
                case ',': tok = Token.Comma; break;
                case '=':
                    this.GetChar();
                    if (this.lastChar == '=') tok = Token.Equal;
                    else return Token.Let;
                    break;
                case '!':
                    this.GetChar();
                    if (this.lastChar == '=') tok = Token.NotEqual;
                    else return Token.Unkown;
                    break;
                case '+': tok = Token.Plus; break;
                case '-': tok = Token.Minus; break;
                case '/': tok = Token.Slash; break;
                case '*': tok = Token.Asterisk; break;
                case '^': tok = Token.Caret; break;
                case '(': tok = Token.LParen; break;
                case ')': tok = Token.RParen; break;
                case '\'':
                    while (this.lastChar != '\n') this.GetChar();
                    this.GetChar();
                    return this.GetToken();
                case '<':
                    this.GetChar();
                    if (this.lastChar == '=') tok = Token.LessEqual;
                    else return Token.Less;
                    break;
                case '>':
                    this.GetChar();
                    if (this.lastChar == '=') tok = Token.MoreEqual;
                    else return Token.More;
                    break;
                case '"':
                    string str = "";
                    while (this.GetChar() != '"')
                    {
                        if (this.lastChar == '\\')
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
                            str += this.lastChar;
                        }
                    }
                    this.Value = new Value(str);
                    tok = Token.Value;
                    break;
                case '&':
                    this.GetChar();
                    if (this.lastChar == '&') tok = Token.And;
                    else return Token.Unkown;
                    break;
                case '|':
                    this.GetChar();
                    if (this.lastChar == '|') tok = Token.Or;
                    else return Token.Unkown;
                    break;
                case (char)0:
                    return Token.EOF;
            }

            this.GetChar();
            return tok;
        }

        bool IsLetterOrDigitOrUnderscore(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }
    }

    public struct Marker
    {
        public int Pointer { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public Marker(int pointer, int line, int column)
            : this()
        {
            this.Pointer = pointer;
            this.Line = line;
            this.Column = this.Column;
        }
    }

    public enum Token
    {
        Unkown,

        Identifer,
        Value,

        //Keywords
        Print,
        If,
        EndIf,
        Then,
        Else,
        For,
        To,
        Next,
        Goto,
        //Input,
        //Let,
        //Gosub,
        //Return,
        //Rem,
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

    public enum ValueType
    {
        Real,
        String
    }

    public struct Value
    {
        public static readonly Value Zero = new Value(0);
        public ValueType Type { get; set; }

        public double Real { get; set; }
        public string String { get; set; }

        public Value(double real) : this()
        {
            this.Type = ValueType.Real;
            this.Real = real;
        }

        public Value(string str)
            : this()
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
                    case ValueType.Real:
                        this.Real = double.Parse(this.String);
                        this.Type = ValueType.Real;
                        break;
                    case ValueType.String:
                        this.String = this.Real.ToString();
                        this.Type = ValueType.String;
                        break;
                }
            }
            return this;
        }

        public Value BinOp(Value b, Token tok)
        {
            Value a = this;
            if (a.Type != b.Type)
            {
                if (a.Type > b.Type)
                    b = b.Convert(a.Type);
                else
                    a = a.Convert(b.Type);
            }

            if (tok == Token.Plus)
            {
                if (a.Type == ValueType.Real)
                    return new Value(a.Real + b.Real);
                else
                    return new Value(a.String + b.String);
            }
            else if (tok == Token.Equal)
            {
                if (a.Type == ValueType.Real)
                    return new Value(a.Real == b.Real ? 1 : 0);
                else
                    return new Value(a.String == b.String ? 1 : 0);
            }
            else if (tok == Token.NotEqual)
            {
                if (a.Type == ValueType.Real)
                    return new Value(a.Real == b.Real ? 0 : 1);
                else
                    return new Value(a.String == b.String ? 0 : 1);
            }
            else
            {
                if (a.Type == ValueType.String)
                    throw new Exception("Cannot do binop on strings(except +).");

                switch (tok)
                {
                    case Token.Minus: return new Value(a.Real - b.Real);
                    case Token.Asterisk: return new Value(a.Real * b.Real);
                    case Token.Slash: return new Value(a.Real / b.Real);
                    case Token.Caret: return new Value(Math.Pow(a.Real, b.Real));
                    case Token.Less: return new Value(a.Real < b.Real ? 1 : 0);
                    case Token.More: return new Value(a.Real > b.Real ? 1 : 0);
                    case Token.LessEqual: return new Value(a.Real <= b.Real ? 1 : 0);
                    case Token.MoreEqual: return new Value(a.Real >= b.Real ? 1 : 0);
                    case Token.And: return new Value(a.Real != 0.0 && b.Real != 0.0 ? 1 : 0);
                    case Token.Or: return new Value(a.Real != 0.0 || b.Real != 0.0 ? 1 : 0);
                }
            }
            throw new Exception("Unkown binop");
        }

        public override string ToString()
        {
            if (this.Type == ValueType.Real)
                return this.Real.ToString();
            return this.String;
        }
    }
}
