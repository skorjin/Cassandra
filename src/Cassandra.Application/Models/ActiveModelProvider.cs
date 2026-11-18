using Cassandra.Application.Settings;
using Cassandra.Domain.Abstractions;

namespace Cassandra.Application.Models;

public sealed class ActiveModelProvider(ISettingsService settings) : IActiveModelProvider
{
    public string? ActiveModelId => settings.Current.ActiveModelId;
}
