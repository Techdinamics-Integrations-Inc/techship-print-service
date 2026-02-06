namespace Techdinamics.Ship.PrintService.Helpers;

public static class RetryHelper
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        int retryCount = 3,
        TimeSpan? retryInterval = null,
        Action<Exception, int>? onRetry = null)
    {
        var exceptions = new List<Exception>();
        var delay = retryInterval ?? TimeSpan.FromSeconds(2);

        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                onRetry?.Invoke(ex, i + 1);
                
                if (i < retryCount - 1)
                {
                    await Task.Delay(delay);
                    // Exponential backoff
                    delay = TimeSpan.FromTicks(delay.Ticks * 2);
                }
            }
        }

        throw new AggregateException($"Operation failed after {retryCount} attempts.", exceptions);
    }

    public static async Task ExecuteWithRetryAsync(
        Func<Task> action,
        int retryCount = 3,
        TimeSpan? retryInterval = null,
        Action<Exception, int>? onRetry = null)
    {
        await ExecuteWithRetryAsync<bool>(async () =>
        {
            await action();
            return true;
        }, retryCount, retryInterval, onRetry);
    }
}
