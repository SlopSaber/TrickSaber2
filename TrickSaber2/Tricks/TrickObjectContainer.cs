using System;
using System.Collections.Generic;
using IPA.Utilities;
using TrickSaber2.Components;
using TrickSaber2.Configuration;
using TrickSaber2.Extensions;
using TrickSaber2.Models;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace TrickSaber2.Tricks;

internal class TrickObjectContainer : MonoBehaviour
{
    public class Factory : PlaceholderFactory<InitData, TrickObjectContainer>;
    public record InitData(XRNode XrNode, TrickObject[] TrickObjects, MovementTracker MovementTracker);
    
    private TimeHelper timeHelper = null!;
    private TrickObject[] trickObjects = null!;
    private MovementTracker movementTracker = null!;
    private Transform rotationContainer = null!;
    private readonly Dictionary<Type, ITrick> tricks = [];
    
    [Inject]
    private void Inject(TimeHelper timeHelper)
    {
        this.timeHelper = timeHelper;
    }

    public void Initialize(InitData initData)
    {
        trickObjects = initData.TrickObjects;
        movementTracker = initData.MovementTracker;
        rotationContainer = new GameObject("RotationContainer").transform;
        rotationContainer.SetParent(transform);
        Node = initData.XrNode;
    }

    private IInputHandler? activeHandler;
    private bool isToggleable;
    private bool isStopping;
    
    public XRNode Node { get; set; }

    public void StartTrick(IInputHandler handler)
    {
        // If a new trick is inputted we should cancel the current one and start the new one
        // If the same trick is inputted sequentially then update it with the new input value
        // If the current trick is active and ready to toggle it should be stopped on the next input 

        if (isToggleable && handler == activeHandler)
        {
            StopTrick(handler);
            return;
        }
        
        if (activeHandler != null)
        {
            var currentTrick = GetTrick(activeHandler.TrickConfig.Type);
            var trickChanged = activeHandler != handler;
            if (trickChanged)
            {
                currentTrick.CancelTrick();
                HandleStopTrickFinishedCallback();
            }
            else
            {
                currentTrick.SetInputValue(handler.GetInputValue());
                return;
            }
        }

        activeHandler = handler;
        transform.position = movementTracker.transform.position;
        transform.rotation = movementTracker.transform.rotation;
        foreach (var trickObject in trickObjects)
        {
            trickObject.SetParent(rotationContainer);
        }

        Plugin.Log.Debug($"Starting {handler.TrickConfig.Type} at {Node}");
        var startedTrick = GetTrick(handler.TrickConfig.Type);
        startedTrick.StartTrick(handler.TrickConfig);
        startedTrick.SetInputValue(handler.GetInputValue());
    }

    public void StopTrick(IInputHandler handler)
    {
        if (isStopping || activeHandler != handler)
        {
            return;
        }

        if (!isToggleable && activeHandler.TrickConfig.InputConfig.Toggle)
        {
            isToggleable = true;
            return;
        }
        
        isToggleable = false;
        isStopping = true;
        
        Plugin.Log.Debug($"Stopping {handler.TrickConfig.Type} at {Node}");
        GetTrick(handler.TrickConfig.Type).StopTrick(finishedCallback: HandleStopTrickFinishedCallback);
    }

    public void CancelActiveTrick()
    {
        if (activeHandler != null)
        {
            Plugin.Log.Debug($"Cancelling {activeHandler.TrickConfig.Type} at {Node}");
            GetTrick(activeHandler.TrickConfig.Type).CancelTrick();
            HandleStopTrickFinishedCallback();
        }
    }

    private ITrick GetTrick(TrickType type) => type switch
    {
        TrickType.Spin => GetTrick<SpinTrick>(),
        TrickType.Throw => GetTrick<ThrowTrick>(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private ITrick GetTrick<TTrick>() where TTrick : Component, ITrick
    {
        if (tricks.TryGetValue(typeof(TTrick), out var trick))
        {
            return trick;
        }

        trick = gameObject
            .GetOrAddComponent<TTrick>()
            .Init(timeHelper, movementTracker, rotationContainer);
        tricks.Add(typeof(TTrick), trick);
        return trick;
    }

    private void HandleStopTrickFinishedCallback()
    {
        foreach (var trickObject in trickObjects)
        {
            trickObject.ResetParent();
        }
        activeHandler = null;
        isToggleable = false;
        isStopping = false;
    }
}
