using System.Collections.ObjectModel;
using Cassandra.Application.Models;
using Cassandra.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cassandra.Presentation.ViewModels;

/// <summary>The "translation model" section of the settings: list of variants with download / switch / delete.</summary>
public sealed partial class ModelsViewModel : ObservableObject, IDisposable
{
    private readonly IModelManager _manager;
    private readonly IUiDispatcher _ui;

    public ModelsViewModel(IModelManager manager, IUiDispatcher ui)
    {
        _manager = manager;
        _ui = ui;
        _manager.Changed += OnChanged;
    }

    public ObservableCollection<ModelOptionViewModel> Options { get; } = [];

    /// <summary>No model is selected: translation cannot work yet.</summary>
    [ObservableProperty]
    private bool _needsModel = true;

    [ObservableProperty]
    private string? _error;

    /// <summary>Raised once after loading when the player has no model, so the HUD can open the settings drawer.</summary>
    public event Action? FirstRunPrompt;

    public async Task InitializeAsync()
    {
        try
        {
            await _manager.InitializeAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return;
        }

        Refresh();
        if (NeedsModel)
        {
            FirstRunPrompt?.Invoke();
        }
    }

    internal void StartDownload(string id)
    {
        Error = null;
        try
        {
            _manager.StartDownload(id);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    internal void Pause() => _manager.Cancel();

    internal void Activate(string id)
    {
        try
        {
            _manager.Activate(id);
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
        }
    }

    internal async Task DeleteAsync(string id) => await _manager.DeleteAsync(id);

    internal void RefreshOption(ModelOptionViewModel option)
    {
        var status = _manager.Statuses.FirstOrDefault(s => s.Variant.Id == option.Id);
        if (status is not null)
        {
            option.Update(status, _manager.ActiveModelId == option.Id, _manager.State);
        }
    }

    private void OnChanged() => _ui.Post(Refresh);

    private void Refresh()
    {
        var statuses = _manager.Statuses;
        if (Options.Count != statuses.Count || Options.Select(o => o.Id).Except(statuses.Select(s => s.Variant.Id)).Any())
        {
            Options.Clear();
            foreach (var status in statuses)
            {
                Options.Add(new ModelOptionViewModel(this, status.Variant));
            }
        }

        foreach (var option in Options)
        {
            RefreshOption(option);
        }

        NeedsModel = _manager.ActiveModelId is null;
    }

    public void Dispose() => _manager.Changed -= OnChanged;
}
