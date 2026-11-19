using System.Buffers.Text;
using System.Security.Cryptography;
using Glassbox.Domain;

namespace Glassbox.Api;

public sealed class Session(string id)
{
    public string Id { get; } = id;
    // Only touch while holding Lock.
    public List<AgentMessage> History { get; } = [new AgentMessage(Role.System, SystemPrompt.Text)];
    public SemaphoreSlim Lock { get; } = new(1, 1);
    internal DateTimeOffset LastUsed;
}

public sealed class SessionStore(TimeProvider time, TimeSpan ttl, int maxSessions)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _sessions = [];

    public Session GetOrCreate(string? id)
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            if (id is not null && _sessions.TryGetValue(id, out var existing))
            {
                if (now - existing.LastUsed <= ttl) { existing.LastUsed = now; return existing; }
                _sessions.Remove(id);
            }
            if (_sessions.Count >= maxSessions) Evict(now);
            var session = new Session(NewId()) { LastUsed = now };
            _sessions[session.Id] = session;
            return session;
        }
    }

    // A running request keeps its own reference, so dropping a locked session is safe.
    private void Evict(DateTimeOffset now)
    {
        foreach (var k in _sessions.Where(p => now - p.Value.LastUsed > ttl).Select(p => p.Key).ToList())
            _sessions.Remove(k);
        while (_sessions.Count >= Math.Max(1, maxSessions))
        {
            var oldest = _sessions.MinBy(p => p.Value.LastUsed).Key;
            _sessions.Remove(oldest);
        }
    }

    private static string NewId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
}
