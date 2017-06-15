using Network;
using Oxide.Game.Rust.Cui;
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
        public static bool testing = true;
        public static bool infiniteAmmo = false;
        public static Dictionary<Item, Magazine> magData { get; set; } = new Dictionary<Item, Magazine>(); //TODO: Remove when items are destroyed
        public static Dictionary<BasePlayer, PlayerData> playerData { get; set; } = new Dictionary<BasePlayer, DayZMags.PlayerData>();

        public static PlayerData GetPlayerData(BasePlayer player)
        {
            PlayerData data;
            if (!playerData.TryGetValue(player, out data))
            {
                data = new PlayerData(player);
                playerData.Add(player, data);
            }
            return data;
        }

        //TODO: When loading large magazine, set capacity of gun to the size of the magazine (may be clientside)

        #region Save / Load hooks

        void Init()
        {
            _plugin = this;
        }

        void OnServerInitialized()
        {
            AmmoSkinIDs = new HashSet<ulong>(AmmoDefinitions.Values.Select(x => x._skinID));
            WrappedAmmoSkinIDs = new HashSet<ulong>(WrappedAmmoDefinitions.Values.Select(x => x._skinID));
            MagazineSkinIDs = new HashSet<ulong>(MagazineDefinitions.Values.Select(x => x._skinID));
            var batDef = ItemManager.itemList.First(x => x.shortname == "battery.small");
            if (batDef.stackable == 1)
            {
                batDef.stackable = 2;
            }
            foreach (var weapon in GameObject.FindObjectsOfType<BaseProjectile>()) //Add custom component to all weapons
            {
                OnEntitySpawned(weapon);
            }
            
            foreach (var player in GameObject.FindObjectsOfType<BasePlayer>()) //Add custom component to all weapons
            {
                OnItemInit(player?.inventory?.AllItems());
            }

            foreach(var storage in GameObject.FindObjectsOfType<StorageContainer>())
            {
                OnItemInit(storage?.inventory?.itemList);
            }

        }

        void Unload()
        {
            foreach (var weapon in GameObject.FindObjectsOfType<Weapon>()) //Remove components
            {
                GameObject.Destroy(weapon);
            }
            var batDef = ItemManager.itemList.First(x => x.shortname == "battery.small");
            if (batDef.stackable == 2)
            {
                batDef.stackable = 1;
            }
        }

        #endregion

        #region Item hooks

        void OnItemInit(IEnumerable<Item> items)
        {
            if (items == null)
            {
                Puts($"OnItemInit() items is null");
                return;
            }
            foreach(var item in items)
            {
                OnItemInit(item);
            }
        }

        void OnItemInit(Item item)
        {
            var held = item?.GetHeldEntity();
            if (held == null)
            {
                return;
            }
            if (!(held is BaseProjectile))
            {
                return;
            }
            if (MagazineHelper.IsMagazine(item))
            {
                return;
            }
            if (held.GetComponent<Weapon>() == null)
            {
                held.gameObject.AddComponent<Weapon>();
            }
        }

        #endregion

        #region Inventory hooks

        object GetMaxStackable(Item item)
        {
            //Puts("GetMaxStackable");
            if (item.skin == 0)
            {
                return null;
            }
            if (AmmoHelper.IsAmmo(item))
            {
                Puts($"IsAmmo with stacksize of {AmmoHelper.GetAmmoInfo(item.skin)._stackLimit}");
                return AmmoHelper.GetAmmoInfo(item.skin)._stackLimit;
            }
            return null;
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

            #region Dragging Magazines onto weapons

            if (MagazineHelper.IsMagazine(movedItem))
            {
                if (MagazineHelper.IsMagazine(targetItem))
                {
                    return null;
                }
                var weaponEntity = targetItem?.GetHeldEntity();
                if (weaponEntity != null)
                {
                    if (weaponEntity.GetComponent<Weapon>() == null)
                    {
                        weaponEntity.gameObject.AddComponent<Weapon>();
                    }
                }
                if (movedItem.info.shortname == SHORTNAME_MODITEM)
                {
                    Puts($"IsMagazine() returned true for a MagazineModItem!");
                    return true;
                }
                var weapon = targetItem?.GetHeldEntity()?.GetComponent<Weapon>(); //Dragging magazine onto a weapon
                var magazine = MagazineHelper.GetMagazine(movedItem);
                if (weapon == null)
                {
                    weapon = container?.parent?.GetHeldEntity()?.GetComponent<Weapon>(); //Dragging a magazine into a weapons modification slots
                }
                if (weapon != null)
                {
                    weapon.AttachMagazine(magazine);
                    return true;
                }

                if (targetItem == null) { _plugin.Puts("targetItem is null"); }
                if (targetItem?.GetHeldEntity() == null) { _plugin.Puts("targetheld is null"); }
                if (targetItem?.GetHeldEntity()?.GetComponentInParent<Weapon>() == null) { _plugin.Puts("Component is null"); }
                if (targetItem == null) { _plugin.Puts(" is null"); }

                Puts("Is Magazine");


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
                if (MagazineHelper.IsMagazine(targetItem)) //Item being dragged to is a Magazine
                {
                    var magazine = MagazineHelper.GetMagazine(targetItem);
                    if (AmmoHelper.IsAmmo(movedItem))
                    {
                        magazine.TryDragLoad(movedItem);
                        return true;
                    }
                }
            }

            #endregion

            #region Dragging Magazine out of weapon

            if (MagazineHelper.IsModMagazineItem(movedItem))
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
                MagazineHelper.GetMagazine(movedItem.contents.itemList[0]).RemoveFromWeapon(movedItem.parent.parent);
                return true;
            }

            #endregion

            #region Moving around unstackable items

            #endregion

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
            if (!MagazineHelper.IsMagazine(item))
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
            switch (action)
            {
                #region Unloading Ammo from magazines, and magazines from guns
                case "unload_ammo":
                    {
                        if (MagazineHelper.IsMagazine(item))
                        {
                            MagazineHelper.GetMagazine(item).Unload();
                            return true;
                        }
                        var weapon = item.GetHeldEntity().GetComponent<Weapon>();
                        if (weapon.HasLoadedMagazine()) //TODO: Move to Weapon class (Unloading weapon's magazine based on mod item)
                        {
                            weapon.DetachMagazine();
                            return true;
                        }
                        break;
                    }
                #endregion

                case "unwrap":
                    {
                        if (!AmmoHelper.IsWrappedAmmo(item))
                        {
                            return null;
                        }
                        var player = item.GetOwnerPlayer();
                        if (player == null)
                        {
                            return true;
                        }
                        var info = AmmoHelper.GetWrappedAmmoInfo(item);
                        item.UseItem();
                        player.GiveItem(AmmoHelper.CreateAmmo(info._ammo, info._unwrapAmount));
                        return true;
                    }

            }
            return null;
        }

        object CanStackItem(Item item1, Item item2)
        {
            //Puts($"CanStack()");
            if (item1.skin == 0 || item2.skin == 0)
            {
                return null;
            }
            if (item1.skin != item2.skin)
            {
                return null;
            }
            if (AmmoHelper.IsAmmo(item1) && AmmoHelper.IsAmmo(item2))
            {
                Puts("CanStack() both are ammo!");
                return true;
            }
            return null;
        }

        object CanAcceptItem(ItemContainer container, Item item) //Prevent magazines (eoka) going into hotbar
        {
            if (MagazineHelper.IsMagazine(item))
            {
                var player = container?.GetOwnerPlayer();
                if (player == null)
                {
                    return null;
                }
                if (container == player.inventory.containerBelt)
                {
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }
            }
            return null;
        }

        #endregion

        #region Entity hooks

        void OnEntitySpawned(BaseNetworkable entity)
        {
            var projectile = entity as BaseProjectile;
            if (projectile != null)
            {
                if (!MagazineHelper.IsMagazine(projectile))
                {
                    return;
                }
                if (projectile.GetComponent<Weapon>() != null)
                {
                    return;
                }
                projectile.gameObject.AddComponent<Weapon>();
                Puts($"Added weapon component");
            }
        }

        #endregion

        #region Reload hooks (PlayerInput too)

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.RELOAD))
            {
                GetPlayerData(player).OnReloadButtonDown();
            }
            else if (input.WasJustReleased(BUTTON.RELOAD))
            {
                GetPlayerData(player).OnReloadButtonUp();
            }

        }

        object OnStartReload(BasePlayer player, BaseProjectile weapon)
        {
            Puts($"OnStartReload");
            GetPlayerData(player).OnReloadStart(weapon);
            return null;
        }

        object OnReloadMagazine(BasePlayer player, BaseProjectile weapon)
        {
            Puts($"OnReloadMagazine");
            GetPlayerData(player).OnReloadEnd(weapon);
            weapon.SendNetworkUpdateImmediate(false);
            global::ItemManager.DoRemoves();
            player.inventory.ServerUpdate(0f);
            return true;
        }

        #endregion

        #region Pickup Items

        //TODO: Add override if NoDespawning is loaded (add support to NoDespawning to override pickup single)
        object OnItemPickup(Item item, BasePlayer player)
        {
            if (MagazineHelper.IsMagazine(item))
            {
                player.GiveItem(item);
                return true;
            }
            return null;
        }

        #endregion

        #region Other hooks

        void OnWeaponFired(BaseProjectile baseProjectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            var weapon = baseProjectile.GetComponent<Weapon>();
            weapon.OnWeaponFired();
            if (infiniteAmmo)
            {
                baseProjectile.primaryMagazine.contents+= 1;
            }
        }

        #endregion

        //TODO: Remove
        #region Inventory Helpers (not needed, will remove later)

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

        #region Debug Messages

        public static void Error(string error)
        {
            if (testing)
            {
                _plugin.Puts($"Error: {error}");
            }
        }

        #endregion

        #region Classes
        //TODO: Magazines aren't getting initialized
        public class PlayerData
        {
            private BasePlayer _player { get; set; }
            private Item _fakeAmmo { get; set; }
            private bool _reloadStarted { get; set; }
            private bool _reloadFinished { get; set; }
            private BaseProjectile _reloadWeapon { get; set; }


            public PlayerData(BasePlayer player)
            {
                _player = player;
            }

            public void OnReloadButtonDown()
            {
                if (!HasMagazine())
                {
                    return;
                }
                var weaponInfo = GetWeapon().GetDefinition();
                if (weaponInfo == null)
                {
                    Error("OnReloadButtonDown() weaponInfo null!");
                    return;
                }
                var magazine = GetBestMagazine(weaponInfo.GetWeaponType());
                if (magazine == null)
                {
                    Error($"OnReloadButtonDown() magzine is null");
                    return;
                }
                CreateFakeAmmo(magazine); //TODO: Only create fake ammo if a valid magazine is in the player's inventory (based on players weapon)
            }

            public void OnReloadButtonUp()
            {
                RemoveFakeAmmo();
            }

            public void OnReloadStart(BaseProjectile weapon)
            {
                _reloadFinished = false;
                RemoveFakeAmmo();
                //this.StartReloadCooldown(this.reloadTime);

                //TODO: Eject magazine (only in hardcore mode)

                _reloadWeapon = weapon;
                _reloadStarted = true;
            }

            public void OnReloadEnd(BaseProjectile weaponEntity)
            {
                if (weaponEntity != _reloadWeapon)
                {
                    _plugin.Puts($"OnReloadEnd reload weapon doesnt equal current weapon");
                    CancelReload();
                    return;
                }
                if (!_reloadStarted)
                {
                    _plugin.Puts($"OnReloadEnd reload not started");
                    CancelReload();
                    return;
                }
                var weapon = weaponEntity.GetComponent<Weapon>();
                var mag = GetBestMagazine(weapon.GetWeaponType());
                if (mag == null)
                {
                    _plugin.Puts($"OnReloadEnd not valid magazines!");
                    return;
                }
                //Do reload here

                weapon.AttachMagazine(mag);
                _reloadWeapon = null;
            }

            public void OnSwitchWeapons()
            {
                RemoveFakeAmmo();
                CancelReload();
            }

            private void CancelReload()
            {
                RemoveFakeAmmo(); //Just to be safe

            }

            private void RemoveFakeAmmo()
            {
                if (_fakeAmmo == null)
                {
                    return;
                }
                _fakeAmmo.Remove();
                ItemManager.DoRemoves();
            }

            private void CreateFakeAmmo(Magazine magazine)
            {
                _fakeAmmo = AmmoHelper.CreateTempAmmo(magazine.GetLoadedAmmoType());
                if (_fakeAmmo == null)
                {
                    Error($"CreateFakeAmmo() _fakeAmmo null");
                }
                _player.inventory.GiveItem(_fakeAmmo);
            }

            public bool HasMagazine()
            {
                return _player.inventory.AllItems().Any(x => MagazineHelper.IsMagazine(x));
            }

            public bool HasMagazine(WeaponType weapon)
            {
                return _player.inventory.AllItems().Any(x => MagazineHelper.IsMagazine(x, weapon));
            }

            public Magazine GetBestMagazine(WeaponType weapon)
            {
                //TODO: Holy fucked up LINQ batman!
                return _player.inventory.AllItems().Where(x => MagazineHelper.IsMagazine(x, weapon)).Select(x=>MagazineHelper.GetMagazine(x)).Where(x=>x.Bullets > 0).OrderByDescending(x => x.Bullets).FirstOrDefault();
            }

            public bool HasWeapon()
            {
                return _player.GetHeldEntity() is BaseProjectile;
            }

            public Weapon GetWeapon()
            {
                return (_player.GetHeldEntity() as BaseProjectile).GetComponent<Weapon>();
            }

        }

        public class WeaponInfo
        {
            private HashSet<AmmoType> _allowedAmmo { get; set; }
            public WeaponType _type { get; set; }
            public int ModSlots { get; set; }

            public WeaponInfo(WeaponType type, int modSlots, params AmmoType[] ammo)
            {
                _allowedAmmo = new HashSet<AmmoType>(ammo);
                _type = type;
                modSlots = modSlots;
            }

            public bool AcceptsAmmo(AmmoType ammo)
            {
                return _allowedAmmo.Contains(ammo);
            }

            public bool AcceptsAmmo(Item item)
            {
                return AcceptsAmmo(AmmoHelper.GetAmmoInfo(item)._ammoType);
            }

            public WeaponType GetWeaponType()
            {
                return _type;
            }
        }

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

            public bool WeaponAccepts(WeaponType type)
            {
                return allowedWeapons.Contains(type);
            }
        }

        public class WrappedAmmoInfo
        {
            public AmmoType _ammo { get; set; }
            public string _name { get; set; }
            public ulong _skinID { get; set; }
            public int _stackLimit { get; set; }
            public int _unwrapAmount { get; set; }

            public WrappedAmmoInfo(ulong skinID, string name, int unwrapAmount, AmmoType ammo, int stackLimit)
            {
                _skinID = skinID;
                _name = name;
                _unwrapAmount = unwrapAmount;
                _stackLimit = stackLimit;
                _ammo = ammo;
            }
        }

        public class AmmoInfo
        {
            public string _name;
            public ulong _skinID;
            public int _stackLimit;
            public string _shortname;
            public AmmoType _ammoType { get; set; }

            public AmmoInfo(ulong skinID, string name, string shortname, int stackLimit, AmmoType ammoType)
            {
                _skinID = skinID;
                _name = name;
                _shortname = shortname;
                _stackLimit = stackLimit;
                _ammoType = ammoType;
            }

            public Item CreateItem(int amount)
            {
                Item item = ItemManager.CreateByPartialName("battery.small", amount);
                item.skin = _skinID;
                item.name = _name;
                return item;
            }

            public Item CreateFakeAmmo()
            {
                return ItemManager.CreateByPartialName(_shortname, 1);
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
            private AmmoType _loadedAmmoType { get; set; }
            public int Bullets { get { return _bullets; } }

            public Magazine(Item item)
            {
                _mag = item;
                _magazineEntity = item.GetHeldEntity() as BaseProjectile;
                if (_magazineEntity == null)
                {
                    _plugin.Puts($"Magazine Entity is null");
                }
                _definition = _plugin.GetDefinition(item.skin);
                _bullets = _magazineEntity.primaryMagazine.contents;
            }

            public AmmoType GetAmmoType()
            {
                return _definition._allowedAmmo;
            }

            public AmmoType GetLoadedAmmoType()
            {
                return GetAmmoType();
            }

            public MagazineInfo GetDefinition()
            {
                return _definition;
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
                _bullets = Mathf.Max(0, --_bullets);
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
                player.GiveItem(AmmoHelper.CreateAmmo(_definition._allowedAmmo, _bullets)); //TODO: Change this to handle overriden bullet types and use loadedammotype
                _bullets = 0;
                UpdateAmmoCount();
            }

            public void FillAmmo(Item ammo)
            {
                int amount = _definition._amountPerReload;
                if (testing)
                {
                    amount = _definition._capacity;
                }
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
                _plugin.Puts($"UpdateAmmoCount");
                _magazineEntity.primaryMagazine.contents = _bullets;
                _magazineEntity.SendNetworkUpdateImmediate();
                _mag.MarkDirty();
                if (_modItem != null)
                {
                    _plugin.Puts($"Mod item IS NOT null, {_bullets} bullets left");
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
                WeaponInfo def;
                if (!WeaponDefinitions.TryGetValue(weapon.info.shortname, out def))
                {
                    return false;
                }
                return _definition.allowedWeapons.Contains(def.GetWeaponType());
            }

            public bool CanWeaponAccept(WeaponType type)
            {
                return _definition.allowedWeapons.Contains(type);
            }

            public void RemoveFromWeapon(Item weapon)
            {
                BaseProjectile weaponEntity = weapon.GetHeldEntity() as BaseProjectile;
                weaponEntity.primaryMagazine.contents = 0;
                _modItem.RemoveFromContainer();
                var container = weaponEntity.GetOwnerPlayer().inventory.containerMain;
                weaponEntity.GetOwnerPlayer().GiveItem(_mag);
                
                /*if (!_mag.MoveToContainer(container)) //??????? Wasn't working original way, changed to this and it didn't work, changed to original now it works
                {
                    _plugin.Puts($"Failed to move into container {container == null}");
                    _mag.Drop(container.dropPosition, container.dropVelocity);
                }*/
                //weaponEntity.GetOwnerPlayer().GiveItem(_mag);
                weaponEntity.SendNetworkUpdateImmediate();
                _mag.MarkDirty();
                _mag.GetHeldEntity().SendNetworkUpdateImmediate();
            }

            public void MoveToPlayerInventory()
            {

            }

            public void TryDragLoad(Item draggedItem)
            {
                if (AmmoHelper.GetAmmoInfo(draggedItem.skin)._ammoType != _definition._allowedAmmo)
                {
                    return;
                }
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

            public void AddToWeapon(Item weaponItem)
            {
                if (weaponItem == null)
                {
                    return;
                }
                if (_mag?.parent?.parent == weaponItem) //Don't reload magazine if it's already in the gun
                {
                    return;
                }
                var weapon = weaponItem.GetHeldEntity().GetComponent<Weapon>();
                BaseProjectile weaponEntity = weaponItem.GetHeldEntity() as BaseProjectile;
                if (weaponEntity == null)
                {
                    return;
                }
                weaponEntity.primaryMagazine.contents = _bullets;
                SetupModItem(weaponEntity);
            }

            public void SetModMagItem(Item item)
            {
                _modItem = item;
            }

            #region Drag-Reload cooldown

            private void StartCooldown()
            {
                _timer?.Destroy();
                _mag.condition = 2;
                _timer = _plugin.timer.Every(DRAG_RELOAD_UPDATE, CooldownTick);
            }

            private bool HasCooldown()
            {
                return _mag.condition != 1;
            }

            private void StopCooldown()
            {
                _timer?.Destroy();
                _mag.condition = 1;
            }

            private void CooldownTick()
            {
                if (!HasCooldown())
                {
                    return;
                }
                _mag.conditionNormalized += DRAG_RELOAD_UPDATE / _definition._timePerReload;
                //_plugin.Puts($"Update: Mag condition is {_mag.conditionNormalized} ({_mag.condition}/{_mag.maxCondition}) ({DRAG_RELOAD_UPDATE * _definition._timePerReload * _mag.info.condition.max})");
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
            private WeaponType _type { get { return _definition._type; } }
            private WeaponInfo _definition { get; set; }

            private void Awake()
            {
                _entity = GetComponent<BaseProjectile>();
                _definition = WeaponHelper.GetWeaponInfo(_entity);
                var magItem = _entity.GetItem()?.contents?.itemList?.FirstOrDefault(x => MagazineHelper.IsModMagazineItem(x));
                if (magItem == null)
                {
                    return;
                }
                _mag = MagazineHelper.GetMagazine(magItem.contents.itemList[0]);
                _mag.SetModMagItem(_entity.GetItem().contents.itemList[0]);
            }

            public void OnWeaponFired()
            {
                _plugin.Puts($"Weapon OnWeaponFired()");
                _mag.UseAmmo();
            }

            public Magazine GetMagazine()
            {
                return _mag;
            }

            public void AttachMagazine(Magazine mag)
            {
                if (_mag != null)
                {
                    DetachMagazine();
                }
                _mag = mag;
                _mag.AddToWeapon(_entity.GetItem());
            }

            public void DetachMagazine()
            {
                _mag.RemoveFromWeapon(_entity.GetItem());
                _mag = null;
            }

            public WeaponType GetWeaponType()
            {
                return _type;
            }

            public bool HasLoadedMagazine()
            {
                return _mag != null;
            }

            public WeaponInfo GetDefinition()
            {
                return _definition;
            }
        }

        #endregion

        #region Static classes

        public static class WeaponHelper
        {
            public static WeaponInfo GetWeaponInfo(BaseProjectile weapon)
            {
                _plugin.Puts($"GetWeaponInfo() {weapon.GetItem().info.shortname}");
                return WeaponDefinitions[weapon.GetItem().info.shortname];
            }

            public static WeaponInfo GetWeaponInfo(Item item)
            {
                return WeaponDefinitions[item.info.shortname];
            }
        }

        public static class AmmoHelper
        {
            public static Item CreateAmmo(AmmoType ammo, int amount)
            {
                var info = GetAmmoInfo(ammo);
                return info.CreateItem(amount);
            }

            public static Item CreateTempAmmo(AmmoType ammo)
            {
                _plugin.Puts($"CreateTempAmmo type {ammo} {GetAmmoInfo(ammo)._shortname}");
                return ItemManager.CreateByName(GetAmmoInfo(ammo)._shortname, 1);
            }

            public static bool IsWrappedAmmo(Item item)
            {
                return WrappedAmmoDefinitions.ContainsKey(item.skin);
            }

            public static bool IsWrappedAmmo(ulong skinID)
            {
                return WrappedAmmoDefinitions.ContainsKey(skinID);
            }

            public static bool IsAmmo(Item item)
            {
                //Keep as simple check for ammo type, to allow dragging ammo to magazines not move the magazine
                return AmmoSkinIDs.Contains(item.skin);
            }

            public static WrappedAmmoInfo GetWrappedAmmoInfo(Item item)
            {
                return WrappedAmmoDefinitions[item.skin];
            }

            public static AmmoInfo GetAmmoInfo(AmmoType ammo)
            {
                return AmmoDefinitions.Values.FirstOrDefault(x => x._ammoType == ammo);
            }

            public static AmmoInfo GetAmmoInfo(ulong skinID)
            {
                return AmmoDefinitions[skinID];
            }

            public static AmmoInfo GetAmmoInfo(Item item)
            {
                return AmmoDefinitions[item.skin];
            }

        }

        public static class MagazineHelper
        {
            public static MagazineInfo GetMagazineInfo(Item item)
            {
                return MagazineDefinitions[item.skin];
            }

            public static bool IsModMagazineItem(Item item)
            {
                if (item == null)
                {
                    return false;
                }
                return item.info.shortname == SHORTNAME_MODITEM && IsMagazineSkin(item.skin);
            }

            public static bool IsMagazine(Item item)
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

            public static bool IsMagazine(Item item, WeaponType weaponType)
            {
                if (item == null)
                {
                    return false;
                }
                if (item.info.shortname != SHORTNAME_MAGAZINE)
                {
                    return false;
                }
                MagazineInfo info;
                if (!MagazineDefinitions.TryGetValue(item.skin, out info))
                {
                    return false;
                }
                return info.WeaponAccepts(weaponType);
            }

            public static bool IsMagazineSkin(ulong skinID)
            {
                return MagazineDefinitions.ContainsKey(skinID);
            }

            public static bool IsMagazine(HeldEntity entity)
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

            public static Magazine GetMagazine(Item item)
            {
                if (item == null)
                {
                    return null;
                }
                Magazine data;
                if (!magData.TryGetValue(item, out data))
                {
                    data = new Magazine(item);
                    magData.Add(item, data);
                }
                return data;
            }
        }

        public static class PlayerHelper //Not used yet
        {

        }
        #endregion

        public static HashSet<ulong> AmmoSkinIDs;
        public static HashSet<ulong> WrappedAmmoSkinIDs;
        public static HashSet<ulong> MagazineSkinIDs;

        #region Config values

        public enum WeaponType
        {
            None = 0,
            Ak47 = 1,
            Semi_Rifle = 2,
            LR300 = 3,
            M249 = 4,
            PumpShotgun = 5,
            Thompson = 6,
            CustomSMG = 7,
            MP5 = 8,
            BoltAction = 9,
            Semi_Pistol = 10,
            Waterpipe = 11,
            Revolver = 12,
            Python = 13,
            M92Pistol = 14,
            FlameThrower = 15,
            DoubleBarrel = 16,
            Crossbow = 17,
            Bow = 18,
            RocketLauncher = 19,
        }

        public enum AmmoType
        {
            None = 0,
            Ammo_Rifle = 1,
            Ammo_Pistol = 2,
            Ammo_Slug = 3,
            Ammo_Buckshot = 4,
            Ammo_HandmadeShell = 5,
            Ammo_Arrow = 6,
            Ammo_LowGrade = 7,
            Ammo_Rockets = 8,
        }

        private static Dictionary<ulong, MagazineInfo> MagazineDefinitions = new Dictionary<ulong, MagazineInfo>()
        {
            {946780213, new MagazineInfo(946780213, "Creative Magazine", 100000, AmmoType.Ammo_Rifle, 100000, 0.1f, WeaponType.Ak47, WeaponType.M249, WeaponType.LR300) },

            {939979493u, new MagazineInfo(939979493u, "STANAG Magazine", 30, AmmoType.Ammo_Rifle, 3, 0.6f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {940561694u, new MagazineInfo(940561694u, "75Rnd DrumMag", 75, AmmoType.Ammo_Rifle, 3, 1.0f, WeaponType.Ak47) },
            {941865444u, new MagazineInfo(941865444u, "10Rnd CMAG", 10, AmmoType.Ammo_Rifle, 2, 0.4f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941868420u, new MagazineInfo(941868420u, "20Rnd CMAG", 20, AmmoType.Ammo_Rifle, 2, 0.6f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941869375u, new MagazineInfo(941869375u, "30Rnd CMAG", 30, AmmoType.Ammo_Rifle, 2, 0.8f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941871543u, new MagazineInfo(941871543u, "40Rnd CMAG", 40, AmmoType.Ammo_Rifle, 2, 1.0f, WeaponType.Ak47, WeaponType.LR300, WeaponType.M249, WeaponType.Semi_Rifle) },
            {941873168u, new MagazineInfo(941873168u, "80Rnd CMAG", 80, AmmoType.Ammo_Rifle, 3,2f, WeaponType.M249, WeaponType.LR300) },
            {946762518, new MagazineInfo(946762518, "150Rnd M249 Magazine", 150, AmmoType.Ammo_Rifle, 5, 1.0f, WeaponType.M249) },
            {946778674, new MagazineInfo(946778674, "30Rnd AK Magazine", 30, AmmoType.Ammo_Rifle, 3, 2.0f, WeaponType.Ak47) },
            {946781897, new MagazineInfo(946781897, "M92 Magazine", 16, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.M92Pistol) }, //Glock19 Magazine
            {946783613, new MagazineInfo(946783613, "Semi-Pistol Magazine", 10, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.Semi_Pistol) }, //P1 Pistol Magazine
            {946784019, new MagazineInfo(946784019, "Python Speedloader", 6, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.Python) },

            {946779217, new MagazineInfo(946779217, "5Rnd Bolt Magazine", 5, AmmoType.Ammo_Rifle, 1, 1.0f, WeaponType.BoltAction) },
            
            {946781016, new MagazineInfo(946781016, "Double Barrel Quickloader", 2, AmmoType.Ammo_Buckshot, 3, 2.0f, WeaponType.DoubleBarrel) },
            {941402580u, new MagazineInfo(941402580u, "5Rnd Shotgun", 5, AmmoType.Ammo_Buckshot, 1, 1.0f, WeaponType.PumpShotgun)},
            {941400934u, new MagazineInfo(941400934u, "8Rnd Shotgun", 8, AmmoType.Ammo_Buckshot, 1, 1.2f, WeaponType.PumpShotgun)},
            {941405262u, new MagazineInfo(941405262u, "20Rnd Shotgun", 20, AmmoType.Ammo_Buckshot, 1, 2f, WeaponType.PumpShotgun)},

            {946782998, new MagazineInfo(946782998, "25Rnd MP5 Magazine", 25, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.MP5) },
            {946783337, new MagazineInfo(946783337, "45Rnd MP5 Magazine", 45, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.MP5) },

            //Unused
            //{946784370, new MagazineInfo(946784370, "25Rnd UMP Magazine", 25, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.MP5) },
            //{946777860, new MagazineInfo(946777860, ".22 Pistol Magazine", 10, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.Semi_Pistol) },
            //{946779679, new MagazineInfo(946779679, "CR75 Magazine", 10, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.Semi_Pistol) },
            //{946782318, new MagazineInfo(946782318, "M1911 Magazine", 10, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.Semi_Pistol) },
            //{946781518, new MagazineInfo(946781518, "FNX75 Magazine", 10, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.Semi_Pistol) },
            //{946782704, new MagazineInfo(946782704, "Makarov Magazine", 8, AmmoType.Ammo_Pistol, 3, 2.0f, WeaponType.Semi_Pistol) }

        };

        private static Dictionary<ulong, WrappedAmmoInfo> WrappedAmmoDefinitions = new Dictionary<ulong, WrappedAmmoInfo>()
        {
            {942695934u, new WrappedAmmoInfo(942695934u, "Wrapped 12 Gauge Ammo", 12, AmmoType.Ammo_Buckshot, 10) },
            {943400587u, new WrappedAmmoInfo(943400587u, "Wrapped 9mm Ammo", 30, AmmoType.Ammo_Pistol, 10) },
            {943401516u, new WrappedAmmoInfo(943401516u, "Wrapped 5.56 Ammo", 30, AmmoType.Ammo_Rifle, 10) },
        };

        private static Dictionary<ulong, AmmoInfo> AmmoDefinitions = new Dictionary<ulong, AmmoInfo>()
        {
            {942706838u, new AmmoInfo(942706838u, "Buckshot", "ammo.shotgun", 32, AmmoType.Ammo_Buckshot) },
            {942708393u, new AmmoInfo(942708393u, "5.56 Rifle Ammo", "ammo.rifle", 120, AmmoType.Ammo_Rifle) },
            {943402758u, new AmmoInfo(943402758u, "9mm Pistol Ammo", "ammo.pistol", 100, AmmoType.Ammo_Pistol) },
        };

        private static Dictionary<string, WeaponInfo> WeaponDefinitions = new Dictionary<string, WeaponInfo>()
        {
            { "rifle.ak", new WeaponInfo(WeaponType.Ak47, 3, AmmoType.Ammo_Rifle) },
            { "rifle.lr300", new WeaponInfo(WeaponType.LR300,  3, AmmoType.Ammo_Rifle) },
            { "rifle.bolt", new WeaponInfo(WeaponType.BoltAction, 3, AmmoType.Ammo_Rifle) },
            { "rifle.semimauto", new WeaponInfo(WeaponType.Semi_Rifle, 3, AmmoType.Ammo_Rifle) },
            { "msg.m249", new WeaponInfo(WeaponType.M249, 3, AmmoType.Ammo_Rifle) },

            { "shotgun.pump", new WeaponInfo(WeaponType.PumpShotgun, 2, AmmoType.Ammo_Buckshot) },
            { "shotgun.double", new WeaponInfo(WeaponType.DoubleBarrel, 2, AmmoType.Ammo_Buckshot) },

            { "crossbow", new WeaponInfo(WeaponType.Crossbow, 2, AmmoType.Ammo_Arrow) },
            { "flamethrower", new WeaponInfo(WeaponType.FlameThrower, 0, AmmoType.Ammo_LowGrade) },

            { "smg.thompson", new WeaponInfo(WeaponType.Thompson, 2, AmmoType.Ammo_Pistol) },
            { "smg.2", new WeaponInfo(WeaponType.CustomSMG, 3, AmmoType.Ammo_Pistol) },
            { "smg.mp5", new WeaponInfo(WeaponType.MP5, 3, AmmoType.Ammo_Pistol) },

            { "pistol.revolver", new WeaponInfo(WeaponType.Revolver, 1, AmmoType.Ammo_Pistol) },
            { "pistol.python", new WeaponInfo(WeaponType.Python, 3, AmmoType.Ammo_Pistol) },
            { "pistol.semiauto", new WeaponInfo(WeaponType.Semi_Pistol, 3, AmmoType.Ammo_Pistol) },
            { "pistol.m92", new WeaponInfo(WeaponType.M92Pistol, 3, AmmoType.Ammo_Pistol) },

            //Weapons that don't need magazines
            { "shotgun.waterpipe", new WeaponInfo(WeaponType.Waterpipe, 2, AmmoType.Ammo_Buckshot) },
            { "bow.hunting", new WeaponInfo(WeaponType.Bow, 0, AmmoType.Ammo_Arrow) },
            { "rocket.launcher", new WeaponInfo(WeaponType.RocketLauncher, 0, AmmoType.Ammo_Rockets) }
        };

        #endregion

        #region Dev helper functions

        public void GiveMagazine(BasePlayer player, MagazineInfo type)
        {
            //Item item = ItemManager.CreateByPartialName("battery");
            Item item = ItemManager.CreateByPartialName(SHORTNAME_MAGAZINE);
            item.skin = type._skinID;
            item.name = type._name;
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

        public void GiveWrappedAmmo(BasePlayer player, WrappedAmmoInfo type, int amount)
        {
            //Item item = ItemManager.CreateByPartialName("battery");
            Item item = ItemManager.CreateByPartialName("present.large");
            item.skin = type._skinID;
            item.name = type._name;
            item.amount = amount;
            player.inventory.GiveItem(item);
        }

        [ChatCommand("mag")]
        void TestMagazines(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            string search = "";
            int amount = 1;
            if (args.Length > 0)
            {
                search = args[0];
                if (args.Length > 1)
                {
                   int.TryParse(args[1], out amount);
                }
            }
            if (search != null && search != "")
            {
                MagazineInfo info = MagazineDefinitions.Values.FirstOrDefault(x => x._name.ToLower().Contains(search.ToLower()));
                if (info != null)
                {
                    for (int i = 0; i < amount; i++)
                    {
                        GiveMagazine(player, info);
                    }
                    return;
                }
            }
            foreach (var mag in MagazineDefinitions)
            {
                GiveMagazine(player, mag.Value);
            }
        }

        [ChatCommand("ammobox")]
        void TestAmmoBox(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            foreach (var ammo in WrappedAmmoDefinitions.Values)
            {
                GiveWrappedAmmo(player, ammo, 10);
            }
        }

        [ChatCommand("ammo")]
        void TestAmmos(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            foreach (var ammo in AmmoDefinitions.Values)
            {
                player.GiveItem(ammo.CreateItem(ammo._stackLimit * 10));   
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
