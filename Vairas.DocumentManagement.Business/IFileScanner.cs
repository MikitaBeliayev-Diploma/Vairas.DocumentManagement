using System.Linq.Expressions;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public interface IFileScanner { Task<string> ScanAsync(byte[] bytes, CancellationToken ct); }
