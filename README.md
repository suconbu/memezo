


# Overview

memezo is a lightweight built-in scripting environment for C#.

* Dynamic typing
* Support interactive-mode

# Usage

Sample source code for use in your application:
```csharp
using Suconbu.Scripting;

static void Main(string[] args)
{
  var interp = new Memezo.Interpreter();

  // Setup custom functions
  interp.Functions["sleep"] = (args) =>
  {
    int duration = args[0].Number;
    await Task.Delay(duration);
  };
  interp.Functions["beep"] = (args) =>
  {
    int freq = args[0].Number;
    int duration = args[1].Number;
    Console.Beep(freq, duration);
  }
  interp.Vars["count"] = 10;
  
  // Run
  string source = $@"
    for i = 1 to count:
      beep(100 * i, 500)
      sleep(100)
      print('i:' + i)
    end
    result = 1";
  bool result = interp.Run(source);

  if (result)
  {
    // Show variable values.
    foreach (var var in interp.Vars)
    {
      Console.WriteLine($"{var.Key}: {var.Value}");
    }
    // count: 10
    // i: 11
    // result: 1
  }
  else
  {
    Console.WriteLine(interp.Error);
  }

  return 0;
}
```

# Language reference

## Sample:FizzBuzz

```py
for n = 1 to 31: 
  if n % 3 == 0 and n % 5 == 0:
    print("FizzBuzz")
  elif n % 3 == 0:
    print("Fizz")
  elif n % 5 == 0:
    print("Buzz")
  else:
    print(n)
  end
end
```

## Data types

* String
* Number

### Conversion

```py
>>> s = "100"
>>> n = Number(a) + 10
>>> print(n)
110

>>> n = 100
>>> s = String(n) + "p"  # Convert explicitly.
>>> s = n + "p"          # 'n' are convert to String implicitly.
>>> print(n)
100p

>>> n = Number("100p")
>>> print(n)
NaN
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
if_stmt ::= "if" expression ":" suite
            ("elif" expression ":" suite)*
            ["else" ":" suite]
            "end"
```

### The `for` statement

```
for_stmt ::=  "for" assignment_stmt "to" expression ":" suite
              "end"
```
