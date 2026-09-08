using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public sealed class ResourceManager : MonoBehaviour
{
    private readonly struct ResourceKey : IEquatable<ResourceKey>
    {
        public readonly Type AssetType;
        public readonly string Address;

        public ResourceKey(Type assetType, string address)
        {
            AssetType = assetType;
            Address = address;
        }

        public bool Equals(ResourceKey other)
        {
            return AssetType == other.AssetType &&
                   string.Equals(Address, other.Address, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AssetType, Address);
        }

        public override string ToString()
        {
            return $"{AssetType?.Name}:{Address}";
        }
    }

    private sealed class ResourceEntry
    {
        public AsyncOperationHandle Handle;
        public UnityEngine.Object Asset;
        public int RefCount;
        public float LastUseTime;
    }

    public static ResourceManager Instance { get; private set; }

    private readonly Dictionary<ResourceKey, ResourceEntry> _cache = new();
    private readonly Dictionary<ResourceKey, Task<UnityEngine.Object>> _loading = new();
    private Task _initializationTask;
    private bool _initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async UniTask<ResourceHandle<T>> LoadHandleAsync<T>(string address, CancellationToken token, int retries = 2)
        where T : UnityEngine.Object
    {
        await EnsureInitializedAsync(token);

        var key = CreateKey<T>(address);

        if (_cache.TryGetValue(key, out var cached) && cached.Asset is T cachedAsset)
        {
            cached.RefCount++;
            cached.LastUseTime = Time.unscaledTime;
            return CreateHandle(key, cachedAsset);
        }

        if (_loading.TryGetValue(key, out var pendingTask))
        {
            var sharedAsset = await pendingTask.AsUniTask().AttachExternalCancellation(token);
            if (sharedAsset == null)
            {
                return null;
            }

            if (sharedAsset is not T typedSharedAsset)
            {
                throw new InvalidOperationException($"[Resource] type mismatch for {key}");
            }

            return AddReference(key, typedSharedAsset);
        }

        var completion = new TaskCompletionSource<UnityEngine.Object>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loading[key] = completion.Task;

        try
        {
            var asset = await LoadWithRetryAsync<T>(key, token, retries, completion);
            if (asset == null)
            {
                return null;
            }

            return AddReference(key, asset);
        }
        finally
        {
            _loading.Remove(key);
        }
    }

    public async UniTask<T> LoadAsync<T>(string address, CancellationToken token, int retries = 2)
        where T : UnityEngine.Object
    {
        using var handle = await LoadHandleAsync<T>(address, token, retries);
        return handle?.Asset;
    }

    public async UniTask PreloadAsync<T>(string address, CancellationToken token, int retries = 2)
        where T : UnityEngine.Object
    {
        await EnsureInitializedAsync(token);

        var key = CreateKey<T>(address);
        if (_cache.ContainsKey(key) || _loading.ContainsKey(key))
        {
            return;
        }

        var completion = new TaskCompletionSource<UnityEngine.Object>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loading[key] = completion.Task;

        try
        {
            await LoadWithRetryAsync<T>(key, token, retries, completion);
        }
        finally
        {
            _loading.Remove(key);
        }
    }

    public async UniTask<ResourceSceneHandle> LoadSceneAsync(
        string address,
        LoadSceneMode mode,
        CancellationToken token,
        bool activateOnLoad = true)
    {
        await EnsureInitializedAsync(token);

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("address is empty", nameof(address));
        }

        var loadHandle = Addressables.LoadSceneAsync(address, mode, activateOnLoad);
        try
        {
            var scene = await loadHandle.ToUniTask(cancellationToken: token);
            return new ResourceSceneHandle(address, scene);
        }
        catch
        {
            if (loadHandle.IsValid())
            {
                Addressables.Release(loadHandle);
            }

            throw;
        }
    }

    public async UniTask<GameObject> InstantiateAsync(
        string address,
        Transform parent,
        CancellationToken token,
        bool worldPositionStays = false)
    {
        await EnsureInitializedAsync(token);

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("address is empty", nameof(address));
        }

        var handle = Addressables.InstantiateAsync(address, parent, worldPositionStays);
        try
        {
            return await handle.ToUniTask(cancellationToken: token);
        }
        catch
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }

            throw;
        }
    }

    public void ReleaseInstance(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Addressables.ReleaseInstance(instance);
    }

    public void Release(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        var keys = new List<ResourceKey>();
        foreach (var pair in _cache)
        {
            if (string.Equals(pair.Key.Address, address, StringComparison.Ordinal))
            {
                keys.Add(pair.Key);
            }
        }

        foreach (var key in keys)
        {
            Release(key);
        }
    }

    public void ReleaseUnused()
    {
        var keys = new List<ResourceKey>();
        foreach (var pair in _cache)
        {
            if (pair.Value.RefCount <= 0)
            {
                keys.Add(pair.Key);
            }
        }

        foreach (var key in keys)
        {
            Release(key);
        }
    }

    public void Clear()
    {
        foreach (var entry in _cache.Values)
        {
            if (entry.Handle.IsValid())
            {
                Addressables.Release(entry.Handle);
            }
        }

        _cache.Clear();
    }

    private async UniTask EnsureInitializedAsync(CancellationToken token)
    {
        if (_initialized)
        {
            return;
        }

        if (_initializationTask == null)
        {
            _initializationTask = InitializeAsync(token).AsTask();
        }

        try
        {
            await _initializationTask.AsUniTask().AttachExternalCancellation(token);
        }
        catch
        {
            if (!_initialized && (_initializationTask.IsFaulted || _initializationTask.IsCanceled))
            {
                _initializationTask = null;
            }

            throw;
        }
    }

    private async UniTask InitializeAsync(CancellationToken token)
    {
        Debug.Log("[Resource] initialize Addressables");
        await Addressables.InitializeAsync().ToUniTask(cancellationToken: token);
        _initialized = true;
    }

    private async UniTask<T> LoadWithRetryAsync<T>(
        ResourceKey key,
        CancellationToken token,
        int retries,
        TaskCompletionSource<UnityEngine.Object> completion)
        where T : UnityEngine.Object
    {
        for (int attempt = 0; attempt <= retries; attempt++)
        {
            AsyncOperationHandle<T> loadHandle = default;
            try
            {
                loadHandle = Addressables.LoadAssetAsync<T>(key.Address);
                var asset = await loadHandle.ToUniTask(cancellationToken: token);

                if (asset != null)
                {
                    _cache[key] = new ResourceEntry
                    {
                        Handle = loadHandle,
                        Asset = asset,
                        RefCount = 0,
                        LastUseTime = Time.unscaledTime
                    };

                    completion.TrySetResult(asset);
                    return asset;
                }
            }
            catch (OperationCanceledException)
            {
                if (loadHandle.IsValid())
                {
                    Addressables.Release(loadHandle);
                }

                completion.TrySetCanceled();
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Resource] load failed {key}, attempt={attempt + 1}: {exception.Message}");
            }

            if (loadHandle.IsValid())
            {
                Addressables.Release(loadHandle);
            }

            if (attempt < retries)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    completion.TrySetCanceled();
                    throw;
                }
            }
        }

        Debug.LogError($"[Resource] load failed permanently: {key}");
        completion.TrySetResult(null);
        return null;
    }

    private ResourceHandle<T> AddReference<T>(ResourceKey key, T asset)
        where T : UnityEngine.Object
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            entry.Asset = asset;
            entry.RefCount++;
            entry.LastUseTime = Time.unscaledTime;
            return CreateHandle(key, asset);
        }

        _cache[key] = new ResourceEntry
        {
            Asset = asset,
            RefCount = 1,
            LastUseTime = Time.unscaledTime
        };

        return CreateHandle(key, asset);
    }

    private ResourceHandle<T> CreateHandle<T>(ResourceKey key, T asset)
        where T : UnityEngine.Object
    {
        return new ResourceHandle<T>(key.Address, asset, () => Release(key));
    }

    private void Release(ResourceKey key)
    {
        if (!_cache.TryGetValue(key, out var entry))
        {
            return;
        }

        if (entry.RefCount <= 0)
        {
            return;
        }

        entry.RefCount--;
        entry.LastUseTime = Time.unscaledTime;

        if (entry.RefCount > 0)
        {
            return;
        }

        entry.RefCount = 0;

        if (entry.Handle.IsValid())
        {
            Addressables.Release(entry.Handle);
        }

        _cache.Remove(key);
    }

    private ResourceKey CreateKey<T>(string address)
        where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("address is empty", nameof(address));
        }

        return new ResourceKey(typeof(T), address);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Clear();
            Instance = null;
        }
    }
}
