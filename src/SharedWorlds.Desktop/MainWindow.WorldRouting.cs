using SharedWorlds.Core.Abstractions;
using SharedWorlds.Core.Domain;
using SharedWorlds.Core.Environment;
using SharedWorlds.Core.Worlds;

namespace SharedWorlds.Desktop;

public partial class MainWindow
{
    private readonly HashSet<WorldId> _peerWorldIds = [];

    /// <summary>
    /// Returns the canonical Worlds installed on this device. Shared Worlds are classified from their
    /// durable peer-authority record; SafeWorld does not merge a backend-owned catalog into the normal
    /// product surface.
    /// </summary>
    private async Task<IReadOnlyList<World>> ListDesktopWorldsAsync(
        CancellationToken cancellationToken = default)
    {
        var localWorlds = await _storage.ListWorldsAsync(cancellationToken);
        _peerWorldIds.Clear();

        foreach (var localWorld in localWorlds)
        {
            if (localWorld.SharingMode == WorldSharingMode.Shared &&
                localWorld.PeerAuthority is not null)
            {
                _peerWorldIds.Add(localWorld.Id);
            }
        }

        return localWorlds;
    }

    private bool HasAuthoritativeRuntimeForWorld(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.SharingMode == WorldSharingMode.LocalOnly)
        {
            return true;
        }

        return _peerWorldIds.Contains(world.Id) && _peerRuntime is not null;
    }

    private IWorldStorage GetStorageForWorld(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return _peerWorldIds.Contains(world.Id)
            ? RequirePeerRuntime(world).Storage
            : _storage;
    }

    private WorldLifecycleService GetLifecycleForWorld(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_peerWorldIds.Contains(world.Id))
        {
            return RequirePeerRuntime(world).Lifecycle;
        }

        if (world.SharingMode == WorldSharingMode.Shared)
        {
            throw new InvalidOperationException(
                "This shared World predates SafeWorld peer authority and cannot be opened writable. Import or migrate it into the peer product before play.");
        }

        return _lifecycle;
    }

    private UserIdentity GetUserForWorld(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_peerWorldIds.Contains(world.Id))
        {
            return RequirePeerRuntime(world).User;
        }

        if (world.SharingMode == WorldSharingMode.Shared)
        {
            throw new InvalidOperationException(
                "This shared World predates SafeWorld peer authority and has no writable peer identity on this device.");
        }

        return GetLocalUser();
    }

    private IGameAdapter GetManagedHostAdapterForWorld(
        World world,
        IGameAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(adapter);
        return _peerWorldIds.Contains(world.Id)
            ? RequirePeerRuntime(world).CoordinateManagedHost(world.Id, adapter)
            : adapter;
    }

    private StewardDesktopPeerRuntime RequirePeerRuntime(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return _peerRuntime
            ?? throw new InvalidOperationException(
                _peerRuntimeProblem is null
                    ? $"Shared World '{world.Name}' uses persistent peer authority, but the embedded Steam peer runtime is unavailable on this launch."
                    : $"Shared World '{world.Name}' uses persistent peer authority, but the embedded Steam peer runtime is unavailable: {_peerRuntimeProblem}");
    }

    private Task<EnvironmentRevision?> LoadEnvironmentRevisionForWorldAsync(
        World world,
        RevisionId revisionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        return GetStorageForWorld(world).LoadEnvironmentRevisionAsync(
            world.Id,
            revisionId,
            cancellationToken);
    }
}
