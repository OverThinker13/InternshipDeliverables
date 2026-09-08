# Unity 6 + Addressables 通用资源管理器：从 0 到可录制 Demo

本教程从空项目开始，搭一个适合个人 Demo、教程视频和作品集展示的通用资源管理器。它不依赖 Luban，不依赖业务表驱动，只围绕 Unity 6、Addressables 和异步生命周期来做。

## 一、目标与架构

最终目标不是“封装一个更短的 Load 接口”，而是做出一套真正能长期使用的资源层：

- 统一加载 Sprite、Prefab、Scene、TextAsset 等资源。
- 统一缓存、引用计数、重试、取消、清理。
- 统一处理同地址并发请求。
- 统一处理实例化和场景卸载。

```text
UI / Gameplay
      ↓
ResourceManager（初始化、加载、缓存、重试、释放）
      ↓
ResourceHandle<T>（资源持有与释放）
      ↓
Addressables（真正的异步资源系统）
      ↓
ResourceSceneHandle（场景加载与卸载）
```

资源键不是单纯的地址，而是 `Type + Address`。这样做的原因很直接：同一个地址如果被错误地拿去当不同类型使用，系统能更早发现问题，不会悄悄串资源。

## 二、创建 Unity 6 工程

1. 用 Unity Hub 新建 Unity 6 项目，建议使用 2D Core 模板。
2. 在 Package Manager 安装 `Addressables`。
3. 再安装 `UniTask`。
4. 打开 `Window > Asset Management > Addressables > Groups`，点击 `Create Addressables Settings`。
5. 创建两个 Group：`ResourceAssets`、`SceneAssets`。
6. 准备一张 Sprite、一个 Prefab、一个测试 Scene 作为示例资源。

Addressables 的优势是异步加载和独立打包；代价是必须保存并释放句柄。这个教程的核心，就是把这件事讲清楚。

## 三、创建目录与资源准备

建议目录结构如下：

```text
Assets/
  Scenes/
  Art/
  Prefabs/
  Scripts/
    ResourceSystem/
    Demo/
```

资源命名建议稳定、可读、可复用，比如：

- `Item/Icon/1001`
- `Prefab/Enemy/Goblin`
- `Scene/Level01`

不要把资源地址写成临时备注。地址是契约，不是笔记。

## 四、导入代码及职责

把本目录下的脚本复制到项目中：

- `Scripts/ResourceSystem/ResourceManager.cs`
- `Scripts/ResourceSystem/ResourceHandle.cs`
- `Scripts/ResourceSystem/ResourceSceneHandle.cs`
- `Scripts/Demo/ResourceDemo.cs`

职责划分如下：

| 文件 | 作用 |
|---|---|
| `ResourceManager.cs` | 单例入口、初始化、加载、缓存、重试、预加载、清理 |
| `ResourceHandle.cs` | 资源引用句柄，负责一次性释放 |
| `ResourceSceneHandle.cs` | 场景句柄，负责卸载场景 |
| `ResourceDemo.cs` | 最小演示脚本，验证异步生命周期 |

## 五、初始化流程

场景里先建一个空物体，命名为 `ResourceSystem`，挂上 `ResourceManager`。启动时它会：

1. 做单例判断，防止重复实例。
2. `DontDestroyOnLoad` 保持跨场景存在。
3. 初始化 Addressables。
4. 后续由 `LoadHandleAsync<T>` 统一拉取资源。

这里最重要的讲解点是：资源管理器不是工具脚本，而是运行时服务。它的存在时间，必须覆盖整个资源生命周期。

## 六、制作测试界面

为了方便录教程，建议在场景里放一个最小测试界面：

```text
Canvas
└── DemoPanel
    ├── Image
    ├── LoadSpriteButton
    ├── LoadPrefabButton
    └── LoadSceneButton
```

测试对象建议再放两个：

- 一个挂 `ResourceDemo` 的空物体，用来演示 Sprite 加载。
- 一个 Prefab 和一个 Scene，用来演示实例化和场景卸载。

如果你只想先录最小闭环，也可以先只做 Image + `ResourceDemo`，后面再加 Prefab 和 Scene。

## 七、核心逻辑

### 加载

`LoadHandleAsync<T>` 的流程是：

1. 先初始化 Addressables。
2. 先查缓存。
3. 再查是否已有相同请求在加载。
4. 如果都没有，就真正发起加载。
5. 成功后把资源放进缓存，返回 `ResourceHandle<T>`。

### 引用计数

每次成功拿到一个句柄，引用计数加 1。每次 `Dispose()`，引用计数减 1。计数归零时，才真正释放 Addressables 句柄。

