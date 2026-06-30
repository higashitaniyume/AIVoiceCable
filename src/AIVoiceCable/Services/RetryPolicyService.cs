using System.Net;
using System.Net.Http;
using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class RetryPolicyService
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetryOptions options,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, options.MaxAttempts);
        var delay = options.InitialDelay;

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation(cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < attempts)
            {
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex) when (!ShouldRetry(ex.StatusCode) || attempt >= attempts)
            {
                throw;
            }
            catch (HttpRequestException ex) when (ShouldRetry(ex.StatusCode))
            {
                if (attempt >= attempts)
                {
                    throw;
                }
            }
            catch (TimeoutException) when (attempt >= attempts)
            {
                throw;
            }
            catch (TimeoutException)
            {
            }

            await Task.Delay(delay, cancellationToken);
            var nextMs = Math.Min(delay.TotalMilliseconds * 1.8, options.MaxDelay.TotalMilliseconds);
            delay = TimeSpan.FromMilliseconds(nextMs);
        }
    }

    public static bool ShouldRetry(HttpStatusCode? statusCode)
    {
        return statusCode is null
            || statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }
}
