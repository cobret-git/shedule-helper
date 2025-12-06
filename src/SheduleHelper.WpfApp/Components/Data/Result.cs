namespace SheduleHelper.WpfApp.Components.Data
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail.
    /// Provides a structured way to handle operation outcomes without throwing exceptions.
    /// </summary>
    public class Result
    {
        #region Properties

        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether the operation failed.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Gets the error message if the operation failed.
        /// Null if the operation was successful.
        /// </summary>
        public string? ErrorMessage { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Result"/> class.
        /// </summary>
        /// <param name="isSuccess">Indicates whether the operation was successful.</param>
        /// <param name="errorMessage">The error message if the operation failed.</param>
        protected Result(bool isSuccess, string? errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <returns>A successful <see cref="Result"/> instance.</returns>
        public static Result Success() => new(true, null);

        /// <summary>
        /// Creates a failed result with the specified error message.
        /// </summary>
        /// <param name="errorMessage">The error message describing why the operation failed.</param>
        /// <returns>A failed <see cref="Result"/> instance.</returns>
        public static Result Failure(string errorMessage) => new(false, errorMessage);

        #endregion
    }

    /// <summary>
    /// Represents the result of an operation that returns data and can either succeed or fail.
    /// Provides a structured way to handle operation outcomes with return values without throwing exceptions.
    /// </summary>
    /// <typeparam name="T">The type of data returned by the operation.</typeparam>
    public class Result<T> : Result
    {
        #region Properties

        /// <summary>
        /// Gets the data returned by the operation if successful.
        /// Default value of T if the operation failed.
        /// </summary>
        public T? Data { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Result{T}"/> class.
        /// </summary>
        /// <param name="isSuccess">Indicates whether the operation was successful.</param>
        /// <param name="data">The data returned by the operation.</param>
        /// <param name="errorMessage">The error message if the operation failed.</param>
        private Result(bool isSuccess, T? data, string? errorMessage)
            : base(isSuccess, errorMessage)
        {
            Data = data;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a successful result with the specified data.
        /// </summary>
        /// <param name="data">The data returned by the successful operation.</param>
        /// <returns>A successful <see cref="Result{T}"/> instance containing the data.</returns>
        public static Result<T> Success(T data) => new(true, data, null);

        /// <summary>
        /// Creates a failed result with the specified error message.
        /// </summary>
        /// <param name="errorMessage">The error message describing why the operation failed.</param>
        /// <returns>A failed <see cref="Result{T}"/> instance.</returns>
        public static new Result<T> Failure(string errorMessage) => new(false, default, errorMessage);

        #endregion
    }
}
