namespace SharedWorlds.Desktop;

public partial class MainWindow
{
    internal async Task InitializeUnifiedStartupAsync()
    {
        // Recovery evidence is authoritative startup input. Load it before any game/World surface
        // can present an interrupted World as Ready.
        await InitializeRuntimeResponsibilityAsync();
        RegisterAdditionalProductionAdapters();
        InitializeUnifiedHostingPreference();
        await LoadDeviceSettingsAsync();

        // SafeWorld is a peer product. Compose Steam peer authority directly from the durable local
        // installation identity. Normal startup must never authenticate to, probe, or construct the
        // legacy Steward backend runtime; old backend code remains migration-only until it is removed.
        InitializeStewardPeerRuntime();

        await InitializeUnifiedGameUiAsync();
        InitializeInstallationAwareWorldActions();
        InitializeWorldSearchUi();

        InitializeWorldSharingUi();
        InitializePortableWorldExportUi();
        InitializePortableWorldImportUi();
        InitializePortableWorldDropUi();

        UpdateWorldSharingActionState();
        await InitializeWorldJoinUiAsync();

        // Steam delivers accepted lobby invitations through GameLobbyJoinRequested_t while SafeWorld is
        // running, but uses +connect_lobby <lobbyId> when the invite launches the app. Process that
        // cold-start form only after the same Join UI/runtime handlers are ready.
        InitializeSteamLobbyLaunchRequest();

        InitializeUnifiedImportBrowser();
        InitializeImportAccessibility();
        await InitializeSafeWorldGamesHomeAsync();
        InitializeResponsibilityPresentation();
        InitializeWorldDeletionUi();

        InitializeProfessionalProductShell();
        InitializeVisualDesignV2();
        InitializeVisualDesignV2Refinements();
        InitializeWorldHistoryUi();
        InitializeNavigationV2StateGuard();
        InitializeQuietStatusPresentation();
        InitializeGameAttentionProjection();
        InitializeResponsiveWorkspace();
    }
}
