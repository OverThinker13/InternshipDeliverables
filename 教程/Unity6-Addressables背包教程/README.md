# Unity 6 + Addressables 道具与背包系统完整教程

本教程只分为两个部分：前半部分是从空项目开始边敲代码边讲解的连续教程；后半部分是与教程最终结果完全一致的总代码附录。不会再出现重复的“主线、录制稿、附录”结构。

最终完成一个独立游戏/实习面试级道具背包：

```text
道具 ScriptableObject 配置
→ Addressables 图标加载
→ 玩家道具数据和堆叠
→ 背包格子 UI
→ 分类、排序、详情
→ 使用、出售
→ 拖动、交换位置、拖到快捷栏
→ JSON 存档
```

## 第一部分：边敲代码边完成道具与背包

### 1. 创建 Unity 6 项目

用 Unity Hub 创建 Unity 6 2D 项目，项目名 `AddressableInventoryDemo`。创建以下目录：

```text
Assets/Art/Items
Assets/Prefabs/UI
Assets/Scripts/Inventory
Assets/Scripts/UI
Assets/Scenes
```

安装 Addressables 和 TextMeshPro。打开 `Window > Asset Management > Addressables > Groups`，点击 `Create Addressables Settings`。创建 `ItemIcons` 和 `ItemDefinitions` 两个 Packed Assets Group。

### 2. 先定义道具类型

创建 `Assets/Scripts/Inventory/ItemTypes.cs`，输入：

```csharp
namespace Demo.Inventory
{
    public enum ItemCategory { Resource, Consumable, Material, Quest, Equipment }
    public enum ItemRarity { Common = 1, Uncommon, Rare, Epic, Legendary }
}
```

保存并等待编译。此时项目还没有背包，只有道具分类的基础类型。

### 3. 创建道具配置 ItemDefinition

