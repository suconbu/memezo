


# Overview

memezo is a lightweight built-in scripting environment for C#.

* Integrate into your application with just one source code.
* Can attach/access the function/variable from the outside language (C#).
* Support interactive-mode.

# Usage

## Usage in C#

Sample source code for use in your application:
```csharp
using Suconbu.Scripting;

static void Main(string[] args)
{
    var interp = new Memezo.Interpreter();

    // Setup custom functions and variable.
    interp.Functions["beep"] = (arguments) =>
    {
        int freq = (int)arguments[0].Number;
        int duration = (int)arguments[1].Number;
        Console.Beep(freq, duration);
        return Memezo.Value.Zero;
    };
    interp.Functions["print"] = (arguments) =>
    {
        Console.WriteLine(arguments[0].ToString());
        return Memezo.Value.Zero;
    };
    interp.Vars["count"] = new Memezo.Value(10);

    // Run
    string source = $@"
    total = 0
    for i in 1 to count:
      print('i:' + i)
      beep(100 * i, 400)
      total = total + i
    end";
    bool result = interp.Run(source);

    if (result)
    {
        Console.WriteLine("----------");
        // Show variable values.
        foreach (var var in interp.Vars)
        {
            Console.WriteLine($"{var.Key}: {var.Value}");
        }
        // count: 10
        // i: 11
        // total: 55
    }
    else
    {
        Console.WriteLine(interp.LastError);
    }

    Console.ReadKey();
}
```

## Sample

### FizzBuzz

```py
for n = 1 to 31: 
  if n % 3 == 0 and n % 5 == 0:
    "FizzBuzz"
  elif n % 3 == 0:
    "Fizz"
  elif n % 5 == 0:
    "Buzz"
  else:
    n
  end
end
```

### Conditional/Loop statements

The memezo allows several statements styles.  
However, it is recommended to use unified styles in one source code.

```py
# 1. Use colon (Similar to Python).
for n = 1 to 3:
  if n % 2 == 0:
    "if"
  else:
    "else"
  end
end

# 2. Use 'then'/'do' (Similar to BASIC/Ruby).
for n in 1 to 3 do
  if n % 2 == 0 then
    "if"
  else
    "else"
  end
end

# 3. None
n = 1
repeat 3
  if n % 2 == 0
    "if"
  else
    "else"
  end
end
```

# Language reference

## Data types

* Number
* String

### Conversion

```py
> n = 100
> s = str(n) + "explicit"  # Convert explicitly.
> s
'100explicit'

> s = n + "implicit"       # 'n' are convert to String implicitly.
> s
'100implicit'

> n = num("100")
> n
100

> n = num("-12.34")
> n
-12.34

> n = num("100px")
ERROR: InvalidParameter
```

## Literals

### Number literal

```
number    ::= digitpart "." [digitpart]
digitpart ::= digit+
digit     ::=  "0"..."9"
```
### String literal

```
string ::=  "'" stringitem* "'" | '"' stringitem* '"'
stringitem ::= stringchar | stringescapeseq
stringchar ::= <any source character except "\" or newline or the quote>
stringescapeseq ::= "\" ("n" | "r" | "t" | "\" | '"' | "'")
```

## Operators

Operator | Description | Precedence
-|-|-
** | Exponentiation | 1
*  | Multiplication | 2
/  | Division       | 2
// | Floor division | 2
%  | Remainder      | 2
+  | Addition       | 3
-  | Subtraction    | 3
<  | Greater than   | 4
>  | Less than      | 4
<= | Greater than or equal | 4
>= | Less than or equal    | 4
!= | Not equal | 4
== | Equal     | 4
not | Boolean NOT | 5
and | Boolean AND | 6
or  | Boolean OR  | 7

```py
>>> 2 ** 3
8

>>> 3 * 4
12

>>> 5 / 2
2.5

>>> 5 // 2
2

>>> -5 // 2
-3

>>> 5 % 2
1
```

## Statement

### Assignment statement

```
assignment_stmt ::= identifier "=" expression
```

### The `if` statement

```
if_stmt ::= "if" expression [":"|"then"] suite
            ("elif" expression [":"|"then"] suite)*
            ["else" [":"] suite]
            "end"
```

### The `for` statement

```
for_stmt ::= "for" identifier "="|"in" expression "to" expression [":"|"do"] suite
             "end"
```

### The `repeat` statement

```
repeat_stmt ::= "repeat" expression [":"|"do"] suite
                "end"
```

# Function

## Standard

Function | Description | Sample
---------|-------------|-
typeof( v )    | Get a data type name. | typeof( 100 ) -> 'number'
str( n )       | Convert a value to string. | str( -12.3 ) -> "-12.3"
num( s )       | Convert a value to number. | num( "55.5" ) -> 55.5
abs( n )       | n < 0 ? -n : n | abs( "-3" ) -> 3
:

## Random

:
