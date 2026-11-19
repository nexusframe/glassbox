using System.Text.Json;
using Glassbox.Domain;

namespace Glassbox.Tests;

public class TraceEventJsonTests
{
    [Fact]
    public void Serializes_with_wire_names()
    {
        var e = new TraceEvent(TraceType.AnswerDelta, "e1", "r1", 5) { Text = "Hi" };
        var json = JsonSerializer.Serialize(e, TraceJson.Options);
        Assert.Equal("""{"type":"answer_delta","id":"e1","runId":"r1","startMs":5,"text":"Hi"}""", json);
    }
}
