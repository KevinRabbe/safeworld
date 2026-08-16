using System.Windows;
using SharedWorlds.Core.Abstractions;
using SharedWorlds.Core.Domain;
using SharedWorlds.Infrastructure.Recovery;

namespace SharedWorlds.Desktop;

public partial class MainWindow
{
    private async void RetryPendingSyncButton_Click(object sender, RoutedEventArgs e)
    {
        var world = _selectedWorld;
        if (world is null || !TryGetAdapter(world.GameAdapterId, out var adapter))
        {
            return;
        }

        if (world.SharingMode == WorldSharingMode.Shared && !HasAuthoritativeRuntimeForWorld(world))
        {
            StatusText.Text =
                "This shared World has no usable peer authority on this launch. Start SafeWorld with Steam available before retrying recovery.";
            return;
        }

        await RunOperationAsync(
            $"Reconciling recovery for {world.Name}...",
            async () =>
            {
                try
                {
                    var updated = await RetryPendingRecoveryCoreAsync(world, adapter);
                    _selectedWorld = updated;
                    await RefreshUnifiedWorldsAsync(updated.Id, preserveStatus: true);
                    StatusText.Text =
                        $"Recovery for '{updated.Name}' is resolved at canonical revision {updated.CurrentStateRevisionId}.";
                }
                finally
                {
                    await RefreshResponsibilityAfterRecoveryAsync();
                }
            });
    }

    private async Task<World> RetryPendingRecoveryCoreAsync(
        World world,
        IGameAdapter adapter,
        GameInstallation? knownInstallation = null)
    {
        var record = (await _workspaceRecoveryStore.ListAsync())
            .Where(candidate =>
                candidate.WorldId == world.Id &&
                candidate.Status == WorkspaceRecoveryStatus.RecoveryPending)
            .OrderBy(candidate => candidate.CreatedAt)
            .ThenBy(candidate => candidate.Id.ToString(), StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "This responsibility is not a pending recovery. SafeWorld left its evidence untouched for the appropriate recovery path.");

        var installation = knownInstallation ?? await GetReadyInstallationForRecoveryRecordAsync(
            world,
            adapter,
            record);

        if (_peerWorldIds.Contains(world.Id))
        {
            var peer = RequirePeerRuntime(world);
            var peerRecovery = new LocalPendingWorkspaceRecoveryService(
                peer.Storage,
                peer.SessionCoordinator,
                _workspaceRecoveryStore,
                _localManagedSessionGate);
            return await peerRecovery.RetryAsync(
                world.Id,
                adapter,
                installation,
                peer.User);
        }

        if (world.SharingMode == WorldSharingMode.Shared)
        {
            throw new InvalidOperationException(
                "A shared World can never use local recovery authority. Its canonical peer authority must be available before recovery can continue.");
        }

        var localRecovery = new LocalPendingWorkspaceRecoveryService(
            _storage,
            _localSessionCoordinator,
            _workspaceRecoveryStore,
            _localManagedSessionGate);
        return await localRecovery.RetryAsync(
            world.Id,
            adapter,
            installation,
            GetLocalUser());
    }

    private async Task RefreshResponsibilityAfterRecoveryAsync()
    {
        // Recovery services change the durable journal directly rather than emitting the normal
        // lifecycle phases. Re-read it so tray, quit guard, banners and writable-action guards cannot
        // remain stale after success or a status transition.
        await InitializeRuntimeResponsibilityAsync();
        RefreshRuntimePresentation();
    }
}
