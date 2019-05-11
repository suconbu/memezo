using System;
using System.Collections.Generic;

namespace Suconbu.Scripting.Memezo
{
    class StandardLibrary : IFunctionLibrary
    {
        public string Name { get { return "standard"; } }

        public IEnumerable<KeyValuePair<string, Function>> GetFunctions()
        {
            return new Dictionary<string, Function>()
            {
                { "typeof", TypeOf }, { "str", Str }, { "num", Num },
                { "abs", Abs }, { "min", Min }, { "max", Max },
                { "floor", Floor }, { "ceil", Ceil }, { "truncate", Truncate }, { "round", Round }
            };
        }

        public static Value TypeOf(List<Value> args)
        {
            if (args.Count != 1) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            var value = args[0];
            return new Value(
                (value.Type == DataType.Number) ? "number" :
                (value.Type == DataType.String) ? "string" :
                throw new InternalErrorException(ErrorType.InvalidDataType));
        }

        // Str(v) : Convert a value to string.
        public static Value Str(List<Value> args)
        {
            if (args.Count != 1) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            return new Value(args[0].ToString());
        }

        // Num(v) : Convert a value to number.
        public static Value Num(List<Value> args)
        {
            if (args.Count != 1) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            var value = args[0];
            return new Value(
                (value.Type == DataType.String) ? (double.TryParse(value.String, out var n) ? n : throw new InternalErrorException(ErrorType.InvalidParameter)) :
                (value.Type == DataType.Number) ? value.Number :
                throw new InternalErrorException(ErrorType.InvalidDataType));
        }

        // Abs(n) -> : n < 0 ? -n : n
        public static Value Abs(List<Value> args)
        {
            if (args.Count != 1) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            if (args[0].Type != DataType.Number) throw new InternalErrorException(ErrorType.InvalidDataType);
            return new Value(Math.Abs(args[0].Number));
        }

        // Min(n1, n2[, ...]) : Get a minimum value in arguments.
        public static Value Min(List<Value> args)
        {
            if (args.Count < 2) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            var min = double.MaxValue;
            foreach (var arg in args)
            {
                if (arg.Type != DataType.Number) throw new InternalErrorException(ErrorType.InvalidDataType);
                min = Math.Min(min, arg.Number);
            }
            return new Value(min);
        }

        // Max(n1, n2[, ...]) : Get a maximum value in arguments.
        public static Value Max(List<Value> args)
        {
            if (args.Count < 2) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            var max = double.MinValue;
            foreach (var arg in args)
            {
                if (arg.Type != DataType.Number) throw new InternalErrorException(ErrorType.InvalidDataType);
                max = Math.Max(max, arg.Number);
            }
            return new Value(max);
        }

        // Floor(n) : Largest integer less than or equal to the specified number.
        public static Value Floor(List<Value> args)
        {
            if (args.Count != 1) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            if (args[0].Type != DataType.Number) throw new InternalErrorException(ErrorType.InvalidDataType);
            return new Value(Math.Floor(args[0].Number));
        }

        // Ceil(n) : Smallest integer greater than or equal to the specified number.
        public static Value Ceil(List<Value> args)
        {
            if (args.Count != 1) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            if (args[0].Type != DataType.Number) throw new InternalErrorException(ErrorType.InvalidDataType);
            return new Value(Math.Ceiling(args[0].Number));
        }

        // Truncate(n) : Get a integral part of a specified number.
        public static Value Truncate(List<Value> args)
        {
            if (args.Count != 1) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            if (args[0].Type != DataType.Number) throw new InternalErrorException(ErrorType.InvalidDataType);
            return new Value(Math.Truncate(args[0].Number));
        }

        // Round(n) Rounds a specified number to the nearest even integer.
        public static Value Round(List<Value> args)
        {
            if (args.Count != 1) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            if (args[0].Type != DataType.Number) throw new InternalErrorException(ErrorType.InvalidDataType);
            return new Value(Math.Round(args[0].Number));
        }
    }
}
