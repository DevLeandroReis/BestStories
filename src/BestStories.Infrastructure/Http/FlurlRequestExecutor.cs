using BestStories.Application.Exceptions;
using Flurl.Http;
using Microsoft.Extensions.Logging;

namespace BestStories.Infrastructure.Http;

internal static class FlurlRequestExecutor
{
    /// <summary>
    /// Executes a Flurl request and translates HTTP exceptions into domain exceptions.
    /// Use this when a failure must propagate (e.g. fetching a required resource).
    /// </summary>
    internal static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> request,
        ILogger logger,
        string context)
    {
        try
        {
            return await request();
        }
        catch (FlurlHttpTimeoutException ex)
        {
            logger.LogError(ex, "Timeout {Context}", context);
            throw new ExternalApiTimeoutException($"Hacker News API timed out while {context}.", ex);
        }
        catch (FlurlHttpException ex) when (ex.StatusCode == 429)
        {
            logger.LogError(ex, "Rate limited {Context}", context);
            throw new ExternalApiRateLimitedException("Hacker News API is rate limiting requests.", ex);
        }
        catch (FlurlHttpException ex) when (ex.StatusCode is null or >= 500)
        {
            logger.LogError(ex, "API unavailable (HTTP {StatusCode}) {Context}", ex.StatusCode, context);
            throw new ExternalApiUnavailableException("Hacker News API is currently unavailable.", ex);
        }
        catch (FlurlHttpException ex)
        {
            logger.LogError(ex, "API request failed (HTTP {StatusCode}) {Context}", ex.StatusCode, context);
            throw new ExternalApiException("Hacker News API returned an unexpected error.", ex);
        }
    }

    /// <summary>
    /// Executes a Flurl request and returns <paramref name="fallback"/> on any HTTP or network error.
    /// Use this when partial failure is acceptable (e.g. fetching individual items in a batch).
    /// </summary>
    internal static async Task<T?> ExecuteWithFallbackAsync<T>(
        Func<Task<T?>> request,
        ILogger logger,
        string context,
        T? fallback = default)
    {
        try
        {
            return await request();
        }
        catch (FlurlHttpTimeoutException ex)
        {
            logger.LogWarning(ex, "Timeout {Context}", context);
            return fallback;
        }
        catch (FlurlHttpException ex) when (ex.StatusCode == 429)
        {
            logger.LogWarning(ex, "Rate limited {Context}", context);
            return fallback;
        }
        catch (FlurlHttpException ex) when (ex.StatusCode is null or >= 500)
        {
            logger.LogWarning(ex, "API unavailable (HTTP {StatusCode}) {Context}", ex.StatusCode, context);
            return fallback;
        }
        catch (FlurlHttpException ex)
        {
            logger.LogWarning(ex, "API request failed (HTTP {StatusCode}) {Context}", ex.StatusCode, context);
            return fallback;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Unexpected error {Context}", context);
            return fallback;
        }
    }
}
