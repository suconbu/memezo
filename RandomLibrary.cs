using Suconbu.Scripting.Memezo;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Suconbu.Scripting.Memezo
{
    class RandomLibrary : IFunctionLibrary
    {
        static readonly Random random = new Random();

        public string Name { get { return "random"; } }

        public IEnumerable<KeyValuePair<string, Function>> GetFunctions()
        {
            return new Dictionary<string, Function>()
            {
                { "random", Random }, { "uniform", Uniform }, { "randrange", RandRange }, { "randint", RandInt }
            };
        }

        [Description("Random() : 0.0 <= {return} < 1.0")]
        public static Value Random(List<Value> args)
        {
            if (args.Count != 0) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            return new Value(RandomLibrary.random.NextDouble());
        }

        [Description("Uniform(min, max) : min <= {return} <= max")]
        public static Value Uniform(List<Value> args)
        {
            if (args.Count != 2) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            var min = (args[0].Type == DataType.Number) ? args[0].Number : throw new InternalErrorException(ErrorType.InvalidDataType);
            var max = (args[1].Type == DataType.Number) ? args[1].Number : throw new InternalErrorException(ErrorType.InvalidDataType);
            if (min > max) throw new InternalErrorException(ErrorType.InvalidParameter);
            return new Value(RandomLibrary.random.NextDouble() * (max - min) + min);
        }

        [Description("RandRange(min, max[, step]) : min <= {return} < max")]
        public static Value RandRange(List<Value> args)
        {
            if (args.Count != 2 && args.Count != 3) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            var min = (args[0].Type == DataType.Number) ? args[0].Number : throw new InternalErrorException(ErrorType.InvalidDataType);
            var max = (args[1].Type == DataType.Number) ? args[1].Number : throw new InternalErrorException(ErrorType.InvalidDataType);
            var range = (int)max - (int)min;
            if (range <= 0) throw new InternalErrorException(ErrorType.InvalidParameter);
            var step = 1.0;
            if (args.Count == 3)
            {
                step = (args[2].Type == DataType.Number) ? args[2].Number : throw new InternalErrorException(ErrorType.InvalidDataType);
                if (step != (int)step) throw new InternalErrorException(ErrorType.InvalidParameter);
                if (step < 1.0) throw new InternalErrorException(ErrorType.InvalidParameter);
                range = (int)Math.Ceiling((double)range / step);
            }
            return new Value(min + RandomLibrary.random.Next(range) * step);
        }

        [Description("RandInt(min, max) : min <= {return} <= max")]
        public static Value RandInt(List<Value> args)
        {
            if (args.Count != 2) throw new InternalErrorException(ErrorType.InvalidNumberOfArguments);
            var min = (args[0].Type == DataType.Number) ? args[0].Number : throw new InternalErrorException(ErrorType.InvalidDataType);
            var max = (args[1].Type == DataType.Number) ? args[1].Number : throw new InternalErrorException(ErrorType.InvalidDataType);
            if (min > max) throw new InternalErrorException(ErrorType.InvalidParameter);
            return new Value(RandomLibrary.random.Next((int)min, (int)max + 1));
        }
    }
}
