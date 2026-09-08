using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;

public sealed class ResourceSceneHandle : IDisposable
{
    private readonly string _address;
    private readonly SceneInstance _scene;
    private bool _disposed;

    internal ResourceSceneHandle(string address, SceneInstance scene)
    {
        _address = address;
        _scene = scene;
    }

    public string Address => _address;
    public SceneInstance Scene => _scene;
    public bool IsValid => !_disposed && _scene.Scene.IsValid();

    public async UniTask UnloadAsync(CancellationToken token = default)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var unloadHandle = Addressables.UnloadSceneAsync(_scene, false);
        try
        {
            await unloadHandle.ToUniTask(cancellationToken: token);
        }
        finally
        {
            if (unloadHandle.IsValid())
            {
                Addressables.Release(unloadHandle);
            }
        }
    }

    public void Dispose()
    {
        UnloadAsync().Forget();
    }
}
