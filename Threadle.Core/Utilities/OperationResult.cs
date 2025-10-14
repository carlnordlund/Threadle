using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    public class OperationResult
    {
        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }

        protected OperationResult(bool success, string code, string message)
        {
            Success = success;
            Code = code;
            Message = message;
        }

        public static OperationResult Ok(string message = "Success") => new OperationResult(true, "OK", message);

        public static OperationResult Fail(string code, string message) => new OperationResult(false, code, message);

        public override string ToString() => $"{Code}: {Message}";
    }
}
