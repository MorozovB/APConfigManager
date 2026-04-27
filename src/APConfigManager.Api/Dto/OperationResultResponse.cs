namespace APConfigManager.Api.Dto
{
    /// <summary>
    /// Unified response for all operations (flash, erase, params).
    /// </summary>
    public class OperationResultResponse
    {
        public bool Success { get; set; }

        public string? Message { get; set; }

        public object? Data { get; set; }
    }
}
