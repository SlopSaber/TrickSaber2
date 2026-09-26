using System.Collections.Generic;
using TrickSaber2.Configuration;
using TrickSaber2.Extensions;
using TrickSaber2.Models;
using TrickSaber2.Services;
using TrickSaber2.Tricks;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace TrickSaber2.Components;

internal class GameTrickManager : MonoBehaviour
{
    [Inject] private readonly PluginConfig pluginConfig = null!;
    [Inject] private readonly PauseController pauseController = null!;
    [Inject] private readonly ILevelEndActions levelEndActions = null!;
    [Inject] private readonly GameTrickObjectManager gameTrickObjectManager = null!;
    [Inject] private readonly NoteTracker noteTracker = null!;
    [InjectOptional] private readonly TimeWarpManager? timeWarpManager = null;
    
    private readonly InputListener inputListener = new();
    private readonly Dictionary<XRNode, TrickObjectContainer> trickContainers = [];

    private void Start()
    {
        inputListener.SetHandlers(pluginConfig.Tricks.ToHandlers());
        foreach (var container in gameTrickObjectManager.GetTrickObjectContainers())
        {
            trickContainers.Add(container.Node, container);
        }
        pauseController.didPauseEvent += HandlePauseControllerDidPause;
        pauseController.didResumeEvent += HandlePauseControllerDidResume;
        pauseController.didReturnToMenuEvent += CancelActiveTricks;
        levelEndActions.levelFinishedEvent += CancelActiveTricks;
        levelEndActions.levelFailedEvent += CancelActiveTricks;
        inputListener.InputActivatedEvent += HandleInputListenerInputActivated;
        inputListener.InputDeactivatedEvent += HandleInputListenerInputDeactivated;
    }

    private void OnDestroy()
    {
        pauseController.didPauseEvent -= HandlePauseControllerDidPause;
        pauseController.didResumeEvent -= HandlePauseControllerDidResume;
        pauseController.didReturnToMenuEvent -= CancelActiveTricks;
        levelEndActions.levelFinishedEvent -= CancelActiveTricks;
        levelEndActions.levelFailedEvent -= CancelActiveTricks;
        inputListener.InputActivatedEvent -= HandleInputListenerInputActivated;
        inputListener.InputDeactivatedEvent -= HandleInputListenerInputDeactivated;
    }

    private void Update()
    {
        inputListener.ManualUpdate();
    }

    private void HandleInputListenerInputActivated(IInputHandler handler)
    {
        if (noteTracker.DisableIfNotesOnScreen())
        {
            return;
        }

        if (timeWarpManager != null && handler.TrickConfig.HasTimeWarp())
        {
            timeWarpManager.StartTimeWarp(handler);
        }
        
        if (handler is IVRInputHandler vrInputHandler
            && trickContainers.TryGetValue(vrInputHandler.Node, out var container))
        {
            container.StartTrick(handler);
        }
#if DEBUG
        else if (handler is KeyboardInputHandler)
        {
            foreach (var (_, value) in trickContainers) value.StartTrick(handler);
        }
#endif
    }

    private void HandleInputListenerInputDeactivated(IInputHandler handler)
    {
        if (timeWarpManager != null && handler.TrickConfig.HasTimeWarp())
        {
            timeWarpManager.StopTimeWarp(handler);
        }
        
        if (handler is IVRInputHandler vrInputHandler
            && trickContainers.TryGetValue(vrInputHandler.Node, out var container))
        {
            container.StopTrick(handler);
        }
#if DEBUG
        else if (handler is KeyboardInputHandler)
        {
            foreach (var (_, value) in trickContainers) value.StopTrick(handler);
        }
#endif
    }

    private void CancelActiveTricks()
    {
        timeWarpManager?.ResetTimeWarp();
        foreach (var (_, container) in trickContainers)
        {
            container.CancelActiveTrick();
        }
    }

    private void HandlePauseControllerDidPause()
    {
        enabled = false;
        CancelActiveTricks();
    }

    private void HandlePauseControllerDidResume()
    {
        enabled = true;
    }
}
