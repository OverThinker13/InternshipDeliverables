using System;
namespace Demo.Inventory { [Serializable] public sealed class InventorySlot { public int itemId; public int amount; public int position; public InventorySlot(int id,int count,int pos){itemId=id;amount=count;position=pos;} } }
