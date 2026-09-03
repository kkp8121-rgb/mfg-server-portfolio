using System.Net;
using System.Text.Json;
using MFG.Server.DTOs;

namespace MFG.Server.Middleware;

public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "INVALID_ARGUMENT"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "INVALID_OPERATION"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "UNAUTHORIZED"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "NOT_FOUND"),
            _ => (HttpStatusCode.InternalServerError, "SERVER_ERROR")
        };

        _logger.LogError(exception, "[{ErrorCode}] {Message}", errorCode, exception.Message);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Fail(exception.Message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
