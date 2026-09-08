using System; using System.Collections.Generic;
namespace Demo.Inventory { [Serializable] public sealed class InventorySaveItem { public int itemId; public int amount; public int position; } [Serializable] public sealed class InventorySaveData { public List<InventorySaveItem> items=new(); } }
