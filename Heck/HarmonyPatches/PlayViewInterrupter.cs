using System;
using System.Reflection;
using HarmonyLib;
using Heck.PlayView;
using HMUI;
using JetBrains.Annotations;
using SiraUtil.Affinity;

namespace Heck.HarmonyPatches;

[HeckPatch]
internal class PlayViewInterrupter : IAffinity
{
    private readonly PlayViewManager _playViewManager;
    private readonly LobbyGameStateController _lobbyGameStateController;
    private readonly LobbyGameStateModel _lobbyGameStateModel;

    private bool _playViewManagerHasRun;

    private PlayViewInterrupter(
        PlayViewManager playViewManager,
        ILobbyGameStateController lobbyGameStateController,
        LobbyGameStateModel lobbyGameStateModel)
    {
        _playViewManager = playViewManager;
        _lobbyGameStateController = (lobbyGameStateController as LobbyGameStateController)!;
        _lobbyGameStateModel = lobbyGameStateModel;
    }

    [AffinityPrefix]
    [AffinityPatch(
        typeof(SinglePlayerLevelSelectionFlowCoordinator),
        nameof(SinglePlayerLevelSelectionFlowCoordinator.StartLevel))]
    private void StartLevelPrefix(
        ref bool __runOriginal,
        SinglePlayerLevelSelectionFlowCoordinator __instance,
        Action beforeSceneSwitchCallback,
        bool practice)
    {
        if (!__runOriginal)
        {
            return;
        }

        StartStandardLevelParameters parameters =
            new StartStandardLevelParameters(
                __instance.gameMode,
                __instance.selectedBeatmapKey,
                __instance.selectedBeatmapLevel,
                __instance._gameplaySetupViewController.environmentOverrideSettings,
                __instance._gameplaySetupViewController.colorSchemesSettings.GetOverrideColorScheme(),
                __instance._gameplaySetupViewController.colorSchemesSettings.ShouldOverrideLightshowColors(),
                __instance._gameplaySetupViewController.colorSchemesSettings.GetOverrideColorScheme(),
                __instance.gameplayModifiers,
                __instance.playerSettings,
                practice ? __instance._practiceViewController.practiceSettings : null,
                __instance._environmentsListModel,
                null,
                __instance.actionButtonText,
                false,
                false,
                beforeSceneSwitchCallback,
                null,
                __instance.HandleStandardLevelDidFinish,
                __instance.HandleStandardLevelWasRestarted,
                null
            );

        _playViewManager.Init(parameters);
        __runOriginal = false;
    }

#if !PRE_V1_37_1
    private static StartMultiplayerLevelParameters GetMultiplayerParameters(
        LobbyGameStateController instance,
        ILevelGameplaySetupData gameplaySetupData,
        IBeatmapLevelData beatmapLevelData,
        Action beforeSceneSwitchCallback)
    {
        return new StartMultiplayerLevelParameters(
            "Multiplayer",
            gameplaySetupData.beatmapKey,
            instance._beatmapLevelsModel.GetBeatmapLevel(gameplaySetupData.beatmapKey.levelId)!,
            beatmapLevelData,
            instance._playerDataModel.playerData.colorSchemesSettings.GetOverrideColorScheme(),
            gameplaySetupData.gameplayModifiers,
            instance._playerDataModel.playerData.playerSpecificSettings,
            null,
            string.Empty,
            false,
            beforeSceneSwitchCallback,
            instance.HandleMultiplayerLevelDidFinish,
            instance.HandleMultiplayerLevelDidDisconnect);
    }
#endif

    [AffinityPostfix]
    [AffinityPatch(typeof(MultiplayerLevelLoader), nameof(MultiplayerLevelLoader.Tick))]
    private void WaitingForCountdownPostfix(
        MultiplayerLevelLoader.MultiplayerBeatmapLoaderState ____loaderState,
        ILevelGameplaySetupData ____gameplaySetupData,
#if !PRE_V1_37_1
        IBeatmapLevelData ____beatmapLevelData)
#else
        IDifficultyBeatmap ____difficultyBeatmap)
#endif
    {
        if (____loaderState != MultiplayerLevelLoader.MultiplayerBeatmapLoaderState.WaitingForCountdown ||
            _playViewManagerHasRun)
        {
            return;
        }

        if (_lobbyGameStateModel.gameState == MultiplayerGameState.Game)
        {
            _playViewManagerHasRun = false;
            return;
        }

        StartMultiplayerLevelParameters parameters = GetMultiplayerParameters(
            _lobbyGameStateController,
            ____gameplaySetupData,
#if !PRE_V1_37_1
            ____beatmapLevelData,
#else
            ____difficultyBeatmap,
#endif
            null!);

        _playViewManager.Init(parameters);
        _playViewManagerHasRun = true;
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(LobbyGameStateController), nameof(LobbyGameStateController.StartMultiplayerLevel))]
    private void StartMultiplayer(ref bool __runOriginal)
    {
        if (!__runOriginal)
        {
            return;
        }

        _playViewManagerHasRun = false;
        __runOriginal = _playViewManager.StartMultiplayer();
    }

    [AffinityPrefix]
    [AffinityPatch(
        typeof(LobbyGameStateController),
        nameof(LobbyGameStateController.HandleMenuRpcManagerCancelledLevelStart))]
    private void CancelMultiplayerLevelStart()
    {
        _playViewManagerHasRun = false;
    }

    [AffinityPrefix]
    [AffinityPatch(
        typeof(SinglePlayerLevelSelectionFlowCoordinator),
        "LevelSelectionFlowCoordinatorTopViewControllerWillChange")]
    private bool LevelSelectionFlowCoordinatorTopViewControllerWillChangePrefix(
        SinglePlayerLevelSelectionFlowCoordinator __instance,
        ViewController newViewController,
        ViewController.AnimationType animationType)
    {
        PlayViewManager.PlayViewControllerData? controllerData = _playViewManager.ActiveView;
        if (newViewController != (ViewController?)controllerData?.ViewController)
        {
            return true;
        }

        __instance.SetLeftScreenViewController(null, animationType);
        __instance.SetRightScreenViewController(null, animationType);
        __instance.SetBottomScreenViewController(null, animationType);
        __instance.SetTitle(null, animationType);

        FlowCoordinator flowCoordinator = __instance;
        flowCoordinator.showBackButton = true;

        return false;
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(SinglePlayerLevelSelectionFlowCoordinator), "BackButtonWasPressed")]
    private void BackButtonWasPressedPrefix(
        ref bool __runOriginal,
        SinglePlayerLevelSelectionFlowCoordinator __instance)
    {
        if (!__runOriginal)
        {
            return;
        }

        __runOriginal = _playViewManager.EarlyDismiss();
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(MenuTransitionsHelper), "HandleMainGameSceneDidFinish")]
    private void HandleMainGameSceneDidFinishPrefix(LevelCompletionResults levelCompletionResults)
    {
        if (levelCompletionResults.levelEndAction != LevelCompletionResults.LevelEndAction.Restart)
        {
            _playViewManager.FinishAll();
        }
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(MenuTransitionsHelper), "HandleMultiplayerLevelDidFinish")]
    [AffinityPatch(typeof(MenuTransitionsHelper), "HandleMultiplayerLevelDidDisconnect")]
    private void HandleMultiplayerLevelDidFinishPrefix()
    {
        _playViewManager.FinishAll();
    }
}
