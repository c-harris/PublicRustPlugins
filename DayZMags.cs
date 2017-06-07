using Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DayZMags", "Jake_Rich", 0.2)]
    [Description("DayZMags")]
    public class DayZMags : RustPlugin
    {
        public static DayZMags _plugin { get; set; }

        public const float DRAG_RELOAD_UPDATE = 0.01f;
        public const string SHORTNAME_MAGAZINE = "pistol.eoka";
        public const string SHORTNAME_MODITEM = "battery.small";

        //TODO: Make magazines unequipable (don't let them move into hotbar perhaps?)
        //TODO: Drag unloading magazines out of weapons
        //TODO: Add monobehaviors (make sure they aren't saved / are unloaded upon unload)
        //TODO: Send network update when weapons are picked up (bugfix)
        //TODO: Make weapons have no ammo in them after they are created

        Item localPlayerWeaponFuck;
        Timer _timer;
        public static bool testing = false;
        private Dictionary<Item, Magazine> magData { get; set; } = new Dictionary<Item, Magazine>(); //TODO: Remove when items are destroyed

        public Magazine GetMagazine(Item item)
        {
            Magazine data;
            if (!magData.TryGetValue(item, out data))
            {
                data = new Magazine(item);
                magData.Add(item, data);
            }
            return data;
        }

        void TryReload(BasePlayer player, HeldEntity entity)
        {
            if (player.GetHeldEntity() != entity)
            {
                Puts("Not the same item!");
                return;
            }
            Item item = player.inventory.AllItems().Where(x => IsMagazine(x)).OrderByDescending(x => x.amount).FirstOrDefault();
            if (item == null)
            {
                Puts("No magazine in inventory!");
                return;
            }
            var magazine = GetMagazine(item);
            magazine.AddToWeapon(entity.GetItem());
        }

        public void LoadMagazine(Item weapon, Item magazine)
        {
            if (weapon.contents.itemList.Contains(magazine)) //Don't reload magazine if it's already in the gun
            {
                return;
            }
            TryUnloadMagazine(weapon);
            BaseProjectile weaponEntity = weapon.GetHeldEntity() as BaseProjectile;
            if (weaponEntity == null)
            {
                return;
            }
            weaponEntity.primaryMagazine.contents = magazine.amount;
            magazine.SetParent(weapon.contents);
            magazine.position = 0;
            weapon.MarkDirty();
            magazine.MarkDirty();
            weaponEntity.SendNetworkUpdateImmediate();
        }

        public bool TryUnloadMagazine(Item weapon)
        {
            var weaponEntity = weapon.GetHeldEntity() as BaseProjectile;
            if (weaponEntity == null)
            {
                Puts($"TryUnloadMagazine() not weapon!");
                return false;
            }
            var magazine = weapon.contents.itemList.FirstOrDefault(x => IsMagazine(x));
            if (magazine == null)
            {
                Puts($"TryUnloadMagazine() magazine is null!");
                return false;
            }
            var player = weapon.GetOwnerPlayer();
            if (player == null)
            {
                Puts($"TryUnloadMagazine() player is null!");
                return false;
            }
            player.GiveItem(magazine);
            weaponEntity.primaryMagazine.contents = 0;
            weaponEntity.SendNetworkUpdateImmediate();
            return true;
        }

        public void DragLoadMagazine(Item magazine, Item ammo)
        {

        }

        #region Hooks

        void Init()
        {
            _plugin = this;
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.RELOAD))
            {
                localPlayerWeaponFuck = ItemManager.CreateByPartialName("ammo.rifle");
                player.inventory.GiveItem(localPlayerWeaponFuck);
                _timer?.Destroy();
            }
            else if (input.WasJustReleased(BUTTON.RELOAD))
            {
                localPlayerWeaponFuck.Remove();
                localPlayerWeaponFuck.MarkDirty();
                _timer = timer.In(5f, () => { TryReload(player, player.GetHeldEntity()); });
            }

        }

        object CanMoveItem(Item movedItem, PlayerInventory playerLoot, uint targetContainerID, int targetSlot)
        {
            //Puts($"Can move item");
            var container = playerLoot.FindContainer(targetContainerID);
            if (container == null)
            {
                Puts("Container NULL");
                return null;
            }
            Item targetItem = container.GetSlot(targetSlot);

            #region Moving Magazines around without them unstacking

            if (IsMagazine(movedItem))
            {
                Puts(movedItem.info.shortname);
                if (movedItem.info.shortname == SHORTNAME_MODITEM)
                {
                    return null;
                }
                var magazine = GetMagazine(movedItem);
                Puts("Is Magazine");
                if (magazine.CanWeaponAccept(targetItem)) //Dragging magazine onto weapon
                {
                    Puts("Weapon Accepts moved magazine");
                    magazine.AddToWeapon(targetItem);
                }
                else if (magazine.CanWeaponAccept(container.parent)) //Dragging magazine into modification slots
                {
                    Puts("Weapon Accepts moved magazine");
                    magazine.AddToWeapon(container.parent);
                    return true;
                }
                /*else if (TryMoveToEmpty(container, movedItem, targetSlot)) //Dragging magazine into empty slots (not really needed)
                {
                    return true;
                }
                else
                {
                    SwapPosition(targetItem, movedItem); //Swapping items (not needed)
                }*/
                /*
                if (IsMagazine(item) || !IsAmmo(item))
                {
                    SwapPosition(item, movedItem);
                    return true;
                }*/
                return null;
            }

            #endregion

            #region Drag To Add Ammo to Magazine
            if (targetItem != null)
            {
                if (IsMagazine(targetItem)) //Item being dragged to is a Magazine
                {
                    var magazine = GetMagazine(targetItem);
                    if (IsAmmo(movedItem))
                    {
                        magazine.TryDragLoad(movedItem);
                        return true;
                    }
                }
            }

            #endregion

            #region Dragging Magazine out of weapon

            if (IsModMagazineItem(movedItem))
            {
                if (movedItem.contents == null)
                {
                    Puts("ModMagazineItem contents is null!");
                    return true;
                }
                if (movedItem.contents.itemList.Count == 0)
                {
                    Puts("ModMagazineItem contents is empty!");
                    return true;
                }
                GetMagazine(movedItem.contents.itemList[0]).RemoveFromWeapon(movedItem.parent.parent);
                return true;
            }

            #endregion

            return null;
        }

        //TODO: Add override if NoDespawning is loaded (add support to NoDespawning to override pickup single)
        object OnItemPickup(Item item, BasePlayer player)
        {
            if (IsMagazine(item))
            {
                player.GiveItem(item);
                return true;
            }
            return null;
        }

        //TODO: Increase modification slots of all weapons +1
        //TODO: Leave first slot free for weapon magazine
        //TODO: Decrease magazine bullet count when weapon is fired
        //TODO: Check if there is already a magazine in the gun when reloading
        //TODO: Hide bullet inserted into inventory when reloading gun
        //TODO: Magazine with 0 bullets
        //TODO: Only reload with invisible bullet if the player has the correct magazine for the gun
        //TODO: Only moved 29 bullets into an "empty" magazine
        //TODO: Alternate ammos for all bullets, to prevent players reloading without magazines

        //TODO: Function to detect if ammo fits in magazine, if magazine fits in gun, and config for magazine size + accepted weapons
        //TODO: Ammo package that can be unwrapped from airdrops / helis
        //TODO: Alternate capacity magazines for some guns (m249 can use stanag or m249 belt) (ak can use 75 round clip from heli drop)
        //TODO: Give weapon magazines (in loot, when crafting items)
        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (!IsMagazine(item))
            {
                //Puts($"OnItemRemoved() not magazine!");
                return;
            }
            if (container.parent == null)
            {
                //Puts($"OnItemRemoved() parent null!");
                return;
            }
            var weapon = container.parent.GetHeldEntity() as BaseProjectile;
            if (weapon == null)
            {
                //Puts($"OnItemRemoved() not weapon!");
                return;
            }
            weapon.primaryMagazine.contents = 0;
            weapon.SendNetworkUpdateImmediate();
        }

        object OnItemAction(Item item, string action)
        {
            if (action != "unload_ammo")
            {
                return null;
            }
            if (IsMagazine(item))
            {
                GetMagazine(item).Unload();
                return true;
            }
            if (HasMagazine(item))
            {
                //TODO: Cleanup solution
                magData.First(x => x.Value.IsModItem(item.contents.itemList[0])).Value.RemoveFromWeapon(item);
                //GetMagazine(item).RemoveFromWeapon();
                return true;
            }
            return null;
        }

        void OnWeaponFired(BaseProjectile weapon, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            var weaponItem = weapon.GetItem();
            if (weaponItem == null)
            {
                Puts("OnWeaponFired() weapon = null!");
                return;
            }
            if (!HasMagazine(weaponItem)) //Remove this if you add more features below
            {
                Puts("OnWeaponFired() weapon doesnt have magazine!");
                return;
            }
            var magazine = GetWeaponMagazine(weaponItem);
            magazine.UseAmmo();
        }

        #endregion

        #region Inventory Helpers

        public void SwapPosition(Item item1, Item item2)
        {
            if (item1.parent == item2.parent) //Swaping within same container
            {
                int pos1 = item1.position;
                item1.position = item2.position;
                item1.MarkDirty();
                item2.position = pos1;
                item2.MarkDirty();
                return;
            }
            var container2 = item2.parent;
            int item2Slot = item2.position;
            int item1Slot = item1.position;
            item2.MoveToContainer(item1.parent, item1Slot);
            item1.MoveToContainer(container2, item2Slot);
        }

        public bool TryMoveToEmpty(ItemContainer targetContainer, Item itemMoved, int slot)
        {
            if (targetContainer == itemMoved.parent)
            {
                if (targetContainer.SlotTaken(slot))
                {
                    return false;
                }
                itemMoved.position = slot;
                itemMoved.MarkDirty();
                return true;
            }
            if (targetContainer.SlotTaken(slot))
            {
                return false;
            }
            itemMoved.MoveToContainer(targetContainer, slot, false);
            return true;
        }

        #endregion

        #region Magazine Helpers

        public bool HasMagazine(Item item)
        {
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon == null)
            {
                return false;
            }
            if (item.contents == null)
            {
                return false;
            }
            return item.contents.itemList.Any(x => IsModMagazineItem(x));
        }

        public Magazine GetWeaponMagazine(Item weapon)
        {
            return GetMagazine(weapon.contents.itemList.FirstOrDefault(x => x.contents != null && IsMagazine(x.contents.itemList[0])));
        }

        public bool IsModMagazineItem(Item item)
        {
            if (item == null)
            {
                return false;
            }
            return item.info.shortname == SHORTNAME_MODITEM;
        }

        public bool IsMagazine(Item item)
        {
            if (item == null)
            {
                return false;
            }
            if (item.info.shortname != SHORTNAME_MAGAZINE)
            {
                return false;
            }
            return MagazineDefinitions.ContainsKey(item.skin);
        }

        public bool IsMagazine(ulong skinID)
        {
            return MagazineDefinitions.ContainsKey(skinID);
        }

        public bool IsMagazine(HeldEntity entity)
        {
            if (entity == null)
            {
                return false;
            }
            Item item = entity.GetItem();
            if (item == null)
            {
                return false;
            }
            return IsMagazine(item);
        }

        public bool IsAmmo(Item item)
        {
            //Keep as simple check for ammo type, to allow dragging ammo to magazines not move the magazine
            return item.info.name.Contains("ammo");
        }

        public bool IsWeaponType(string name)
        {
            switch (name)
            {

            }
            return true;
        }

        #endregion

        public MagazineInfo GetMagazineInfo(Item item)
        {
            return MagazineDefinitions[item.skin];
        }

        #region Classes

        public class MagazineInfo
        {
            public ulong _skinID { get; set; }
            public string _name { get; set; }
            public int _capacity { get; set; }
            public List<WeaponType> allowedWeapons { get; set; } = new List<WeaponType>();
            public AmmoType _allowedAmmo { get; set; }
            public int _amountPerReload { get; set; }
            public float _timePerReload { get; set; }

            public MagazineInfo(ulong skinID, string name, int capacity, AmmoType ammo, int reloadAmount, float timePerReload, params WeaponType[] weapons)
            {
                _allowedAmmo = ammo;
                _skinID = skinID;
                _name = name;
                _capacity = capacity;
                allowedWeapons = weapons.ToList();
                _amountPerReload = reloadAmount;
                _timePerReload = timePerReload;
            }
        }

        public class Magazine
        {
            Item _modItem { get; set; }
            Item _mag { get; set; }
            //Item _weapon { get; set; }
            BaseProjectile _magazineEntity { get; set; }
            MagazineInfo _definition { get; set; }
            Timer _timer { get; set; }
            private int _bullets { get; set; }
            private ItemDefinition _bulletDefinition { get; set; } = ItemManager.FindItemDefinition("ammo.shotgun");

            public Magazine(Item item)
            {
                _mag = item;
                _magazineEntity = item.GetHeldEntity() as BaseProjectile;
                _definition = _plugin.GetDefinition(item.skin);
                _bullets = _magazineEntity.primaryMagazine.contents;
            }

            public bool IsModItem(Item item)
            {
                return item != null && _modItem == item;
            }

            private void SetupModItem(BaseProjectile weaponEntity)
            {
                if (_modItem == null)
                {
                    _modItem = ItemManager.CreateByName(SHORTNAME_MODITEM, Mathf.Max(1, _bullets), _definition._skinID);
                }
                _modItem.amount = _bullets;
                _mag.RemoveFromContainer();
                _plugin.Puts("1");
                _modItem.SetParent(weaponEntity.GetItem().contents);
                _modItem.position = 0;
                _plugin.Puts("3");
                weaponEntity.GetItem().MarkDirty();
                _modItem.MarkDirty();
                if (_modItem.contents == null)
                {
                    _modItem.contents = new ItemContainer();
                    _modItem.contents.ServerInitialize(_modItem, 1);
                    _modItem.contents.GiveUID(); //I dont remember how much of this you need, but I aint testing it
                    _modItem.contents.allowedContents = ItemContainer.ContentsType.Generic;
                    _modItem.contents.SetLocked(false);
                }
                _mag.MoveToContainer(_modItem.contents);
                _plugin.Puts("5");
                weaponEntity.SendNetworkUpdateImmediate();
                _plugin.Puts("7");
            }

            public void UseAmmo(int amount = 1)
            {
                if (_bullets == 0)
                {
                    return;
                }
                _bullets = Mathf.Max(0, _bullets--);
                UpdateAmmoCount();
            }

            public void Unload()
            {
                var player = _mag?.parent.GetOwnerPlayer();
                if (player == null)
                {
                    _plugin.Puts($"Magazine.Unload() null player!");
                    return;
                }
                player.GiveItem(ItemManager.Create(_bulletDefinition, _bullets)); //TODO: Change this to handle overriden bullet types
                _bullets = 0;
                UpdateAmmoCount();
            }

            public void FillAmmo(Item ammo)
            {
                int amount = _definition._amountPerReload;
                if (_bullets >= _definition._capacity)
                {
                    return;
                }
                if (_bullets + amount > _definition._capacity)
                {
                    amount = _definition._capacity - _bullets;
                }
                amount = Mathf.Min(amount, ammo.amount);
                ammo.UseItem(amount);

                _bullets += amount;
                UpdateAmmoCount();
            }

            private bool IsFull()
            {
                return _bullets >= _definition._capacity;
            }

            private void UpdateAmmoCount()
            {
                _magazineEntity.primaryMagazine.contents = _bullets;
                _magazineEntity.SendNetworkUpdateImmediate();
                _mag.MarkDirty();
                if (_modItem != null)
                {
                    _modItem.amount = _bullets;
                    _modItem.MarkDirty();
                }
                //TODO: Update item here as well
            }

            public bool CanWeaponAccept(Item weapon)
            {
                if (weapon == null)
                {
                    return false;
                }
                WeaponType type;
                if (!_plugin.weaponAssignments.TryGetValue(weapon.info.shortname, out type))
                {
                    return false;
                }
                return _definition.allowedWeapons.Contains(type);
            }

            public void RemoveFromWeapon(Item weapon)
            {
                BaseProjectile weaponEntity = weapon.GetHeldEntity() as BaseProjectile;
                weaponEntity.primaryMagazine.contents = 0;
                _modItem.RemoveFromContainer();
                weaponEntity.GetOwnerPlayer().GiveItem(_mag);
                _mag.MarkDirty();
                _mag.GetHeldEntity().SendNetworkUpdate();
                weaponEntity.SendNetworkUpdateImmediate();
            }

            public void TryDragLoad(Item draggedItem)
            {
                if (HasCooldown())
                {
                    _plugin.Puts("Drag reload has cooldown!");
                    return;
                }
                if (IsFull())
                {
                    _plugin.Puts("Drag reload already full!");
                    return;
                }
                FillAmmo(draggedItem);
                _plugin.Puts("Drag loaded magazine");
                StartCooldown();
            }

            public void AddToWeapon(Item weapon)
            {
                if (weapon == null)
                {
                    return;
                }
                if (_mag?.parent?.parent == weapon) //Don't reload magazine if it's already in the gun
                {
                    return;
                }
                if (_plugin.HasMagazine(weapon))
                {
                    _plugin.GetWeaponMagazine(weapon).RemoveFromWeapon(weapon);
                    //unload weapon
                }
                BaseProjectile weaponEntity = weapon.GetHeldEntity() as BaseProjectile;
                if (weaponEntity == null)
                {
                    return;
                }
                weaponEntity.primaryMagazine.contents = _bullets;
                SetupModItem(weaponEntity);
            }

            #region Drag-Reload cooldown

            private void StartCooldown()
            {
                _timer?.Destroy();
                _timer = _plugin.timer.Every(DRAG_RELOAD_UPDATE, CooldownTick);
            }

            private bool HasCooldown()
            {
                return _mag.condition != 1;
            }

            private void StopCooldown()
            {
                _timer.Destroy();
                _mag.condition = 1;
            }

            private void CooldownTick()
            {
                _mag.conditionNormalized += DRAG_RELOAD_UPDATE / _definition._timePerReload;
                //_plugin.Puts($"Update: Mag condition is {_mag.conditionNormalized} ({_mag.condition}/{_mag.maxCondition}) ({DRAG_RELOAD_UPDATE * _definition._timePerReload * _mag.info.condition.max})");
                UpdateAmmoCount();
                if (_mag.conditionNormalized >= 1)
                {
                    StopCooldown();
                }
            }

            #endregion
        }

        public class Weapon : MonoBehaviour
        {
            private Magazine _mag { get; set; }
            private BaseProjectile _entity { get; set; }

            private void Awake()
            {
                _entity = GetComponent<BaseProjectile>();
                var magItem = _entity.GetItem().contents.itemList.FirstOrDefault(x => _plugin.IsModMagazineItem(x));
                if (magItem == null)
                {
                    return;
                }
                _mag = _plugin.GetMagazine(magItem.contents.itemList[0]);
            }

            public void OnWeaponFired()
            {
                _mag.UseAmmo();
            }

            public Magazine GetMagazine()
            {
                return _mag;
            }

            public void AttachMagazine(Magazine mag)
            {
                _mag = mag;
                _mag.AddToWeapon(_entity.GetItem());
            }

            public void DetachMagazine()
            {
                _mag.RemoveFromWeapon(_entity.GetItem());
            }

        }

        #endregion

        #region Config values

        public enum WeaponType
        {
            None = 0,
            Ak47 = 1,
            Semi_Rifle = 2,
            LR300 = 3,
            M249 = 4,
            PumpShotgun = 5,
        }

        public enum AmmoType
        {
            None = 0,
            Ammo_Rifle = 1,
            Ammo_Pistol = 2,
            Ammo_Slug = 3,
            Ammo_Buck = 4,
            Ammo_HandmadeShell = 5,
            Ammo_Arrow = 6,
        }

        public Dictionary<ulong, MagazineInfo> MagazineDefinitions = new Dictionary<ulong, MagazineInfo>()
        {
            {939979493u, new MagazineInfo(939979493u, "STANAG Magazine", 30, AmmoType.Ammo_Rifle, 3, 0.6f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {940561694u, new MagazineInfo(940561694u, "75Rnd DrumMag", 75, AmmoType.Ammo_Rifle, 3, 1.0f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941402580u, new MagazineInfo(941402580u, "5Rnd Shotgun", 5, AmmoType.Ammo_Buck, 1, 1.0f, WeaponType.PumpShotgun)},
            {941400934u, new MagazineInfo(941400934u, "8Rnd Shotgun", 8, AmmoType.Ammo_Buck, 1, 1.2f, WeaponType.PumpShotgun)},
            {941405262u, new MagazineInfo(941405262u, "20Rnd Shotgun", 20, AmmoType.Ammo_Buck, 1, 2f, WeaponType.PumpShotgun)},
            {941865444u, new MagazineInfo(941865444u, "10Rnd CMAG", 10, AmmoType.Ammo_Rifle, 2, 0.4f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941868420u, new MagazineInfo(941868420u, "20Rnd CMAG", 20, AmmoType.Ammo_Rifle, 2, 0.6f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941869375u, new MagazineInfo(941869375u, "30Rnd CMAG", 30, AmmoType.Ammo_Rifle, 2, 0.8f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941871543u, new MagazineInfo(941871543u, "40Rnd CMAG", 40, AmmoType.Ammo_Rifle, 2, 1.0f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941873168u, new MagazineInfo(941873168u, "100Rnd CMAG", 100, AmmoType.Ammo_Rifle, 3, 2.0f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
        };

        public Dictionary<string, WeaponType> weaponAssignments = new Dictionary<string, WeaponType>()
        {
            {"rifle.semiauto", WeaponType.Semi_Rifle},
            {"rifle.ak", WeaponType.Ak47},
            {"lmg.m249", WeaponType.M249},
            {"rifle.lr300", WeaponType.LR300},
            {"shotgun.pump", WeaponType.PumpShotgun },
        };

        #endregion

        #region Dev helper functions

        public void GiveMagazine(BasePlayer player, MagazineInfo type)
        {
            //Item item = ItemManager.CreateByPartialName("battery");
            Item item = ItemManager.CreateByPartialName(SHORTNAME_MAGAZINE);
            item.skin = type._skinID;
            item.name = GetMagazineInfo(item)._name;
            item.info.condition.max = 10000;
            item.maxCondition = item.info.condition.max;
            item.condition = 1;
            if (item.contents != null)
            {
                item.contents.capacity = 0;
            }
            Puts($"{item.GetHeldEntity().GetType().Name}");
            var weapon = item.GetHeldEntity() as BaseProjectile;
            weapon.primaryMagazine.contents = 0;
            player.inventory.GiveItem(item);
        }

        [ChatCommand("mag")]
        void TestMagazines(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            foreach (var mag in MagazineDefinitions)
            {
                GiveMagazine(player, mag.Value);
            }
        }

        #endregion

        #region Magazines Definitions

        public MagazineInfo GetDefinition(ulong skinID)
        {
            return MagazineDefinitions[skinID];
        }

        #endregion
    }
}
