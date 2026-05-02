/* Auto-Stack Bait & Ammo
 *
 * SMAPI mod that automatically adds any new bait to fishing poles that
 * have the same type of bait attached and new ammo to slingshots that
 * have the same type of ammo attached.
 *
 * Copyright (C) 2024, 2026 Jonathan Feenstra
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Runtime.CompilerServices;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using Object = StardewValley.Object;

namespace AutoStackBaitAndAmmo;

internal sealed class ModEntry : Mod
{
    private static Item? s_lastCursorSlotItem;

    public override void Entry(IModHelper helper)
    {
        helper.Events.Player.InventoryChanged += OnInventoryChanged;
        helper.Events.World.ChestInventoryChanged += OnChestInventoryChanged;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.Display.MenuChanged += OnMenuChanged;
    }

    private static void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (!e.IsLocalPlayer) return;
        
        ReadOnlySpan<Item> addedItems = Unsafe.As<Item[]>(e.Added);
        if (addedItems.Length == 0) return;
        
        var inventory = e.Player.Items;
        bool skipLastCursorSlotItem = s_lastCursorSlotItem is not null && IsInventoryMenuOpen();
        
        foreach (var addedItem in addedItems)
        {
            if (addedItem is not Object addedObject || (skipLastCursorSlotItem && addedObject == s_lastCursorSlotItem))
                continue;

            ProcessAddedItem(inventory, addedObject);
        }
    }

    private static void OnChestInventoryChanged(object? sender, ChestInventoryChangedEventArgs e)
    {
        ReadOnlySpan<Item> addedItems = Unsafe.As<Chest[]>(e.Added);
        var chestInventory = e.Chest.Items;
        foreach (var addedItem in addedItems)
        {
            if (addedItem is not Object addedObject) continue;
            ProcessAddedItem(chestInventory, addedObject);
        }
    }

    private static void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        var releasedButtons = Unsafe.As<SButton[]>(e.Released);
        if (releasedButtons.Length > 0)
        {
            s_lastCursorSlotItem = Game1.player.CursorSlotItem;
        }
    }
    
    private static void OnMenuChanged(object? sender, MenuChangedEventArgs e) => s_lastCursorSlotItem = null;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInventoryMenuOpen() =>
        Game1.activeClickableMenu is GameMenu menu && menu.currentTab == GameMenu.inventoryTab;

    private static void ProcessAddedItem(Inventory inventory, Object addedObject)
    {
        if (addedObject.Category == Object.baitCategory)
        {
            AddBaitToRods(inventory, addedObject);
        }
        else if (IsSlingshotAmmo(addedObject))
        {
            AddAmmoToSlingshots(inventory, addedObject);
        }
    }

    private static void AddBaitToRods(Inventory inventory, Object bait)
    {
        foreach (var item in inventory)
        {
            if (item is not FishingRod rod) continue;
            var attachedBait = rod.GetBait();
            if (attachedBait is null || !CanStackBait(attachedBait, bait)) continue;
            if (StackItems(inventory, attachedBait, bait)) return;
        }
    }

    private static void AddAmmoToSlingshots(Inventory inventory, Object ammo)
    {
        foreach (var item in inventory)
        {
            if (item is not Slingshot slingshot) continue;
            var attachedAmmo = slingshot.attachments[0];
            if (attachedAmmo is null || !CanStackAmmo(attachedAmmo, ammo)) continue;
            if (StackItems(inventory, attachedAmmo, ammo)) return;
        }
    }
    
    /// <summary>
    /// Stack <paramref name="addedObject"/> onto <paramref name="oldObject"/> as much as possible.
    /// Remove <paramref name="addedObject"/> from the <paramref name="inventory"/> if its stack is fully transferred,
    /// but keep an empty slot where <paramref name="addedObject"/> used to be (as to not shift any subsequent items to
    /// the left by one slot).
    /// </summary>
    /// <returns>
    /// True if the stack of <paramref name="addedObject"/> is fully transferred to <paramref name="oldObject"/>, false
    /// if there are any items left in <paramref name="addedObject"/> after filling <paramref name="oldObject"/> to its
    /// max stack size.
    /// </returns>
    /// <remarks>
    /// More simplified than the implementation of <see cref="StardewValley.Menus.InventoryMenu.tryToAddItem"/>.
    /// </remarks>
    private static bool StackItems(
        Inventory inventory,
        Object oldObject,
        Object addedObject)
    {
        int newStackSize = oldObject.Stack + addedObject.Stack;
        int maxSize = oldObject.maximumStackSize();
        if (newStackSize > maxSize)
        {
            oldObject.Stack = maxSize;
            addedObject.Stack = newStackSize - maxSize;
            return false;
        }

        oldObject.Stack = newStackSize;
        inventory.RemoveButKeepEmptySlot(addedObject);
        return true;
    }

    // based on Slingshot.canThisBeAttached
    private static bool IsSlingshotAmmo(Object obj) =>
        obj.ItemId switch
        {
            "378" or "380" or "382" or "384" or "386" or "388" or "390" or "441" => true,
            _ => obj.Category is -5 or -79 or -75,
        };

    // More optimized implementations of Item.canStackWith:
    // The following checks are skipped:
    // - Type checks (will always be StardewValley.Object, since bait and ammo cannot be ColoredObjects)
    // - maximumStackSize() > 1 checks (the StackItems method will handle this)
    // - For bait: Quality and orderData are not relevant
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanStackBait(Object bait1, Object bait2) =>
        bait1.ItemId == bait2.ItemId && bait1.name == bait2.name;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanStackAmmo(Object ammo1, Object ammo2) =>
        ammo1.ItemId == ammo2.ItemId 
        && ammo1.name == ammo2.name
        && ammo1.Quality == ammo2.Quality
        && ammo1.orderData.Value == ammo2.orderData.Value;
}