### 并发合并

同一个地址被多个系统同时请求时，只发一次真实加载，后来的请求 await 同一个任务。这样能避免同帧重复拉取。

### 重试与取消

加载失败后会按次数重试；对象销毁时通过 `CancellationToken` 立刻退出。失败是失败，取消是取消，这两件事一定要分开讲。

### Prefab 与 Scene

Prefab 用 `InstantiateAsync` 创建，释放时用 `ReleaseInstance`。  
场景用 `LoadSceneAsync` 加载，卸载时通过 `ResourceSceneHandle.UnloadAsync()` 归还。

### 预加载与清理

预加载是“先放进缓存，不马上给业务层持有”。  
清理是“切场景、退出时把所有句柄收干净”。

## 八、按钮与测试入口

如果你在 Demo Panel 上加按钮，可以按这个思路录：

```text
Load Sprite → 显示到 Image
Load Prefab → 实例化到父节点
Load Scene  → 加载测试场景
```

录制时重点不是按钮本身，而是它背后的资源生命周期：

- Sprite 是怎么被加载的。
- Prefab 实例是怎么被回收的。
- Scene 是怎么卸载的。

## 九、完整成功调用链

```text
按钮 / 脚本调用
→ ResourceManager.LoadHandleAsync<T>
→ 初始化 Addressables
→ 查缓存
→ 查并发中的同地址请求
→ Addressables.LoadAssetAsync<T>
→ 放入缓存
→ 返回 ResourceHandle<T>
→ UI 使用资源
→ Dispose
→ Release 引用计数
→ 计数归零后释放句柄
```

失败分支要同步讲清楚：

- 地址错误。
- 类型不匹配。
- 资源加载失败。
- 对象销毁后继续 await。
- 句柄没释放导致泄漏。

## 十、失败、边界和清理

| 场景 | 处理 |
|---|---|
| 地址为空 | 直接抛参数错误 |
| 地址不存在 | 重试后返回空并报错 |
| 对象已销毁 | 取消加载，不再写 UI |
| 同地址重复请求 | 合并到同一个加载任务 |
| Prefab 实例化失败 | 释放临时句柄 |
| 场景卸载 | 走 `UnloadAsync()` |
| 切场景 | `Clear()` 清空缓存 |
| 退出主对象 | `OnDestroy()` 兜底释放 |

典型失败流程可以这样讲：

```text
点击加载 → 校验地址 → 发起请求 → 如果失败则重试 →
如果对象已销毁则取消 → 如果最终失败则返回空 →
UI 不再继续使用结果。
```

## 十一、运行验证

1. 构建 Addressables。
2. 运行场景，确认 `ResourceManager` 正常初始化。
3. 先加载 Sprite，确认 Image 显示。
4. 再测 Prefab，确认实例能创建和释放。
5. 再测 Scene，确认能进入和卸载。
6. 故意填错地址，确认重试和报错行为正常。
7. 切场景和关闭对象，确认没有残留句柄。

## 十二、对象池和缓存扩展

第一版可以先不用对象池，直接创建和销毁。正式扩展时可以加：

- 资源缓存复用。
- UI 对象池。
- 最近使用淘汰。
- 低内存批量清理。

如果以后项目变大，还可以把统一接口扩展成：下载进度、资源分组、版本管理和热更新预加载。

## 十三、视频录制顺序

建议录制顺序如下：

1. 目标与架构。
2. Unity 6 工程和 Addressables 初始化。
3. 目录与资源地址规范。
4. `ResourceManager` 单例。
5. `ResourceHandle<T>`。
6. 缓存、引用计数、并发合并。
7. 失败重试与取消。
8. Sprite Demo。
9. Prefab 实例化。
10. Scene 加载和卸载。
11. 预加载与清理。
12. 测试与常见错误。

每一集都按同一个节奏讲：

1. 本集解决什么问题。
2. 上一集代码长什么样。
3. 本集新增什么。
4. Unity 怎么点。
5. Inspector 怎么填。
6. 跑出来是什么结果。
7. 最容易踩什么坑。

## 十四、面试表达

可以这样介绍：

> 我在 Unity 6 里实现了一套基于 Addressables 的通用资源管理器。它统一处理资源初始化、异步加载、缓存、引用计数、并发合并、重试、取消、Prefab 实例化和场景卸载，业务层只需要拿地址和类型，不需要直接管理 Addressables 句柄。这样既能减少重复代码，也能避免资源泄漏和生命周期错乱。

## 十五、逐步录制稿：从空项目到可运行资源管理器

下面不是概念说明，而是可以直接照着录的步骤。

### 第 0 步：创建项目并确认版本

