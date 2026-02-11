namespace MeatSpeak.Server.Tls;

using Microsoft.AspNetCore.Http;

public sealed class AcmeChallengeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Http01ChallengeHandler _challengeHandler;

    private const string ChallengePath = "/.well-known/acme-challenge/";

    public AcmeChallengeMiddleware(RequestDelegate next, Http01ChallengeHandler challengeHandler)
    {
        _next = next;
        _challengeHandler = challengeHandler;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/.well-known/acme-challenge"))
        {
            var token = context.Request.Path.Value?.Split('/').LastOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                var response = _challengeHandler.GetResponse(token);
                if (response != null)
                {
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(response);
                    return;
                }
            }

            context.Response.StatusCode = 404;
            return;
        }

        await _next(context);
    }
}
