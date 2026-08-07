namespace Aimon.Infrastructure.Tests.TestSupport;

internal static class AsyncStream
{
    public static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source)
            items.Add(item);

        return items;
    }
}
