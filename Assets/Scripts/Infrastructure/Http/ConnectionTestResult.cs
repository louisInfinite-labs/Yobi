namespace Yobi.Infrastructure.Http
{
    public readonly struct ConnectionTestResult
    {
        public bool IsSuccess { get; }
        public int ItemCount { get; }
        public string ErrorMessage { get; }

        private ConnectionTestResult(bool isSuccess, int itemCount, string errorMessage)
        {
            IsSuccess = isSuccess;
            ItemCount = itemCount;
            ErrorMessage = errorMessage;
        }

        public static ConnectionTestResult Success(int itemCount) => new ConnectionTestResult(true, itemCount, null);

        public static ConnectionTestResult Failure(string errorMessage) => new ConnectionTestResult(false, 0, errorMessage);
    }
}
