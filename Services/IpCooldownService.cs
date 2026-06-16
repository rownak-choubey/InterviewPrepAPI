using System.Collections.Concurrent;

namespace InterviewPrepAPI.Services;

public interface IIpCooldownService
{
    bool IsAllowed(string ip, string action, int cooldownSeconds = 60);
    TimeSpan? GetRemainingCooldown(string ip, string action, int cooldownSeconds = 60);
}

public class InMemoryIpCooldownService : IIpCooldownService
{
    private readonly ConcurrentDictionary<string, DateTime> _lastRequest = new();
    private readonly ILogger<InMemoryIpCooldownService> _logger;

    public InMemoryIpCooldownService(ILogger<InMemoryIpCooldownService> logger)
    {
        _logger = logger;
    }

    public bool IsAllowed(string ip, string action, int cooldownSeconds = 60)
    {
        var key = $"{ip}:{action}";
        var now = DateTime.UtcNow;

        if (_lastRequest.TryGetValue(key, out var lastTime))
        {
            var elapsed = (now - lastTime).TotalSeconds;
            if (elapsed < cooldownSeconds)
            {
                var remaining = (int)(cooldownSeconds - elapsed);
                _logger.LogDebug("IP cooldown active for {Ip}:{Action}, {Seconds}s remaining",
                    ip, action, remaining);
                return false;
            }
        }

        _lastRequest[key] = now;
        return true;
    }

    public TimeSpan? GetRemainingCooldown(string ip, string action, int cooldownSeconds = 60)
    {
        var key = $"{ip}:{action}";
        if (_lastRequest.TryGetValue(key, out var lastTime))
        {
            var elapsed = (DateTime.UtcNow - lastTime).TotalSeconds;
            if (elapsed < cooldownSeconds)
                return TimeSpan.FromSeconds(cooldownSeconds - elapsed);
        }
        return null;
    }
}
