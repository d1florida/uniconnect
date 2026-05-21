using System.Net;
using System.Text.Json;

namespace UniConnect.Api;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteError(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex) when (IsDatabaseUnavailable(ex))
        {
            logger.LogWarning(ex, "Database connection failed");
            await WriteError(
                context,
                HttpStatusCode.ServiceUnavailable,
                "Database is not available. Start Docker Desktop, then run: docker compose up -d");
        }
        catch (InvalidOperationException ex)
        {
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            logger.LogWarning(ex, "Database connection failed");
            await WriteError(
                context,
                HttpStatusCode.ServiceUnavailable,
                "Database is not available. Start Docker Desktop, then run: docker compose up -d");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteError(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static bool IsDatabaseUnavailable(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            var name = current.GetType().FullName ?? "";
            if (name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                return true;
            if (current.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static async Task WriteError(HttpContext context, HttpStatusCode status, string detail)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        var body = new { title = status.ToString(), status = (int)status, detail };
        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
