# Unity 6 + Addressables 通用资源管理器：一步一步做出来

本教程从空项目开始，完整做出一个适合个人 Demo 的通用资源管理器。  
教程里每一步都会先讲目标，再写代码，再给 Unity 点击步骤，最后说明本步结果和下步依赖。  
完整脚本文件放在 `教程/Unity6通用资源管理器/代码文件/` 目录里，方便你边录边对照。

## 一、目标与架构

我们要做的不是“封装一个更短的 Load”，而是一套真正能用的资源层：

- 统一加载 Sprite、Prefab、Scene。
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

资源键不是单纯地址，而是 `Type + Address`。这样同一个地址被拿去当不同类型用时，系统能更早发现问题。

## 二、创建 Unity 6 工程

1. 用 Unity Hub 新建 Unity 6 项目，建议 2D Core 模板。
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

## 四、一步一步写代码

### 第 1 步：先创建 `ResourceManager.cs` 骨架

本步目标：先有一个能挂在场景里的资源入口，不急着加载资源。

上一步状态：项目还是空的，没有资源系统。

本步新增文件：`教程/Unity6通用资源管理器/代码文件/ResourceSystem/ResourceManager.cs`

本步代码：

```csharp
using UnityEngine;

public sealed class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

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
}
```

Unity 操作：

1. 在 Hierarchy 新建空物体，命名 `ResourceSystem`。
2. 把 `ResourceManager` 挂到这个物体上。
3. 保存场景并运行。

Inspector：

- 这一集没有额外字段，保持空即可。

运行结果：

- 运行后这个物体不会随着切场景消失。
- `ResourceManager.Instance` 可以稳定拿到。

常见错误：

- 场景里挂了两个 `ResourceManager`，会自动销毁一个。
- 忘记把对象放到启动场景里，`Instance` 就是空。

下一步依赖：

- 下一步要开始做“资源持有和释放”，所以这个单例入口必须先跑通。

### 第 2 步：创建 `ResourceHandle.cs`

本步目标：把“谁持有资源、谁负责释放”分开。

上一步状态：只有单例入口，还不能真正持有资源。

本步新增文件：`教程/Unity6通用资源管理器/代码文件/ResourceSystem/ResourceHandle.cs`

本步代码：

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

讲解要点：

1. 句柄不是简单包装资源，而是资源所有权。
2. `Asset` 只读暴露，避免业务层乱改释放逻辑。
3. `Dispose()` 是释放的唯一出口，后面所有资源生命周期都要回到这里。

Unity 操作：

- 这一集只改脚本，不需要额外编辑器操作。

运行结果：

- 资源句柄可以像普通对象一样被 `using` 或 `Dispose()` 释放。

常见错误：

- 句柄被重复释放。
- 业务层把 `Asset` 存太久，忘了在对象销毁时释放句柄。

下一步依赖：

- 下一步要让 `ResourceManager` 真正返回这个句柄。

### 第 3 步：创建 `ResourceSceneHandle.cs`

本步目标：先把场景加载与卸载的生命周期也准备好。

上一步状态：有了资源句柄，但还没有场景句柄。

本步新增文件：`教程/Unity6通用资源管理器/代码文件/ResourceSystem/ResourceSceneHandle.cs`

本步代码：

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

讲解要点：

1. 场景也是资源，不能只管加载不管卸载。
2. `ResourceSceneHandle` 是把“场景加载”和“场景释放”捆在一起的关键壳子。
3. `LoadSceneAsync` 返回的不是场景本身，而是一个能继续卸载的句柄。

Unity 操作：

- 这一集还是只改脚本。

运行结果：

- 你已经有了场景卸载的出口。

常见错误：

- 只记住 `SceneInstance`，不保留释放入口。
- 场景卸载后还继续访问旧对象。

下一步依赖：

- 下面开始给 `ResourceManager` 加缓存和 key。

### 第 4 步：给 `ResourceManager` 加缓存和资源键

本步目标：让同地址、同类型的资源可以命中缓存。

