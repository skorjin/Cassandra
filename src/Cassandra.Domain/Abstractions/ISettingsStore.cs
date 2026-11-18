using Cassandra.Domain.Models;
namespace Cassandra.Domain.Abstractions;

public interface ISettingsStore
{
    AppSettings Load();
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
