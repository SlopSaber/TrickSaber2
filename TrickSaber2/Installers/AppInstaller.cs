using TrickSaber2.Configuration;
using TrickSaber2.Services;
using TrickSaber2.Tricks;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace TrickSaber2.Installers;

internal class AppInstaller : Installer
{
    private readonly PluginConfig pluginConfig;

    public AppInstaller(PluginConfig pluginConfig)
    {
        this.pluginConfig = pluginConfig;
    }

    public override void InstallBindings()
    {
        Container.BindInstance(pluginConfig).AsSingle();

        Container.BindInterfacesAndSelfTo<GameResourcesProvider>().AsSingle();

        Container
            .BindFactory<TrickObjectContainer.InitData, TrickObjectContainer, TrickObjectContainer.Factory>()
            .FromMethod((container, initData) =>
            {
                var gameObject = new GameObject(nameof(TrickObjectContainer));
                var trickObjectContainer = container.InstantiateComponent<TrickObjectContainer>(gameObject);
                trickObjectContainer.Initialize(initData);
                return trickObjectContainer;
            });
    }
}