1. 打开 Unity Hub，新建 Unity 6 项目。
2. 选择 2D Core 模板。
3. 项目名填写 `AddressableResourceDemo`。
4. 打开后先确认 Console 没有红色错误。

预期结果：项目可以正常编译，Hierarchy 里有默认场景对象。

### 第 1 步：安装 Addressables 和 UniTask

1. 打开 Package Manager。
2. 安装 `Addressables`。
3. 安装 `UniTask`。
4. 打开 Addressables Groups，创建 settings。

预期结果：项目里出现 `AddressableAssetsData`。

### 第 2 步：创建资源分组

1. 在 Groups 窗口创建 `ResourceAssets`。
2. 再创建 `SceneAssets`。
3. 把 Sprite、Prefab、Scene 分别拖入对应分组。

预期结果：资源都能显示 Address 和 Label。

### 第 3 步：创建脚本目录

1. 创建 `Scripts/ResourceSystem`。
2. 创建 `Scripts/Demo`。
3. 把本教程代码文件放进去。

### 第 4 步：挂载 ResourceManager

1. 场景里新建空物体 `ResourceSystem`。
2. 挂上 `ResourceManager`。
3. 保存场景并运行。

预期结果：`ResourceManager.Instance` 稳定存在。

### 第 5 步：准备测试图片

1. 准备一张 Sprite。
2. 设置为 Addressable。
3. 填一个稳定地址，例如 `Item/Icon/1001`。

预期结果：图片能被 Addressables 正常加载。

### 第 6 步：创建 Demo 物体

1. 新建空物体 `ResourceDemo`。
2. 挂上 `ResourceDemo.cs`。
3. 把 Image 拖到 `target`。
4. 把 Sprite 地址填到 `spriteAddress`。

预期结果：运行后图片异步显示。

### 第 7 步：验证 Prefab

1. 准备一个 Prefab。
2. 设置为 Addressable。
3. 用 `InstantiateAsync` 试着创建它。
4. 销毁时用 `ReleaseInstance` 归还。

预期结果：Prefab 能创建和释放，不会残留句柄。

### 第 8 步：验证 Scene

1. 准备一个测试 Scene。
2. 加入 Addressables。
3. 调用 `LoadSceneAsync`。
4. 再通过 `ResourceSceneHandle.UnloadAsync()` 卸载。

预期结果：场景能进入，也能正常退出。

### 第 9 步：故意制造错误

1. 把地址改错。
2. 运行，观察重试日志。
3. 关闭对象，确认不会继续写 UI。

预期结果：失败链路有日志，且不会把流程卡死。

### 第 10 步：录制收口

最后总结三件事：

1. 资源入口统一了。
2. 生命周期统一了。
3. 业务层只管用，不管释放细节。

## 十六、完整代码附录

下面的代码是本教程的最终版本。按文件名创建 `.cs` 文件即可。

### 1. `ResourceHandle.cs`

```csharp
using System;
using UnityEngine;

public sealed class ResourceHandle<T> : IDisposable where T : UnityEngine.Object
{
    private readonly string _address;
    private readonly T _asset;
    private readonly Action _release;
    private bool _disposed;

    internal ResourceHandle(string address, T asset, Action release)
    {
        _address = address;
        _asset = asset;
        _release = release;
    }

    public string Address => _address;
    public T Asset => _disposed ? null : _asset;
    public bool IsValid => !_disposed && _asset != null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _release?.Invoke();
    }
}
```

### 2. `ResourceSceneHandle.cs`

```csharp
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
```

### 3. `ResourceManager.cs`

```csharp
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
```

### 4. `ResourceDemo.cs`

```csharp
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceDemo : MonoBehaviour
{
    [SerializeField] private string spriteAddress = "Item/Icon/1001";
    [SerializeField] private Image target;
    private ResourceHandle<Sprite> _spriteHandle;

    private async UniTaskVoid Start()
    {
        try
        {
            _spriteHandle = await ResourceManager.Instance.LoadHandleAsync<Sprite>(spriteAddress, destroyCancellationToken);
            if (this == null || !isActiveAndEnabled || _spriteHandle == null)
            {
                return;
            }

            if (target != null)
            {
                target.sprite = _spriteHandle.Asset;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnDestroy()
    {
        _spriteHandle?.Dispose();
        _spriteHandle = null;
    }
}
```

### 代码组合顺序

1. 先放 `ResourceHandle.cs`。
2. 再放 `ResourceSceneHandle.cs`。
3. 再放 `ResourceManager.cs`。
4. 最后放 `ResourceDemo.cs` 做验证。

这样每一步编译成功后再继续，最容易定位问题。
