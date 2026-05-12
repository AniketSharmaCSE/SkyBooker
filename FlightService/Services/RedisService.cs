using System.Text.Json;
using StackExchange.Redis;

namespace FlightService.Services;

public class RedisService
{
    private const string SearchKeysSet = "flights:search:keys";
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(2);
    private readonly IDatabase _cache;

    public RedisService(IConnectionMultiplexer redis)
    {
        _cache = redis.GetDatabase();
    }

    public string BuildSearchKey(string? origin, string? destination, DateTime? date)
    {
        var datePart = date.HasValue ? date.Value.Date.ToString("yyyy-MM-dd") : "any";
        return $"flights:search:{Normalize(origin)}:{Normalize(destination)}:{datePart}";
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _cache.StringGetAsync(key);
            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (RedisException)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _cache.StringSetAsync(key, json, SearchCacheTtl);
            await _cache.SetAddAsync(SearchKeysSet, key);
            await _cache.KeyExpireAsync(SearchKeysSet, TimeSpan.FromMinutes(10));
        }
        catch (RedisException)
        {
            // Redis is an optimization. Flight reads/writes should still work if it is unavailable.
        }
    }

    public async Task InvalidateSearchCacheAsync()
    {
        try
        {
            var keys = await _cache.SetMembersAsync(SearchKeysSet);
            if (keys.Length == 0)
                return;

            var redisKeys = keys.Select(key => (RedisKey)key.ToString()).ToArray();
            await _cache.KeyDeleteAsync(redisKeys);
            await _cache.KeyDeleteAsync(SearchKeysSet);
        }
        catch (RedisException)
        {
            // Ignore cache invalidation failures; the database remains the source of truth.
        }
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "any"
            : value.Trim().ToLowerInvariant();
    }
}