新建 `ItemDefinition.cs`。这一步把“道具是什么”定义出来，不保存玩家拥有数量：

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Demo.Inventory
{
    [CreateAssetMenu(menuName = "Inventory/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        public int itemId;
        public string itemName;
        [TextArea] public string description;
        public ItemCategory category;
        public ItemRarity rarity;
        public AssetReferenceSprite iconReference;
        public bool stackable = true;
        public int maxStack = 99;
        public bool usable;
        public bool sellable;
        public int sellPrice;
    }
}
```

右键 Project：`Create > Inventory > Item Definition`，创建三个资源：

| ID | 名称 | 分类 | 品质 | MaxStack | Usable | Sellable |
|---:|---|---|---|---:|---|---|
| 1001 | 木材 | Resource | Common | 999 | 否 | 是 |
| 1002 | 生命药水 | Consumable | Uncommon | 99 | 是 | 是 |
| 1003 | 宝石 | Material | Epic | 20 | 否 | 是 |

准备三张图片，标记 Addressable，放入 `ItemIcons` Group，创建 Label `ItemIcon`。把三个 ItemDefinition 也标记 Addressable，放入 `ItemDefinitions` Group，创建 Label `ItemDefinition`。将图标拖到每个配置的 Icon Reference。

运行前执行 `Build > New Build > Default Build Script`。

### 4. 创建玩家道具数据

新建 `InventorySlot.cs`：

```csharp
using System;
namespace Demo.Inventory
{
    [Serializable]
    public class InventorySlot
    {
        public int itemId;
        public int amount;
        public int position;
        public InventorySlot(int id, int count, int pos)
        { itemId = id; amount = count; position = pos; }
    }
}
```

这里 `position` 是拖动交换后的背包位置。配置资源永远不会被拖动修改，拖动修改的是 Slot。

### 5. 编写配置数据库

新建 `InventoryDatabase.cs`，逐行输入完整代码：

```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Demo.Inventory
{
    public class InventoryDatabase
    {
        private readonly Dictionary<int, ItemDefinition> definitions = new();
        private AsyncOperationHandle<IList<ItemDefinition>> handle;

        public async UniTask InitializeAsync()
        {
            handle = Addressables.LoadAssetsAsync<ItemDefinition>(
                "ItemDefinition", item =>
                {
                    if (item != null) definitions[item.itemId] = item;
                });
            await handle.Task;
        }

        public bool TryGet(int id, out ItemDefinition item)
            => definitions.TryGetValue(id, out item);

        public void Release()
        {
            if (handle.IsValid()) Addressables.Release(handle);
            definitions.Clear();
        }
    }
}
```

### 6. 编写背包服务：增加、删除、堆叠、位置

新建 `InventoryService.cs`。它是唯一修改库存的地方：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo.Inventory
{
    public class InventoryService
    {
        public const int Capacity = 30;
        private readonly InventoryDatabase database;
        private readonly Dictionary<int, InventorySlot> slots = new();
        public event Action OnChanged;
        public IReadOnlyCollection<InventorySlot> Slots => slots.Values;

        public InventoryService(InventoryDatabase db) { database = db; }

        public bool AddItem(int id, int amount)
        {
            if (amount <= 0 || !database.TryGet(id, out var def)) return false;
            if (slots.TryGetValue(id, out var slot))
            {
                if (!def.stackable) return false;
                slot.amount = Math.Min(def.maxStack, slot.amount + amount);
            }
            else
            {
                int pos = FirstFreePosition();
                if (pos < 0) return false;
                slots[id] = new InventorySlot(id,
                    def.stackable ? Math.Min(amount, def.maxStack) : 1, pos);
            }
            OnChanged?.Invoke(); return true;
        }

        public bool RemoveItem(int id, int amount)
        {
            if (amount <= 0 || !slots.TryGetValue(id, out var slot) || slot.amount < amount)
                return false;
            slot.amount -= amount;
            if (slot.amount == 0) slots.Remove(id);
            OnChanged?.Invoke(); return true;
        }

        public bool MoveItem(int from, int to)
        {
            var a = slots.Values.FirstOrDefault(x => x.position == from);
            var b = slots.Values.FirstOrDefault(x => x.position == to);
            if (a == null) return false;
            a.position = to;
            if (b != null) b.position = from;
            OnChanged?.Invoke(); return true;
        }

        public int GetAmount(int id) => slots.TryGetValue(id, out var s) ? s.amount : 0;
        public ItemDefinition GetDefinition(int id) => database.TryGet(id, out var d) ? d : null;
        public int FirstFreePosition()
        {
            for (int i = 0; i < Capacity; i++)
                if (!slots.Any(x => x.Value.position == i)) return i;
            return -1;
        }
        public List<InventorySlot> GetOrdered(ItemCategory? category = null)
            => slots.Values.Where(s => !category.HasValue || GetDefinition(s.itemId)?.category == category)
                .OrderBy(s => s.position).ToList();
    }
}
```

此时可以创建一个临时测试脚本，在 `Start` 中调用 `AddItem(1002,20)`。运行后确认 Service 数据正确，再继续 UI。

### 7. 创建 Runtime 初始化入口

新建 `InventoryRuntime.cs`，挂到场景空物体 `InventoryRuntime`：

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Demo.Inventory
{
    public class InventoryRuntime : MonoBehaviour
    {
        public static InventoryRuntime Instance { get; private set; }
        public InventoryService Service { get; private set; }
        private InventoryDatabase database;

        private async void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            database = new InventoryDatabase();
            await database.InitializeAsync();
            if (this == null) return;
            Service = new InventoryService(database);
            Service.AddItem(1002, 20);
            Service.AddItem(1001, 80);
            Service.AddItem(1003, 3);
        }

        private void OnDestroy() => database?.Release();
    }
}
```

### 8. 搭建背包 UI

创建：`Canvas/InventoryPanel/Scroll View/Viewport/Content`。Content 添加 `GridLayoutGroup`：

```text
Constraint: Fixed Column Count
Count: 6
Cell Size: 100 x 100
Spacing: 8 x 8
```

创建格子预制体：

```text
InventoryItemView
├── Icon (Image)
├── AmountText (TMP_Text)
├── NameText (TMP_Text)
└── DragHandle (空物体)
```

创建 30 个空格也可以，但更推荐运行时生成 30 个格子，空格子负责接收拖动。

### 9. 编写格子显示和拖动代码

新建 `InventoryItemView.cs`，实现点击、拖动开始、拖动结束：

```csharp
using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo.Inventory
{
    public class InventoryItemView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private CanvasGroup canvasGroup;
        private InventorySlot slot;
        private Action<InventorySlot> clicked;
        private Action<int, int> dropped;
        private int position;

        public void Bind(InventorySlot data, ItemDefinition def,
            Action<InventorySlot> onClick, Action<int, int> onDrop)
        {
            slot = data; position = data.position;
            clicked = onClick; dropped = onDrop;
            nameText.text = def.itemName;
            amountText.text = data.amount.ToString();
            LoadIcon(def).Forget();
        }

        private async UniTaskVoid LoadIcon(ItemDefinition def)
        {
            if (def.iconReference == null) return;
            var sprite = await def.iconReference.LoadAssetAsync<Sprite>().Task;
            if (this == null) return;
            icon.sprite = sprite;
        }

        public void OnPointerClick(PointerEventData eventData) => clicked?.Invoke(slot);

        public void OnBeginDrag(PointerEventData eventData)
        { canvasGroup.blocksRaycasts = false; transform.SetAsLastSibling(); }

        public void OnDrag(PointerEventData eventData)
        { transform.position = eventData.position; }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            var target = eventData.pointerEnter?.GetComponentInParent<InventoryItemView>();
            dropped?.Invoke(position, target == null ? position : target.position);
        }
    }
}
```

### 10. 编写 Panel：生成格子、分类、交换位置

新建 `InventoryPanel.cs`。Panel 订阅 Service 事件；拖动结束后调用 `MoveItem`，而不是直接修改 UI：

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Demo.Inventory
{
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private Transform content;
        [SerializeField] private InventoryItemView itemPrefab;
        [SerializeField] private ItemDetailPanel detailPanel;
        private readonly List<InventoryItemView> views = new();
        private InventoryService service;
        private ItemCategory? category;

        private void OnEnable()
        {
            service = InventoryRuntime.Instance.Service;
            if (service == null) return;
            service.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        { if (service != null) service.OnChanged -= Refresh; }

        public void Refresh()
        {
            foreach (var view in views) Destroy(view.gameObject);
            views.Clear();
            foreach (var slot in service.GetOrdered(category))
            {
                var def = service.GetDefinition(slot.itemId);
                var view = Instantiate(itemPrefab, content);
                views.Add(view);
                view.Bind(slot, def, detailPanel.Show, OnDrop);
            }
        }

        private void OnDrop(int from, int to)
        { if (from != to) service.MoveItem(from, to); }

        public void ShowCategory(int index)
        { category = index < 0 ? null : (ItemCategory?)index; Refresh(); }
    }
}
```

