using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace GoogleBook.Api.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            HttpResponse? response = context.Response;
            response.ContentType = "application/json";

            var problemDetails = new ProblemDetails
            {
                Instance = context.Request.Path,
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "An error occurred while processing your request",
                Detail = exception.Message
            };

            if (exception is SecurityTokenException or UnauthorizedAccessException)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Unauthorized";
                problemDetails.Detail = "Authentication failed. Please provide a valid token.";
            }
            else if (exception is SecurityTokenExpiredException)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Token Expired";
                problemDetails.Detail = "Your authentication token has expired. Please refresh your token.";
            }
            else if (exception is SecurityTokenInvalidSignatureException)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Invalid Token";
                problemDetails.Detail = "The provided token signature is invalid.";
            }
            else if (exception is SecurityTokenValidationException)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Token Validation Failed";
                problemDetails.Detail = "The provided token failed validation.";
            }
            else if (exception is ArgumentNullException argNullEx &&
                     (argNullEx.ParamName?.Contains("token", StringComparison.OrdinalIgnoreCase) == true ||
                      argNullEx.ParamName?.Contains("authorization", StringComparison.OrdinalIgnoreCase) == true))
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Missing Authentication";
                problemDetails.Detail = "Authentication token is required but was not provided.";
            }
            else if (exception is ArgumentException argEx && argEx.Message.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Invalid Authentication";
                problemDetails.Detail = argEx.Message;
            }
            else if (exception is HttpRequestException httpEx)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Bad Request";
                problemDetails.Detail = httpEx.Message;
            }
            else if (exception is KeyNotFoundException)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                problemDetails.Status = (int)HttpStatusCode.NotFound;
                problemDetails.Title = "Resource Not Found";
                problemDetails.Detail = exception.Message;
            }
            else if (exception is ArgumentException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Invalid Argument";
                problemDetails.Detail = exception.Message;
            }
            else if (exception is InvalidOperationException invalidOpEx &&
                     invalidOpEx.Message.Contains("MetadataAddress", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Configuration Error";
                problemDetails.Detail = "JWT Bearer configuration error: The Authority must use HTTPS or set RequireHttpsMetadata=false for development.";
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                problemDetails.Status = (int)HttpStatusCode.InternalServerError;
                problemDetails.Title = "Internal Server Error";
                problemDetails.Detail = "An unexpected error occurred. Please try again later.";
            }

            string? json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return response.WriteAsync(json);
        }
    }
}
