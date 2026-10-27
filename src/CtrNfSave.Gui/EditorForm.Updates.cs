using CtrNfSave.Updates;

namespace CtrNfSave.Gui;

internal sealed partial class EditorForm
{
    private readonly UpdateService updates = new();
    private readonly CancellationTokenSource updateLifetime = new();
    private readonly SemaphoreSlim updateGate = new(1, 1);
    private bool restartingForUpdate;
    private void ShowAbout()
    {
        using var dialog = new AboutDialog(updates, owner => CheckUpdates(owner, false)); dialog.ShowDialog(this);
    }
    private async Task CheckUpdates(Form owner, bool automatic)
    {
        if (IsDisposed || owner.IsDisposed || automatic && !updates.Settings.CheckUpdatesAtStartup) return;
        try { await updateGate.WaitAsync(updateLifetime.Token); }
        catch (OperationCanceledException) { return; }
        try
        {
            if (IsDisposed || owner.IsDisposed || updateLifetime.IsCancellationRequested) return;
            SetStatus("checkingUpdates");
            var release = await updates.Check(updateLifetime.Token);
            if (IsDisposed || owner.IsDisposed || updateLifetime.IsCancellationRequested) return;
            if (!UpdateVersion.TryParse(UpdateService.Version, out var current)) throw new InvalidDataException("Invalid running version.");
            if (release is null || release.Version.CompareTo(current) <= 0)
            {
                SetStatus("upToDate");
                if (!automatic) MessageBox.Show(owner, L.T("upToDate"), L.T("checkUpdates"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SetStatus("updateAvailable");
            if (automatic && (release.Version.ToString() == updates.Settings.SkippedVersion || bulkWorking || OwnedForms.Length != 0)) return;
            using var dialog = new UpdateDialog(release, automatic); dialog.ShowDialog(owner);
            if (dialog.SkipRequested) updates.SetSettings(updates.Settings with { SkippedVersion = release.Version.ToString() });
            if (!dialog.InstallRequested || bulkWorking || !CanLeave()) return;
            string? script = await UpdateDialog.Prepare(owner, release);
            if (script is null) return;
            UpdateService.LaunchInstaller(script);
            restartingForUpdate = true;
            if (owner != this) owner.Close();
            BeginInvoke((Action)Close);
        }
        catch (OperationCanceledException) when (updateLifetime.IsCancellationRequested) { }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or KeyNotFoundException)
        {
            if (IsDisposed || updateLifetime.IsCancellationRequested) return;
            SetStatus("updateFailed");
            if (!automatic) MessageBox.Show(owner.IsDisposed ? this : owner, L.T("updateFailed"), L.T("checkUpdates"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { updateGate.Release(); }
    }
}
