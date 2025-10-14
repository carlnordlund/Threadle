using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    public class OperationResult<T> : OperationResult
    {
        public T? Value { get; }

        private OperationResult(bool success, string code, string message, T? value)
        : base(success, code, message)
        {
            Value = value;
        }

        public static OperationResult<T> Ok(T value, string message = "Success") =>
        new(true, "OK", message, value);

        public new static OperationResult<T> Fail(string code, string message) =>
            new(false, code, message, default);

        public static OperationResult<T> Fail(OperationResult opResult) => new OperationResult<T>(false, opResult.Code, opResult.Message, default);
    }
}