上一步状态：入口、句柄、场景句柄都已经有了。

本步修改文件：`ResourceManager.cs`

本步代码：

```csharp
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

private readonly Dictionary<ResourceKey, ResourceEntry> _cache = new();
private readonly Dictionary<ResourceKey, Task<UnityEngine.Object>> _loading = new();
```

讲解要点：

1. `Type + Address` 的 key 是为了避免同地址不同类型串资源。
2. 缓存项里不只放资源，还要放原始句柄，因为真正释放的是 Addressables 句柄。
3. `RefCount` 是这套系统的核心控制量。

Unity 操作：

- 这一集还是只改脚本。

运行结果：

- 同类型、同地址的资源后面可以直接复用。

常见错误：

- 只用地址做 key，类型一乱就冲突。
- 缓存里只存资源，不存引用计数，后面无法正确释放。

下一步依赖：

- 下一步要把缓存和真正的 Addressables 加载接起来。

### 第 5 步：加入初始化、并发合并和重试

本步目标：真正开始异步加载，并且避免同地址重复请求。

上一步状态：已经能记住资源，但还不能稳定地异步加载。

本步修改文件：`ResourceManager.cs`

本步代码：

```csharp
private Task _initializationTask;
private bool _initialized;

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
```

```csharp
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
```

讲解要点：

1. `EnsureInitializedAsync` 放在所有加载前面，是为了保证 Addressables 只初始化一次。
2. `_cache` 先查，是最省成本的路径。
3. `_loading` 的作用是把同地址并发请求合并成一次加载。
4. `TaskCompletionSource` 的意义是把“第一个请求”和“后来的等待者”接到同一个完成信号上。

Unity 操作：

1. 在场景里把 `ResourceManager` 挂好。
2. 准备一个 Addressables 资源地址。
3. 先不要做 UI，后面再测。

运行结果：

- 同一个地址在短时间内被多个地方请求时，只会走一次真正加载。

常见错误：

- 地址空字符串，直接报参数错误。
- 地址拼错，导致重试后还是失败。
- 不先初始化 Addressables，第一次加载会抛异常。

下一步依赖：

- 下一步要给失败场景加重试和取消。

### 第 6 步：把最小 Demo 脚本接进来

本步目标：让一个 UI 组件真正拿到异步加载回来的 Sprite，并在对象销毁时正确释放。

上一步状态：资源系统已经能加载了，但还没有使用方。

本步新增文件：`教程/Unity6通用资源管理器/代码文件/Demo/ResourceDemo.cs`

本步代码：

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

Unity 操作：

1. 在场景中新建一个 UI Image。
2. 新建空物体挂 `ResourceDemo`。
3. 把 Image 拖到 `target`。
4. 把一个 Addressables Sprite 地址填到 `spriteAddress`。

Inspector：

- `spriteAddress`：填真实 Addressables 地址。
- `target`：拖场景里的 Image。

运行结果：

- 启动后图标会异步显示出来。
- 退出对象时句柄释放。

常见错误：

- 忘了挂 `Image`。
- 地址没进 Addressables。
- 对象销毁后还继续访问 UI。

下一步依赖：

- 下一步要从资源加载扩展到 Prefab 实例化。

### 第 7 步：加入 Prefab 实例化和释放

本步目标：让资源管理器不仅能加载资源，还能创建并释放 Addressables 实例。

上一步状态：Sprite 已经可以异步加载到 UI。

本步修改文件：`ResourceManager.cs`

本步代码：

```csharp
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
```

Unity 操作：

1. 准备一个 Prefab。
2. 把 Prefab 放进 Addressables。
3. 运行时调用 `InstantiateAsync`。

Inspector：

- Prefab 的 Address 填成稳定规则，例如 `Prefab/Enemy/Goblin`。

运行结果：

- Prefab 可以被创建出来，销毁时也能按 Addressables 的方式回收。

常见错误：

- 直接 `Destroy(go)`，但资源侧没释放。
- 实例化时忘了传父节点，层级会乱。

下一步依赖：

