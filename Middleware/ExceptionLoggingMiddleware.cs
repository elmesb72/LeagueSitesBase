using Microsoft.AspNetCore.Diagnostics;

public class ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, LeagueSitesContext dbContext)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            long uid = -1;
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var uidClaim = context.User.Claims.FirstOrDefault(c => c.Type == "UserID");
                if (uidClaim is not null && long.TryParse(uidClaim.Value, out var parsed))
                    uid = parsed;
            }

            try
            {
                dbContext.Events.Add(Event.Log(
                    EventType.Error, uid,
                    context.Request.Path.ToString(),
                    ex.GetType().Name,
                    ex.ToString()));
                await dbContext.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                logger.LogError(logEx, "Failed to write exception event to database");
            }

            // Return a JSON error for API routes, otherwise rethrow
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "An internal error occurred." });
            }
            else
            {
                throw;
            }
        }
    }
}