拖动逻辑的关键是：UI 只负责报告 `from/to`，真正交换由 `InventoryService.MoveItem` 完成；事件触发后整个列表刷新，所以数据和显示不会分叉。

### 11. 创建详情面板、使用和出售

新建 `ItemDetailPanel.cs`，绑定 Name、Description、Amount、UseButton、SellButton：

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Demo.Inventory
{
    public class ItemDetailPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text nameText, descriptionText, amountText;
        [SerializeField] Button useButton, sellButton;
        private InventorySlot slot; private InventoryService service;

        public void Show(InventorySlot data)
        {
            slot = data; service = InventoryRuntime.Instance.Service;
            var def = service.GetDefinition(data.itemId);
            if (def == null) return;
            gameObject.SetActive(true);
            nameText.text = def.itemName;
            descriptionText.text = def.description;
            amountText.text = $"数量：{service.GetAmount(data.itemId)}";
            useButton.gameObject.SetActive(def.usable);
            sellButton.gameObject.SetActive(def.sellable);
            useButton.onClick.RemoveAllListeners();
            sellButton.onClick.RemoveAllListeners();
            useButton.onClick.AddListener(Use);
            sellButton.onClick.AddListener(Sell);
        }

        private void Use()
        {
            var def = service.GetDefinition(slot.itemId);
            if (def != null && def.usable && service.RemoveItem(slot.itemId, 1))
                Show(slot);
        }

        private void Sell()
        {
            var def = service.GetDefinition(slot.itemId);
            if (def != null && def.sellable && service.RemoveItem(slot.itemId, 1))
                Hide();
        }

        public void Hide() { slot = null; gameObject.SetActive(false); }
    }
}
```

### 12. 存档

存档只保存 `itemId、amount、position`，不保存 Sprite 和 Addressables Handle。创建 DTO 后，将 Service 中的 Slot 转为 JSON，启动时先加载 Database 再恢复 Slot。可以使用 PlayerPrefs 完成 Demo，也可以替换为文件系统。

### 13. 拖动边界测试

逐项测试：拖到空格、拖到另一个道具、拖回原位、拖出背包、拖动时关闭面板、拖动过程中切换分类。拖出有效区域时应回到原位置；如果目标为空，则只修改自己的 position；如果目标有物品，则交换两个 position。

### 14. 最终检查

执行 Addressables Build；运行后确认图标、数量、分类、详情、使用、出售、拖动和存档都有效。关闭面板时解绑事件；图标句柄在对象销毁或对象池回收时释放；所有异步 await 后检查对象是否仍存在。

## 第二部分：附录——完整总代码

本目录 `Scripts/Inventory` 和 `Scripts/UI` 中的文件，就是前面教程逐步敲出来的最终总代码。它们与教程中的最终版本保持一致：

```text
Scripts/Inventory/ItemCategory.cs
Scripts/Inventory/ItemDefinition.cs
Scripts/Inventory/InventorySlot.cs
Scripts/Inventory/InventoryDatabase.cs
Scripts/Inventory/InventoryService.cs
Scripts/Inventory/InventoryRuntime.cs
Scripts/UI/InventoryItemView.cs
Scripts/UI/InventoryPanel.cs
Scripts/UI/ItemDetailPanel.cs
```

复制顺序仍然是：类型 → 配置 → Slot → Database → Service → Runtime → 格子 → Panel → 详情。完成后可直接在 Unity 6 空项目中按第一部分的 Inspector 步骤组装运行。