- 下一步把场景也纳入资源系统。

### 第 8 步：加入场景加载与卸载

本步目标：让场景也当成资源来管理。

上一步状态：Prefab 已经能实例化。

本步修改文件：`ResourceManager.cs` 和 `ResourceSceneHandle.cs`

本步代码：

```csharp
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
```

Unity 操作：

1. 把一个测试 Scene 加入 Addressables。
2. 用脚本调用 `LoadSceneAsync`。
3. 测试卸载流程。

Inspector：

- 场景地址建议用 `Scene/Level01` 这种稳定命名。

运行结果：

- 场景可加载，也可通过句柄正常卸载。

常见错误：

- 只记住 `SceneInstance`，不保留释放入口。
- 场景卸载后还继续访问旧对象。

下一步依赖：

- 下一步给资源系统加预加载和清理。

### 第 9 步：加入预加载与清理

本步目标：让资源系统能提前把资源放进缓存，也能在退出时把句柄收干净。

上一步状态：加载、实例化、场景都能用了。

本步修改文件：`ResourceManager.cs`

本步代码：

```csharp
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
```

Unity 操作：

1. 在进入主界面前调用 `PreloadAsync<Sprite>`。
2. 再进入实际界面。
3. 确认第二次请求直接命中缓存。

运行结果：

- 资源会先进入缓存，后续加载更快。
- 切场景和退出时能清干净句柄。

常见错误：

- 预加载和正式加载都记引用，最后引用计数混乱。
- 预加载后忘了在真正使用时再增加持有关系。

下一步依赖：

- 下一步做一次完整测试收口。

### 第 10 步：测试矩阵和常见错误

本步目标：把最容易出问题的地方一次讲透。

上一步状态：项目已经能从零跑到完整资源管理器。

本步代码：不新增新代码，重点是跑测。

测试项：

| 场景 | 操作 | 预期 |
|---|---|---|
| 首次加载 | 调用 `LoadHandleAsync<Sprite>` | 成功显示图片 |
| 重复加载 | 连续请求同一地址 | 复用缓存或并发任务 |
| Dispose 释放 | 释放句柄 | 引用归零后回收 |
| 错误地址 | 输入错误 address | 记录失败日志并返回空 |
| 物体销毁中加载 | 加载时关闭物体 | 取消且不访问已销毁 UI |
| Prefab 实例化 | 实例创建后销毁 | 句柄可释放 |
| 场景加载卸载 | 加载后卸载场景 | 不残留句柄 |

常见错误：

- 资源地址没进 Addressables。
- Unity 里忘了 Build Player Content。
- 对象销毁后还在访问 UI。
- 不同类型用同一个地址，导致类型冲突。

## 五、附录总结

### 推荐录制顺序

1. 目标与架构。
2. Unity 6 工程和 Addressables 初始化。
3. 目录与资源地址规范。
4. `ResourceManager` 单例。
5. `ResourceHandle<T>`。
6. `ResourceSceneHandle`。
7. 缓存、引用计数、并发合并。
8. 失败重试与取消。
9. Sprite Demo。
10. Prefab 实例化。
11. Scene 加载和卸载。
12. 预加载与清理。
13. 测试与常见错误。

### 面试表达

> 我在 Unity 6 里实现了一套基于 Addressables 的通用资源管理器。它统一处理资源初始化、异步加载、缓存、引用计数、并发合并、重试、取消、Prefab 实例化和场景卸载，业务层只需要拿地址和类型，不需要直接管理 Addressables 句柄。这样既能减少重复代码，也能避免资源泄漏和生命周期错乱。

### 代码文件说明

完整脚本都在这里：

```text
教程/Unity6通用资源管理器/代码文件/
  ResourceSystem/ResourceManager.cs
  ResourceSystem/ResourceHandle.cs
  ResourceSystem/ResourceSceneHandle.cs
  Demo/ResourceDemo.cs
```

你录视频时可以直接按“先讲目标，再写代码，再跑结果”的顺序推进，这样教程会更像真正的项目实战，而不是步骤清单。
