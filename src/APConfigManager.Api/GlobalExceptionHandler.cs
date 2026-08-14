using APConfigManager.Api.Dto;
using APConfigManager.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace APConfigManager.Api
{
    /// <summary>
    /// Centralized exception → HTTP mapping. Registered via AddExceptionHandler.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            this.logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var (status, message) = Map(exception);

            if (status >= 500)
            {
                logger.LogError(exception, "Unhandled exception");
            }
            else
            {
                logger.LogWarning("Request failed ({Status}): {Message}", status, exception.Message);
            }

            httpContext.Response.StatusCode = status;
            await httpContext.Response.WriteAsJsonAsync(
                new OperationResultResponse { Success = false, Message = message },
                cancellationToken);

            return true; 
        }

        private static (int status, string message) Map(Exception ex) => ex switch
        {
            SessionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            PortInUseException => (StatusCodes.Status409Conflict, ex.Message),
            SessionLimitReachedException => (StatusCodes.Status429TooManyRequests, ex.Message),

            KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            FileNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            ApjParseException => (StatusCodes.Status400BadRequest, ex.Message),
            ParamParseException => (StatusCodes.Status400BadRequest, ex.Message),
            DeviceConnectionException => (StatusCodes.Status503ServiceUnavailable, ex.Message),
            BootloaderException => (StatusCodes.Status502BadGateway, ex.Message),

            SessionException => (StatusCodes.Status409Conflict, ex.Message),

            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };
    }
}
