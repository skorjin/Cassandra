using Cassandra.Domain.Models;

namespace Cassandra.Application.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    event Action<AppSettings>? Changed;
    void Update(Func<AppSettings, AppSettings> change);
}
