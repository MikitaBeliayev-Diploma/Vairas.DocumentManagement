using System.Linq.Expressions;
using System.Text.Json;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class TestScanner : IFileScanner
{ public string Status { get; set; } = "Clean"; public Task<string> ScanAsync(byte[] bytes, CancellationToken ct) => Task.FromResult(Status); }
