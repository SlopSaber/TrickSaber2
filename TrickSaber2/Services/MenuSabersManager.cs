using System.Collections.Generic;
using TrickSaber2.Components;
using TrickSaber2.Configuration;
using TrickSaber2.Extensions;
using TrickSaber2.Models;
using TrickSaber2.Tricks;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace TrickSaber2.Services;

internal class MenuSabersManager : ITickable
{
    private readonly PluginConfig pluginConfig;
    private readonly MenuPointers menuPointers;
    private readonly ColorSchemesSettings colorSchemesSettings;

    private readonly MockSaber leftSaber;
    private readonly MockSaber rightSaber;
    private readonly Dictionary<XRNode, TrickObjectContainer> trickContainers = [];
    private readonly List<MovementTracker> movementTrackers = [];

    private readonly InputListener inputListener = new();
    private bool enabled;
    
    public MenuSabersManager(
        PluginConfig pluginConfig,
        MenuPointers menuPointers,
        ColorSchemesSettings colorSchemesSettings,
        GameResourcesProvider gameResourcesProvider,
        TrickObjectContainer.Factory trickObjectContainerFactory)
    {
        this.pluginConfig = pluginConfig;
        this.menuPointers = menuPointers;
        this.colorSchemesSettings = colorSchemesSettings;

        leftSaber = gameResourcesProvider.CreateMockSaber();
        rightSaber = gameResourcesProvider.CreateMockSaber();
        SetupSaber(leftSaber, XRNode.LeftHand, menuPointers.LeftParent);
        SetupSaber(rightSaber, XRNode.RightHand, menuPointers.RightParent);
        
        SetSaberVisibility(false);
        UpdateInputListeners();

        void SetupSaber(MockSaber saber, XRNode node, Transform parent)
        {
            saber.transform.SetParent(parent, false);
            var tracker = parent.gameObject.AddComponent<MovementTracker>();
            movementTrackers.Add(tracker);
            var trickObject = saber.gameObject.AddComponent<TrickObject>();
            var trickObjectContainer = trickObjectContainerFactory.Create(new(node, [trickObject], tracker));
            trickContainers.Add(node, trickObjectContainer);
        }
    }

    public void UpdateInputListeners()
    {
        CancelActiveTricks();
        inputListener.SetHandlers(pluginConfig.Tricks.ToHandlers());
    }

    public void SwitchToSabers()
    {
        inputListener.InputActivatedEvent += HandleInputListenerInputActivated;
        inputListener.InputDeactivatedEvent += HandleInputListenerInputDeactivated;
        enabled = true;
        
        menuPointers.SetPointerVisibility(false);
        SetSaberVisibility(true);
        
        var colorScheme = colorSchemesSettings.GetSelectedColorScheme();
        leftSaber.SetColor(colorScheme.saberAColor);
        rightSaber.SetColor(colorScheme.saberBColor);
    }

    public void SwitchToPointers()
    {
        inputListener.InputActivatedEvent -= HandleInputListenerInputActivated;
        inputListener.InputDeactivatedEvent -= HandleInputListenerInputDeactivated;
        enabled = false;
        CancelActiveTricks();
        
        menuPointers.SetPointerVisibility(true);
        SetSaberVisibility(false);
    }

    public void Tick()
    {
        if (enabled) inputListener.ManualUpdate();
    }

    private void SetSaberVisibility(bool isVisible)
    {
        foreach (var tracker in movementTrackers) tracker.enabled = isVisible;
        leftSaber.gameObject.SetActive(isVisible);
        rightSaber.gameObject.SetActive(isVisible);
    }

    private void HandleInputListenerInputActivated(IInputHandler handler)
    {
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
        foreach (var (_, container) in trickContainers) container.CancelActiveTrick();
    }
}
