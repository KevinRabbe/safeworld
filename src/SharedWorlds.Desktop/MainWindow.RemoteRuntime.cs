using System.IO;
using System.Net.Http;
using SharedWorlds.Core.Domain;
using SharedWorlds.Infrastructure.Remote;

namespace SharedWorlds.Desktop;

public partial class MainWindow
{
    // Legacy migration state. These fields are intentionally not consumed by MainWindow.WorldRouting;
    // the normal SafeWorld product surface is local/peer only.
    private readonly HashSet<WorldId> _remoteWorldIds = [];
    private readonly HashSet<WorldId> _remoteIncompleteWorldIds = [];
    private StewardDesktopRemoteRuntime? _remoteRuntime;

    /// <summary>
    /// Connects an already-authenticated Steward session to the retained legacy migration runtime.
    /// Normal SafeWorld startup never calls this method and normal World routing never consumes the
    /// resulting backend catalog.
    /// </summary>
    internal async Task SetAuthenticatedRemoteRuntimeAsync(
        Uri apiBaseAddress,
        StewardRemoteSessionTokens initialTokens,
        UserIdentity authenticatedUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apiBaseAddress);
        ArgumentNullException.ThrowIfNull(initialTokens);
        ArgumentNullException.ThrowIfNull(authenticatedUser);

        var migrationPublication = EnsureOwnedWorldLocationMigrationState();
        var remoteRoot = Path.Combine(
            DesktopLocalDataRoot.RequireResolvedRoot(),
            "remote");
        var next = StewardDesktopRemoteRuntime.Create(
            apiBaseAddress,
            _deviceSettings.InstallationId,
            initialTokens,
            authenticatedUser,
            remoteRoot,
            _storage,
            migrationPublication.Journal,
            _workspaceRecoveryStore,
            CreateDesktopLifecycleObserver());

        StewardDesktopRemoteRuntime? previous = null;
        var publicationGateHeld = false;
        try
        {
            var registration = await next.OwnedWorldLocations.RegisterCurrentInstallationAsync(
                Environment.MachineName,
                cancellationToken);
            if (registration.IsConflict ||
                !string.Equals(
                    registration.Code,
                    "InstallationRegistered",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Steward did not confirm this Safe World installation registration.");
            }

            await _ownedWorldLocationPublicationGate.WaitAsync(cancellationToken);
            publicationGateHeld = true;
            await next.ReconcileAndReplayOwnedWorldLocationsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            previous = Interlocked.Exchange(ref _remoteRuntime, next);
        }
        catch
        {
            next.Dispose();
            throw;
        }
        finally
        {
            if (publicationGateHeld)
            {
                _ownedWorldLocationPublicationGate.Release();
            }
        }

        previous?.Dispose();
        migrationPublication.Trigger.Request();

        await RefreshUnifiedWorldsAsync(
            _selectedWorld?.Id,
            preserveStatus: false);
        await RefreshOwnedPrivateWorldCatalogAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void DisposeRemoteRuntime()
    {
        _remoteWorldIds.Clear();
        _remoteIncompleteWorldIds.Clear();
        Interlocked.Exchange(ref _remoteRuntime, null)?.Dispose();
    }

    private static bool IsRemoteAvailabilityFailure(Exception exception)
        => exception is StewardRemoteApiException or
            HttpRequestException or
            IOException or
            TimeoutException or
            TaskCanceledException;
}
