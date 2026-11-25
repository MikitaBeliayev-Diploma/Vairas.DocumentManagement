using System.Linq.Expressions;
using System.Text.Json;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Time { get; set; } = new(2026,11,24,12,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Time;
}
