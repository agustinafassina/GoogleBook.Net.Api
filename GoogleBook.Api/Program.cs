using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using GoogleBook.Api.Middleware;
using GoogleBook.Api.Validators;
using GoogleBook.Repository;
using GoogleBook.Services;

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);
ConfigurationManager? configuration = builder.Configuration;

// Render (and most PaaS) inject PORT. Bind on 0.0.0.0 so the reverse proxy can reach Kestrel.
string port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("GameApiKey", new OpenApiSecurityScheme
    {
        Description = "API key required by clients. Send it in the X-API-Key header.",
        Name = "X-API-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "GameApiKey"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "GameApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Dependency Injection: Repositories (then Services)
builder.Services.AddRepositories(configuration);
builder.Services.AddApplicationServices();

// Request validators (FluentValidation)
builder.Services.AddValidatorsFromAssemblyContaining<GoogleBooksIngestRequestValidator>();

// CORS
string[]? allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? (builder.Environment.IsDevelopment() ? new[] { "*" } : Array.Empty<string>());

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        if (builder.Environment.IsDevelopment() && allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer("Auth0App1", options =>
{
    options.Audience = configuration["Auth0App1:Audience"] ?? Environment.GetEnvironmentVariable("Auth0App1.Audience");
    options.Authority = configuration["Auth0App1:Issuer"] ?? Environment.GetEnvironmentVariable("Auth0App1.Issuer");
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = configuration["Auth0App1:Issuer"] ?? Environment.GetEnvironmentVariable("Auth0App1.Issuer")
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Exception? exception = context.Exception;
            if (exception is SecurityTokenExpiredException)
                throw new SecurityTokenExpiredException("Token has expired", exception);
            if (exception is SecurityTokenInvalidSignatureException)
                throw new SecurityTokenInvalidSignatureException("Invalid token signature", exception);
            if (exception is SecurityTokenValidationException)
                throw new SecurityTokenValidationException("Token validation failed", exception);
            throw new UnauthorizedAccessException("Authentication failed", exception);
        },
        OnChallenge = context =>
        {
            if (string.IsNullOrEmpty(context.Request.Headers.Authorization))
                throw new UnauthorizedAccessException("Authorization header is missing");
            context.HandleResponse();
            throw new UnauthorizedAccessException("Authentication challenge failed");
        }
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddHealthChecks();

WebApplication? app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
// Skip HTTPS redirection behind Render/load balancers (TLS terminates at the proxy).
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();

// Swagger stays available in Production so the game client / Render deploy can be explored.
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
