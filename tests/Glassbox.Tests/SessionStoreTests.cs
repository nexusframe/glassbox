extern alias api;
using api::Glassbox.Api;
using Glassbox.Domain;
using Microsoft.Extensions.Time.Testing;

namespace Glassbox.Tests;

public class SessionStoreTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    [Fact]
    public void New_session_starts_with_system_prompt()
    {
        var store = new SessionStore(new FakeTimeProvider(), Ttl, 10);
        var s = store.GetOrCreate(null);
        var first = Assert.Single(s.History);
        Assert.Equal(Role.System, first.Role);
        Assert.Equal(SystemPrompt.Text, first.Text);
    }

    [Fact]
    public void Known_id_returns_same_session()
    {
        var store = new SessionStore(new FakeTimeProvider(), Ttl, 10);
        var a = store.GetOrCreate(null);
        Assert.Same(a, store.GetOrCreate(a.Id));
    }

    [Fact]
    public void Unknown_id_creates_new_session_with_new_id()
    {
        var store = new SessionStore(new FakeTimeProvider(), Ttl, 10);
        var s = store.GetOrCreate("nope");
        Assert.NotEqual("nope", s.Id);
    }

    [Fact]
    public void Session_expires_after_ttl()
    {
        var time = new FakeTimeProvider();
        var store = new SessionStore(time, Ttl, 10);
        var a = store.GetOrCreate(null);
        time.Advance(Ttl + TimeSpan.FromSeconds(1));
        Assert.NotSame(a, store.GetOrCreate(a.Id));
    }

    [Fact]
    public void Use_extends_ttl()
    {
        var time = new FakeTimeProvider();
        var store = new SessionStore(time, Ttl, 10);
        var a = store.GetOrCreate(null);
        time.Advance(TimeSpan.FromMinutes(20));
        Assert.Same(a, store.GetOrCreate(a.Id));
        time.Advance(TimeSpan.FromMinutes(20));
        Assert.Same(a, store.GetOrCreate(a.Id));
    }

    [Fact]
    public void Least_recently_used_session_is_evicted_at_capacity()
    {
        var time = new FakeTimeProvider();
        var store = new SessionStore(time, Ttl, 2);
        var a = store.GetOrCreate(null);
        time.Advance(TimeSpan.FromSeconds(1));
        var b = store.GetOrCreate(null);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Same(a, store.GetOrCreate(a.Id)); // a is now newer than b
        time.Advance(TimeSpan.FromSeconds(1));
        var c = store.GetOrCreate(null);          // evicts b
        Assert.Same(a, store.GetOrCreate(a.Id));
        Assert.Same(c, store.GetOrCreate(c.Id));
        Assert.NotSame(b, store.GetOrCreate(b.Id));
    }

    [Fact]
    public void Evicted_locked_session_keeps_working_for_its_holder()
    {
        var store = new SessionStore(new FakeTimeProvider(), Ttl, 1);
        var a = store.GetOrCreate(null);
        Assert.True(a.Lock.Wait(0));
        store.GetOrCreate(null); // evicts a
        a.History.Add(new AgentMessage(Role.User, "x"));
        a.Lock.Release();
    }

    [Fact]
    public void Ids_are_22_char_base64url()
    {
        var store = new SessionStore(new FakeTimeProvider(), Ttl, 1000);
        var ids = Enumerable.Range(0, 200).Select(_ => store.GetOrCreate(null).Id).ToList();
        Assert.All(ids, id =>
        {
            Assert.Equal(22, id.Length);
            Assert.Matches("^[A-Za-z0-9_-]{22}$", id);
        });
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Concurrent_use_is_safe()
    {
        var store = new SessionStore(TimeProvider.System, Ttl, 50);
        var ids = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 2000, i =>
        {
            var s = store.GetOrCreate(i % 3 == 0 ? null : ids.FirstOrDefault());
            ids.Add(s.Id);
        });
        Assert.NotEmpty(ids);
    }
}
