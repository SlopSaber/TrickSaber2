using System;
using System.Collections;
using System.Collections.Generic;
using TrickSaber2.Models;
using UnityEngine;
using Zenject;

namespace TrickSaber2.Services;

internal class TimeWarpManager : IInitializable, IDisposable
{
    private readonly AudioTimeSyncController audioTimeSyncController;
    private readonly StandardLevelGameplayManager standardLevelGameplayManager;
    private readonly ICoroutineStarter coroutineStarter;
    private readonly WaitForFixedUpdate waitForFixedUpdate = new();

    public TimeWarpManager(
        AudioTimeSyncController audioTimeSyncController,
        StandardLevelGameplayManager standardLevelGameplayManager,
        ICoroutineStarter coroutineStarter)
    {
        this.audioTimeSyncController = audioTimeSyncController;
        this.standardLevelGameplayManager = standardLevelGameplayManager;
        this.coroutineStarter = coroutineStarter;
    }

    private const float DefaultTimescaleStep = 0.02f;
    private float originalTimeScale;

    private Coroutine? startTimeWarpAnimation;
    private Coroutine? endTimeWarpAnimation;

    private readonly HashSet<IInputHandler> activeTimeWarps = [];

    public void Initialize()
    {
        audioTimeSyncController.stateChangedEvent += HandleAudioTimeSyncControllerStateChanged;
    }

    public void Dispose()
    {
        audioTimeSyncController.stateChangedEvent -= HandleAudioTimeSyncControllerStateChanged;
        if (startTimeWarpAnimation != null) coroutineStarter.StopCoroutine(startTimeWarpAnimation);
        if (endTimeWarpAnimation != null) coroutineStarter.StopCoroutine(endTimeWarpAnimation);
        startTimeWarpAnimation = null;
        endTimeWarpAnimation = null;
        activeTimeWarps.Clear();
    }

    public void StartTimeWarp(IInputHandler handler)
    {
        if (!(audioTimeSyncController.didStart && activeTimeWarps.Add(handler) && activeTimeWarps.Count == 1))
        {
            return;
        }

        if (endTimeWarpAnimation != null)
        {
            coroutineStarter.StopCoroutine(endTimeWarpAnimation);
        }

        var warpSpeed = 1f - handler.TrickConfig.TrickSpecificSettings.ThrowTimeWarpMultiplier;
        startTimeWarpAnimation = coroutineStarter.StartCoroutine(StartTimeWarp(warpSpeed));
    }

    public void StopTimeWarp(IInputHandler handler)
    {
        if (!(audioTimeSyncController.didStart && activeTimeWarps.Remove(handler) && activeTimeWarps.Count == 0))
        {
            return;
        }

        if (startTimeWarpAnimation != null)
        {
            coroutineStarter.StopCoroutine(startTimeWarpAnimation);
        }

        endTimeWarpAnimation = coroutineStarter.StartCoroutine(EndTimeWarp());
        activeTimeWarps.Remove(handler);
    }

    public void ResetTimeWarp()
    {
        activeTimeWarps.Clear();
        if (startTimeWarpAnimation != null) coroutineStarter.StopCoroutine(startTimeWarpAnimation);
        if (endTimeWarpAnimation != null) coroutineStarter.StopCoroutine(endTimeWarpAnimation);
        SetTimeScale(originalTimeScale);
    }
    
    private IEnumerator StartTimeWarp(float warpSpeed)
    {
        var timeScale = audioTimeSyncController.timeScale;
        var targetTimeScale = originalTimeScale - warpSpeed;
        if (targetTimeScale < 0.1f) targetTimeScale = 0.1f;
        
        Plugin.Log.Debug($"Starting time warp - current timeScale: {timeScale} - target timeScale: {targetTimeScale}");

        var step = DefaultTimescaleStep;
        
        while (timeScale > targetTimeScale)
        {
            timeScale -= step;
            SetTimeScale(timeScale);
            yield return waitForFixedUpdate;
        }

        SetTimeScale(timeScale);
    }

    private IEnumerator EndTimeWarp()
    {
        Plugin.Log.Debug("Ending time warp");
        
        var timeScale = audioTimeSyncController.timeScale;
        var targetTimeScale = originalTimeScale;

        var step = DefaultTimescaleStep;
        
        while (timeScale < targetTimeScale)
        {
            timeScale += step;
            audioTimeSyncController._timeScale = timeScale;
            audioTimeSyncController._audioSource.pitch = timeScale;
            yield return waitForFixedUpdate;
        }

        audioTimeSyncController._timeScale = timeScale;
        audioTimeSyncController._audioSource.pitch = timeScale;
    }

    private void SetTimeScale(float timeScale)
    {
        audioTimeSyncController._timeScale = timeScale;
        audioTimeSyncController._audioSource.pitch = timeScale;
    }

    private void HandleAudioTimeSyncControllerStateChanged()
    {
        if (audioTimeSyncController.didStart)
        {
            audioTimeSyncController.stateChangedEvent -= HandleAudioTimeSyncControllerStateChanged;
            originalTimeScale = audioTimeSyncController.timeScale;
            Plugin.Log.Notice($"OriginalTimeScale: {originalTimeScale}");
        }
    }
}