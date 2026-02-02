using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Represents the outcome of several methods in Threadle, indicating success or failure.
    /// When successful, the returned object also contains a return value.
    /// Provides a consistent way to return success/fail info, messages and potential error
    /// codes instead of using exceptions. If a method is not returning a value, use the
    /// <see cref="OperationResult"/> class instead, from which this class inherits.
    /// </summary>
    public class OperationResult<T> : OperationResult
    {
        #region Properties
        /// <summary>
        /// Generic nullable object that could be returned with the OperationResult object.
        /// </summary>
        public T? Value { get; }
        #endregion


        #region Constructor
        /// <summary>
        /// The class-internal protected constructor for creating an OperationResult object with
        /// a potential generic return value T.
        /// Used by the public factory methods below.
        /// </summary>
        /// <param name="success">Boolean indicating whether the operation was successful (true) or failed (false).</param>
        /// <param name="code">A short code string for the operation status.</param>
        /// <param name="message">A message describing how the operation went, including what might have gone wrong.</param>
        /// <param name="value">A generic nullable return value.</param>
        protected OperationResult(bool success, string code, string message, T? value)
        : base(success, code, message)
        {
            Value = value;
        }

        /// <summary>
        /// Factory method to create an OperationResult object for successful operations, including a return value
        /// </summary>
        /// <param name="value">The generic return value to include.</param>
        /// <param name="message">A message about the successful operation (defaults to 'Success').</param>
        /// <returns>An <see cref="OperationResult{T}"/> object indicating success, with an attached value of type T.</returns>
        public static OperationResult<T> Ok(T value, string message = "") => new(true, "OK", message, value);

        /// <summary>
        /// Factory method to create an OperationResult object for failed operations, including a default (null) value.
        /// </summary>
        /// <param name="code">A short code string for the type of failure that happened.</param>
        /// <param name="message">A message describing in what way the operation went wrong.</param>
        /// <returns>An <see cref="OperationResult{T}"/> object indicating success, with a default (null-like) value of type T.</returns>
        public new static OperationResult<T> Fail(string code, string message) =>
            new(false, code, message, default);

        /// <summary>
        /// Factory method to create an OperationResult object for a failed operation, reusing the code and message from
        /// an existing OperationResult. Useful for propagating a failed operation further up the call chain.
        /// </summary>
        /// <param name="opResult">The existing <see cref="OperationResult"/> object that is to be propagated.</param>
        /// <returns>An <see cref="OperationResult{T}"/> object indicating success, with a default (null-like) value of type T.</returns>
        public static OperationResult<T> Fail(OperationResult opResult) => new OperationResult<T>(false, opResult.Code, opResult.Message, default);
        #endregion
    }
}
