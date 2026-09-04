using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GoogleBook.Api.Security
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ApiKeyAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public const string HeaderName = "X-API-Key";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            IConfiguration? configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            string? expectedApiKey = configuration["GameApi:ApiKey"]
                ?? Environment.GetEnvironmentVariable("LIBRARIAN_CHALLENGE_GAME_API_KEY");

            if (string.IsNullOrWhiteSpace(expectedApiKey))
            {
                context.Result = new ObjectResult(new { message = "Game API key is not configured." })
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedApiKey) ||
                !ApiKeysMatch(providedApiKey.ToString(), expectedApiKey))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Invalid or missing API key." });
            }
        }

        private static bool ApiKeysMatch(string providedApiKey, string expectedApiKey)
        {
            byte[] providedBytes = Encoding.UTF8.GetBytes(providedApiKey);
            byte[]? expectedBytes = Encoding.UTF8.GetBytes(expectedApiKey);

            return providedBytes.Length == expectedBytes.Length &&
                CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
    }
}
