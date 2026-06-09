using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace RuneReader.Classes.Updates;

public enum UpdateState
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    Error,
    NotInstalled,
}

public sealed class UpdateService
{
    private static readonly string UpdateFeedUrl = UpdateServiceConfig.UpdateFeedUrl;

    private readonly UpdateManager? _manager;
    private UpdateInfo? _pendingUpdate;
    private UpdateState _state;
    private string _statusMessage = string.Empty;

    public event Action<UpdateState, string>? StatusChanged;

    public UpdateState State => _state;
    public string StatusMessage => _statusMessage;

    public string? AvailableVersion =>
        _pendingUpdate?.TargetFullRelease?.Version?.ToString();

    public string CurrentVersion =>
        _manager?.CurrentVersion?.ToString() ?? GetAssemblyVersion();

    public UpdateService()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UpdateFeedUrl))
            {
                _state = UpdateState.NotInstalled;
                _manager = null;
                return;
            }

            IUpdateSource source = new SimpleWebSource(UpdateFeedUrl);
            _manager = new UpdateManager(source);
            _state = _manager.IsInstalled ? UpdateState.Idle : UpdateState.NotInstalled;
        }
        catch
        {
            _state = UpdateState.NotInstalled;
            _manager = null;
        }
    }

    public async Task CheckAsync(CancellationToken ct = default)
    {
        if (_manager == null || !_manager.IsInstalled) return;
        if (_state is UpdateState.Checking or UpdateState.Downloading) return;

        SetState(UpdateState.Checking, "Checking for updates...");
        try
        {
            var update = await _manager.CheckForUpdatesAsync().WaitAsync(ct);
            if (update == null)
            {
                _pendingUpdate = null;
                SetState(UpdateState.UpToDate, "Rune Reader is up to date.");
            }
            else
            {
                _pendingUpdate = update;
                SetState(UpdateState.UpdateAvailable,
                    $"Version {update.TargetFullRelease?.Version} is available.");
            }
        }
        catch (OperationCanceledException)
        {
            SetState(UpdateState.Idle, string.Empty);
        }
        catch (Exception ex)
        {
            SetState(UpdateState.Error, $"Update check failed: {ex.Message}");
        }
    }

    public async Task CheckSilentlyAsync(CancellationToken ct = default)
    {
        if (_manager == null || !_manager.IsInstalled) return;
        if (_state is UpdateState.Checking or UpdateState.Downloading or UpdateState.ReadyToInstall) return;

        try
        {
            var update = await _manager.CheckForUpdatesAsync().WaitAsync(ct);
            if (update == null)
            {
                _pendingUpdate = null;
                SetState(UpdateState.UpToDate, "Rune Reader is up to date.");
            }
            else
            {
                _pendingUpdate = update;
                SetState(UpdateState.UpdateAvailable,
                    $"Version {update.TargetFullRelease?.Version} is available.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (_state == UpdateState.Checking)
                SetState(UpdateState.Idle, string.Empty);
        }
    }

    public async Task DownloadAndStageAsync(Action<int>? onProgress = null, CancellationToken ct = default)
    {
        if (_manager == null || _pendingUpdate == null) return;
        if (_state != UpdateState.UpdateAvailable) return;

        SetState(UpdateState.Downloading, "Downloading update...");
        try
        {
            await _manager.DownloadUpdatesAsync(_pendingUpdate, onProgress).WaitAsync(ct);
            SetState(UpdateState.ReadyToInstall,
                $"Version {_pendingUpdate.TargetFullRelease?.Version} ready. Restart to install.");
        }
        catch (OperationCanceledException)
        {
            SetState(UpdateState.UpdateAvailable,
                $"Download cancelled. Version {_pendingUpdate.TargetFullRelease?.Version} is available.");
        }
        catch (Exception ex)
        {
            SetState(UpdateState.Error, $"Download failed: {ex.Message}");
        }
    }

    public void RestartAndInstall()
    {
        if (_manager == null || _state != UpdateState.ReadyToInstall) return;
        if (_pendingUpdate != null)
            _manager.ApplyUpdatesAndRestart(_pendingUpdate);
    }

    private void SetState(UpdateState state, string message)
    {
        _state = state;
        _statusMessage = message;
        StatusChanged?.Invoke(state, message);
    }

    private static string GetAssemblyVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "unknown";
    }
}
