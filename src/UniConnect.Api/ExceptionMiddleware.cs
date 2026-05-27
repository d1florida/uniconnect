using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application;

namespace UniConnect.Api;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ForbiddenException ex)
        {
            await WriteError(context, HttpStatusCode.Forbidden, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteError(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Database update failed");
            await WriteError(context, HttpStatusCode.BadRequest, DescribeDbUpdateFailure(ex));
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

    private static string DescribeDbUpdateFailure(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("IX_Vehicles_Vin", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                && message.Contains("Vin", StringComparison.OrdinalIgnoreCase)))
            return "A vehicle with this VIN already exists.";
        if (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            return "This record already exists.";
        if (message.Contains("violates not-null", StringComparison.OrdinalIgnoreCase))
            return "Required fields are missing.";
        if (message.Contains("violates foreign key", StringComparison.OrdinalIgnoreCase))
            return "Related record not found.";
        return "Could not save changes. Check your input and try again.";
    }

    private static bool IsDatabaseUnavailable(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            var message = current.Message;
            if (IsConstraintOrDataError(message))
                return false;

            if (message.Contains("transient failure", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase)
                || message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
                || message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsConstraintOrDataError(string message) =>
        message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
        || message.Contains("violates foreign key", StringComparison.OrdinalIgnoreCase)
        || message.Contains("violates not-null", StringComparison.OrdinalIgnoreCase)
        || message.Contains("violates check", StringComparison.OrdinalIgnoreCase)
        || message.Contains("23505", StringComparison.OrdinalIgnoreCase)
        || message.Contains("23503", StringComparison.OrdinalIgnoreCase)
        || message.Contains("23502", StringComparison.OrdinalIgnoreCase);

    private static async Task WriteError(HttpContext context, HttpStatusCode status, string detail)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        var body = new { title = status.ToString(), status = (int)status, detail };
        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
