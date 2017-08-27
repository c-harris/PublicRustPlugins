using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StackSizeFix", "Jake_Rich", 1.0)]
    [Description("Fix High Stacksizes")]

    public class StackSizeFix : RustPlugin
    {
        object CanMoveItem(Item item, PlayerInventory inventory, uint container, int slot, uint amount)
        {
            int simAmount = 0;
            Puts($"Amount: {amount} {item.amount % UInt16.MaxValue} Guessed Amount: {simAmount} Slot: {slot}");
            if (item.amount < UInt16.MaxValue) //Moving normal amount of items
            {
                return null;
            }
            if (amount + item.amount / UInt16.MaxValue == item.amount % UInt16.MaxValue) //Moving max stacks
            {
                ItemContainer itemContainer = inventory.FindContainer(container);
                if (itemContainer == null)
                {
                    return true;
                }
                item.MoveToContainer(itemContainer, slot, true);
                return true;
            }
            else if (amount + (item.amount / 2) / UInt16.MaxValue == (item.amount / 2) % UInt16.MaxValue) //Moving half stack
            {
                ItemContainer itemContainer = inventory.FindContainer(container);
                if (itemContainer == null)
                {
                    return true;
                }
                Item item2 = item.SplitItem(item.amount / 2);
                if (!item2.MoveToContainer(itemContainer, slot, true))
                {
                    item.amount += item2.amount;
                    item2.Remove(0f);
                }
                ItemManager.DoRemoves();
                inventory.ServerUpdate(0f);
                return true;
            }

            return null;
        }
    }
}


