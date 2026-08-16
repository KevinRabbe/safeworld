using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SharedWorlds.Core.Domain;
using SharedWorlds.Core.Worlds;

namespace SharedWorlds.Desktop;

public partial class MainWindow
{
    private StackPanel? _deleteWorldPanel;
    private TextBlock? _deleteWorldDescription;
    private Button? _deleteWorldButton;
    private bool _worldDeletionUiInitialized;

    internal void InitializeWorldDeletionUi()
    {
        if (_worldDeletionUiInitialized ||
            KeepExactGameVersionCheckBox.Parent is not StackPanel settings)
        {
            return;
        }

        _worldDeletionUiInitialized = true;

        var separator = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 18, 0, 16),
            Background = (Brush)FindResource("BorderBrush")
        };
        var heading = new TextBlock
        {
            Text = DesktopText.DangerZone,
            FontWeight = FontWeights.SemiBold
        };
        var description = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 12),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("MutedTextBrush")
        };
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 6, 12, 6),
            FontWeight = FontWeights.SemiBold
        };

        var panel = new StackPanel
        {
            Visibility = Visibility.Collapsed
        };
        panel.Children.Add(separator);
        panel.Children.Add(heading);
        panel.Children.Add(description);
        panel.Children.Add(button);
        settings.Children.Add(panel);

        _deleteWorldPanel = panel;
        _deleteWorldDescription = description;
        _deleteWorldButton = button;
        button.Click += DeleteWorldButton_Click;
        WorldList.SelectionChanged += (_, _) => UpdateWorldDeletionActionState();
        WorldList.IsEnabledChanged += (_, _) => UpdateWorldDeletionActionState();

        UpdateWorldDeletionActionState();
    }

    private void UpdateWorldDeletionActionState()
    {
        if (!_worldDeletionUiInitialized ||
            _deleteWorldPanel is null ||
            _deleteWorldDescription is null ||
            _deleteWorldButton is null)
        {
            return;
        }

        var world = _selectedWorld;
        var isPersistentPeerWorld = world?.PeerAuthority is not null;
        var canOfferLocalRemoval = world is not null && !isPersistentPeerWorld;
        _deleteWorldPanel.Visibility = canOfferLocalRemoval
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!canOfferLocalRemoval || world is null)
        {
            _deleteWorldButton.IsEnabled = false;
            return;
        }

        // Shared without peer authority is a pre-peer local record. Persistent peer Worlds are never
        // deleted through this raw local-storage action; membership/leave owns that transition.
        var incompleteSharedRecord =
            world.SharingMode == WorldSharingMode.Shared &&
            world.PeerAuthority is null;
        var responsibility = _responsibilityTracker.Current;
        var selectedOwnsResponsibility = responsibility.Kind != WorldLifecycleResponsibilityKind.None &&
                                         responsibility.WorldId == world.Id;
        var canAbandonDurableResponsibility =
            incompleteSharedRecord &&
            selectedOwnsResponsibility &&
            responsibility.Kind is
                WorldLifecycleResponsibilityKind.InterruptedSession or
                WorldLifecycleResponsibilityKind.RecoveryNeeded or
                WorldLifecycleResponsibilityKind.CleanupPending;

        _deleteWorldButton.Content = incompleteSharedRecord
            ? "Remove from SafeWorld"
            : DesktopText.DeleteWorld;
        _deleteWorldDescription.Text = incompleteSharedRecord
            ? canAbandonDurableResponsibility
                ? "Remove this pre-peer shared World record from this PC and stop its unresolved local recovery responsibility. Uncommitted changes will not be recovered; uncertain workspace files are preserved as abandoned evidence."
                : "Remove this pre-peer shared World record and its local revision history from this PC. This does not contact or modify any legacy service."
            : DesktopText.DeleteWorldDescription;
        AutomationProperties.SetName(
            _deleteWorldButton,
            incompleteSharedRecord ? "Remove from SafeWorld" : DesktopText.DeleteWorld);

        _deleteWorldButton.IsEnabled =
            !_isBusy &&
            (!selectedOwnsResponsibility || canAbandonDurableResponsibility);

        var help = selectedOwnsResponsibility && !canAbandonDurableResponsibility
            ? "Finish the active game session before removing this World."
            : canAbandonDurableResponsibility
                ? "Abandon this pre-peer World's unresolved local recovery evidence, remove its local catalog record, and unblock other Worlds."
                : incompleteSharedRecord
                    ? "Remove the pre-peer local shared-World record from this PC without contacting any legacy service."
                    : "Delete SafeWorld's managed copy and revision history. The game's own save folder is not modified.";
        _deleteWorldButton.ToolTip = help;
        AutomationProperties.SetHelpText(_deleteWorldButton, help);
    }

    private async void DeleteWorldButton_Click(object sender, RoutedEventArgs e)
    {
        var world = _selectedWorld;
        if (world is null)
        {
            return;
        }

        if (world.PeerAuthority is not null)
        {
            StatusText.Text =
                "Shared peer Worlds must be left through Manage access so SafeWorld can preserve membership and authority safety.";
            UpdateWorldDeletionActionState();
            return;
        }

        var incompleteSharedRecord = world.SharingMode == WorldSharingMode.Shared;
        var responsibility = _responsibilityTracker.Current;
        var selectedOwnsResponsibility = responsibility.Kind != WorldLifecycleResponsibilityKind.None &&
                                         responsibility.WorldId == world.Id;
        var canAbandonDurableResponsibility =
            incompleteSharedRecord &&
            selectedOwnsResponsibility &&
            responsibility.Kind is
                WorldLifecycleResponsibilityKind.InterruptedSession or
                WorldLifecycleResponsibilityKind.RecoveryNeeded or
                WorldLifecycleResponsibilityKind.CleanupPending;

        if (selectedOwnsResponsibility && !canAbandonDurableResponsibility)
        {
            StatusText.Text =
                "Finish this World's active game session before removing it from SafeWorld.";
            UpdateWorldDeletionActionState();
            return;
        }

        var actionTitle = incompleteSharedRecord
            ? "Remove from SafeWorld"
            : DesktopText.DeleteWorld;
        var consequence = incompleteSharedRecord
            ? canAbandonDurableResponsibility
                ? "SafeWorld will stop treating this pre-peer World's unresolved local recovery record as active, preserve uncertain workspace files as abandoned evidence, and remove the World and its local revision history from this PC. " +
                  "Uncommitted gameplay changes will not be recoverable through SafeWorld. No legacy service is contacted or modified."
                : "SafeWorld will remove this pre-peer shared World record and its local revision history from this PC. " +
                  "No legacy service is contacted or modified."
            : "SafeWorld's managed copy and its revision history will be deleted. " +
              "A save in the game's own save folder is not deleted.";

        var confirmation = MessageBox.Show(
            this,
            $"{actionTitle} for '{world.Name}'?{Environment.NewLine}{Environment.NewLine}" +
            consequence +
            $"{Environment.NewLine}{Environment.NewLine}This cannot be undone on this PC.",
            actionTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var worldId = world.Id;
        var worldName = world.Name;
        await RunUnifiedOperationAsync(
            $"Removing {worldName}...",
            async () =>
            {
                try
                {
                    if (canAbandonDurableResponsibility)
                    {
                        await AbandonRecoveryRecordsForRemovedWorldAsync(worldId, worldName);
                    }

                    if (!await _storage.DeleteWorldAsync(worldId))
                    {
                        throw new InvalidOperationException(
                            $"World '{worldName}' is no longer present in SafeWorld's local managed storage.");
                    }

                    _selectedWorld = null;
                    WorldList.SelectedItem = null;
                    await RefreshUnifiedWorldsAsync(preserveStatus: true);
                    StatusText.Text = incompleteSharedRecord
                        ? $"Removed pre-peer shared World '{worldName}' from this PC. Other Worlds are no longer blocked by it."
                        : $"Deleted '{worldName}' from SafeWorld.";
                }
                finally
                {
                    await InitializeRuntimeResponsibilityAsync();
                    RefreshRuntimePresentation();
                }
            });

        UpdateWorldDeletionActionState();
    }

    private async Task AbandonRecoveryRecordsForRemovedWorldAsync(
        WorldId worldId,
        string worldName)
    {
        var records = (await _workspaceRecoveryStore.ListAsync())
            .Where(record =>
                record.WorldId == worldId &&
                record.Status != WorkspaceRecoveryStatus.Abandoned)
            .OrderBy(record => record.CreatedAt)
            .ThenBy(record => record.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (records.Length == 0)
        {
            throw new InvalidOperationException(
                $"SafeWorld could not find the durable recovery evidence that currently blocks '{worldName}'.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var record in records)
        {
            await _workspaceRecoveryStore.SaveAsync(record with
            {
                Status = WorkspaceRecoveryStatus.Abandoned,
                UpdatedAt = now,
                Reason =
                    $"The user removed pre-peer local shared World '{worldName}'. SafeWorld preserved " +
                    "the uncertain workspace as abandoned evidence instead of guessing how to clean it. " +
                    "This record no longer owns runtime responsibility."
            });
        }
    }
}
