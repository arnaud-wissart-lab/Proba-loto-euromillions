using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Web.Options;

namespace Web.Security;

public sealed class AdminBasicAuthMiddleware(
    RequestDelegate next,
    IOptionsMonitor<AdminOptions> adminOptions,
    ILogger<AdminBasicAuthMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var options = adminOptions.CurrentValue;
        if (!options.ProtectUi)
        {
            await next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.WebPassword))
        {
            logger.LogWarning("Protection UI admin active mais mot de passe absent. Acces refuse.");
            await RejectAsync(context);
            return;
        }

        if (!TryValidateCredentials(context, options.WebUsername, options.WebPassword))
        {
            await RejectAsync(context);
            return;
        }

        await next(context);
    }

    private static bool TryValidateCredentials(HttpContext context, string expectedUser, string expectedPassword)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var authorizationValue))
        {
            return false;
        }

        var header = authorizationValue.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var encodedCredentials = header["Basic ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(encodedCredentials))
        {
            return false;
        }

        string rawCredentials;
        try
        {
            rawCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
        }
        catch (FormatException)
        {
            return false;
        }

        var separatorIndex = rawCredentials.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var providedUser = rawCredentials[..separatorIndex];
        var providedPassword = rawCredentials[(separatorIndex + 1)..];

        return ConstantTimeEquals(providedUser, expectedUser)
               && ConstantTimeEquals(providedPassword, expectedPassword);
    }

    private static bool ConstantTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
               && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static async Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"Administration\", charset=\"UTF-8\"";
        await context.Response.WriteAsync("Authentification admin requise.");
    }
}
