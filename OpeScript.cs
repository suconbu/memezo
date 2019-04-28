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
            if (!vars.ContainsKey(name))
                throw new Exception("Variable with name " + name + " does not exist.");
            return vars[name];
        }

        public void SetVar(string name, Value val)
        {
            if (!vars.ContainsKey(name)) vars.Add(name, val);
            else vars[name] = val;
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
            exit = false;
            GetNextToken();
            while (!exit) Line();
        }

        void Error(string text)
        {
            throw new Exception(text + " at line: " + lineMarker.Line);
        }

        void Match(Token tok)
        {
            if (lastToken != tok)
                Error("Expect " + tok.ToString() + " got " + lastToken.ToString());
        }

        Token GetNextToken()
        {
            prevToken = lastToken;
            lastToken = lex.GetToken();

            if (lastToken == Token.EOF && prevToken == Token.EOF)
                Error("Unexpected end of file");

            return lastToken;
        }

        void Line()
        {
            while (lastToken == Token.NewLine) GetNextToken();

            if (lastToken == Token.EOF)
            {
                exit = true;
                return;
            }

            lineMarker = lex.TokenMarker;
            Statment();

            if (lastToken != Token.NewLine && lastToken != Token.EOF)
                Error("Expect new line got " + lastToken.ToString());
        }

        void Statment()
        {
            Token keyword = this.lastToken;
            var token = GetNextToken();
            switch (keyword)
            {
                //case Token.Print: Print(); break;
                //case Token.Input: Input(); break;
                case Token.Goto: Goto(); break;
                case Token.If: If(); break;
                case Token.Else: Else(); break;
                case Token.EndIf: break;
                case Token.For: For(); break;
                case Token.Next: Next(); break;
                //case Token.Let: Let(); break;
                case Token.End: End(); break;
                case Token.Identifer:
                    if (token == Token.Equal)
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
                    exit = true;
                    break;
                default:
                    Error("Expect keyword got " + keyword.ToString());
                    break;
            }
            if (lastToken == Token.Colon)
            {
                GetNextToken();
                Statment();
            }
        }

        //void Print()
        //{
        //    Console.WriteLine(Expr().ToString());
        //}

        void Goto()
        {
            Match(Token.Identifer);
            string name = lex.Identifer;

            if (!labels.ContainsKey(name))
            {
                while (true)
                {
                    if (GetNextToken() == Token.Colon && prevToken == Token.Identifer)
                    {
                        if (!labels.ContainsKey(lex.Identifer))
                            labels.Add(lex.Identifer, lex.TokenMarker);
                        if (lex.Identifer == name)
                            break;
                    }
                    if (lastToken == Token.EOF)
                    {
                        Error("Cannot find label named " + name);
                    }
                }
            }
            lex.GoTo(labels[name]);
            lastToken = Token.NewLine;
        }

        void If()
        {
            bool result = (Expr().BinOp(new Value(0), Token.Equal).Real == 1);

            Match(Token.Then);
            GetNextToken();

            if (result)
            {
                int i = ifcounter;
                while (true)
                {
                    if (lastToken == Token.If)
                    {
                        i++;
                    }
                    else if (lastToken == Token.Else)
                    {
                        if (i == ifcounter)
                        {
                            GetNextToken();
                            return;
                        }
                    }
                    else if (lastToken == Token.EndIf)
                    {
                        if (i == ifcounter)
                        {
                            GetNextToken();
                            return;
                        }
                        i--;
                    }
                    GetNextToken();
                }
            }
        }

        void Else()
        {
            int i = ifcounter;
            while (true)
            {
                if (lastToken == Token.If)
                {
                    i++;
                }
                else if (lastToken == Token.EndIf)
                {
                    if (i == ifcounter)
                    {
                        GetNextToken();
                        return;
                    }
                    i--;
                }
                GetNextToken();
            }
        }

        void Label()
        {
            string name = lex.Identifer;
            if (!labels.ContainsKey(name)) labels.Add(name, lex.TokenMarker);

            GetNextToken();
            Match(Token.NewLine);
        }

        void End()
        {
            exit = true;
        }

        void Let()
        {
            if (lastToken != Token.Equal)
            {
                Match(Token.Identifer);
                GetNextToken();
                Match(Token.Equal);
            }

            string id = lex.Identifer;

            GetNextToken();

            SetVar(id, Expr());
        }

        void Invoke()
        {
            string name = lex.Identifer;
            List<Value> args = new List<Value>();
            while (true)
            {
                if (this.GetNextToken() != Token.RParen)
                {
                    args.Add(Expr());
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
            Match(Token.Identifer);
            string var = lex.Identifer;

            GetNextToken();
            Match(Token.Equal);

            GetNextToken();
            Value v = Expr();

            if (loops.ContainsKey(var))
            {
                loops[var] = lineMarker;
            }
            else
            {
                SetVar(var, v);
                loops.Add(var, lineMarker);
            }

            Match(Token.To);

            GetNextToken();
            v = Expr();

            if (vars[var].BinOp(v, Token.More).Real == 1)
            {
                while (true)
                {
                    while (!(GetNextToken() == Token.Identifer && prevToken == Token.Next)) ;
                    if (lex.Identifer == var)
                    {
                        loops.Remove(var);
                        GetNextToken();
                        Match(Token.NewLine);
                        break;
                    }
                }
            }

        }

        void Next()
        {
            Match(Token.Identifer);
            string var = lex.Identifer;
            vars[var] = vars[var].BinOp(new Value(1), Token.Plus);
            lex.GoTo(new Marker(loops[var].Pointer - 1, loops[var].Line, loops[var].Column - 1));
            lastToken = Token.NewLine;
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

            Value lhs = Primary();

            while (true)
            {
                if (lastToken < Token.Plus || lastToken > Token.And || precedens[lastToken] < min)
                    break;

                Token op = lastToken;
                int prec = precedens[lastToken];
                int assoc = 0; // 0 left, 1 right
                int nextmin = assoc == 0 ? prec : prec + 1;
                GetNextToken();
                Value rhs = Expr(nextmin);
                lhs = lhs.BinOp(rhs, op);
            }

            return lhs;
        }

        Value Primary()
        {
            Value prim = Value.Zero;

            if (lastToken == Token.Value)
            {
                prim = lex.Value;
                GetNextToken();
            }
            else if (lastToken == Token.Identifer)
            {
                if (vars.ContainsKey(lex.Identifer))
                {
                    prim = vars[lex.Identifer];
                }
                else if (functions.ContainsKey(lex.Identifer))
                {
                    string name = lex.Identifer;
                    List<Value> args = new List<Value>();
                    GetNextToken();
                    Match(Token.LParen);

                start:
                    if (GetNextToken() != Token.RParen)
                    {
                        args.Add(Expr());
                        if (lastToken == Token.Comma)
                            goto start;
                    }

                    prim = functions[name](this, args);
                }
                else
                {
                    Error("Undeclared variable " + lex.Identifer);
                }
                GetNextToken();
            }
            else if (lastToken == Token.LParen)
            {
                GetNextToken();
                prim = Expr();
                Match(Token.RParen);
                GetNextToken();
            }
            else if (lastToken == Token.Plus || lastToken == Token.Minus)
            {
                Token op = lastToken;
                GetNextToken();
                prim = Value.Zero.BinOp(Primary(), op); // we dont realy have a unary operators
            }
            else
            {
                Error("Unexpexted token in primary!");
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
            source = input;
            sourceMarker = new Marker(0, 1, 1);
            lastChar = source[0];
        }

        public void GoTo(Marker marker)
        {
            sourceMarker = marker;
        }

        char GetChar()
        {
            sourceMarker.Column++;
            sourceMarker.Pointer++;

            if (sourceMarker.Pointer >= source.Length)
                return lastChar = (char)0;

            if ((lastChar = source[sourceMarker.Pointer]) == '\n')
            {
                sourceMarker.Column = 1;
                sourceMarker.Line++;
            }
            return lastChar;
        }

        public Token GetToken()
        {
            while (lastChar == ' ' || lastChar == '\t' || lastChar == '\r')
                GetChar();

            TokenMarker = sourceMarker;

            if (char.IsLetter(lastChar))
            {
                Identifer = lastChar.ToString();
                while (this.IsLetterOrDigitOrUnderscore(GetChar())) Identifer += lastChar;
                Debug.Print(Identifer);
                switch (Identifer.ToUpper())
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
                    case "OR": return Token.Or;
                    case "AND": return Token.And;
                    case "REM":
                        while (lastChar != '\n') GetChar();
                        GetChar();
                        return GetToken();
                    default:
                        return Token.Identifer;
                }
            }

            if (char.IsDigit(lastChar))
            {
                string num = "";
                do { num += lastChar; } while (char.IsDigit(GetChar()) || lastChar == '.');

                double real;
                if (!double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out real))
                    throw new Exception("ERROR while parsing number");
                Value = new Value(real);
                return Token.Value;
            }

            Token tok = Token.Unkown;
            switch (lastChar)
            {
                case '\n': tok = Token.NewLine; break;
                case ':': tok = Token.Colon; break;
                case ';': tok = Token.Semicolon; break;
                case ',': tok = Token.Comma; break;
                case '=': tok = Token.Equal; break;
                case '+': tok = Token.Plus; break;
                case '-': tok = Token.Minus; break;
                case '/': tok = Token.Slash; break;
                case '*': tok = Token.Asterisk; break;
                case '^': tok = Token.Caret; break;
                case '(': tok = Token.LParen; break;
                case ')': tok = Token.RParen; break;
                case '\'':
                    while (lastChar != '\n') GetChar();
                    GetChar();
                    return GetToken();
                case '<':
                    GetChar();
                    if (lastChar == '>') tok = Token.NotEqual;
                    else if (lastChar == '=') tok = Token.LessEqual;
                    else return Token.Less;
                    break;
                case '>':
                    GetChar();
                    if (lastChar == '=') tok = Token.MoreEqual;
                    else return Token.More;
                    break;
                case '"':
                    string str = "";
                    while (GetChar() != '"')
                    {
                        if (lastChar == '\\')
                        {
                            switch (char.ToLower(GetChar()))
                            {
                                case 'n': str += '\n'; break;
                                case 't': str += '\t'; break;
                                case '\\': str += '\\'; break;
                                case '"': str += '"'; break;
                            }
                        }
                        else
                        {
                            str += lastChar;
                        }
                    }
                    Value = new Value(str);
                    tok = Token.Value;
                    break;
                case (char)0:
                    return Token.EOF;
            }

            GetChar();
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
            Pointer = pointer;
            Line = line;
            Column = Column;
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
        Rem,
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
        Equal,
        Less,
        More,
        NotEqual,
        LessEqual,
        MoreEqual,
        Or,
        And,
        Not,

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
