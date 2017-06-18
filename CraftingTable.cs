using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CraftingTable", "Jake_Rich", "1.1.0", ResourceId = 2498)]
    [Description("Centralized controller for modded custom crafting recipes")]

    public partial class CraftingTable : RustPlugin
    {
        public static CraftingTable _plugin { get; set; }
        public static CraftMenuController CraftController { get; set; }
        public static UIBaseElement CraftTableIcon { get; set; }

        #region Hooks

        void Init()
        {
            _plugin = this;
        }

        void OnServerInitialized()
        {
            SetupUI();
            CraftController = new CraftMenuController();

            foreach (var entity in GameObject.FindObjectsOfType<BaseEntity>())
            {
                OnEntitySpawned(entity);
            }
        }

        void Unload()
        {
            CraftTableIcon.HideAll();
            CraftController.OnUnload();

            foreach (var entity in GameObject.FindObjectsOfType<BaseEntity>())
            {
                OnEntityKilled(entity);
            }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!GetPlayerData(player).LookingAtTable)
            {
                return;
            }
            if (input.WasJustPressed(BUTTON.USE))
            {
                CraftController.ShowPlayer(player);
            }
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity.PrefabName == "assets/prefabs/deployable/table/table.deployed.prefab")
            {
                entity.gameObject.AddComponent<CraftTableTrigger>();
            }
        }

        void OnEntityKilled(BaseEntity entity)
        {
            if (entity.PrefabName == "assets/prefabs/deployable/table/table.deployed.prefab")
            {
                GameObject.Destroy(entity.GetComponent<CraftTableTrigger>());
            }
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            Puts("OnItemCraftFinished()");
        }

        #endregion

        #region UI Settings

        public void SetupUI()
        {
            CraftTableIcon = new UIBaseElement(new Vector2(0.40f, 0.40f), new Vector2(0.60f, 0.60f));

            float dotSize = 0.007f;
            float fadeIn = 0.25f;

            UIImage crosshairDot = new UIImage(new Vector2(0.5f - dotSize, 0.5f - dotSize * 2), new Vector2(0.5f + dotSize, 0.5f + dotSize * 2), CraftTableIcon);
            crosshairDot.Image.Sprite = "assets/icons/circle_closed.png";

            UIImage openIconSprite = new UIImage(new Vector2(0.46f, 0.73f), new Vector2(0.54f, 0.90f), CraftTableIcon);
            openIconSprite.Image.Sprite = "assets/icons/open.png";
            openIconSprite.Image.FadeIn = fadeIn;

            UILabel openIconText = new UILabel(new Vector2(0, 0.5f), new Vector2(1f, 0.8f), lang.GetMessage("Open", this), 14, parent: CraftTableIcon);
            openIconText.text.FadeIn = fadeIn;
        }

        #endregion

        #region LangAPI

        public Dictionary<string, string> LangAPI = new Dictionary<string, string>()
        {
            { "Open", "Open" },
        };

        #endregion

        #region PlayerData

        public class PlayerData
        {
            private BasePlayer _player { get; set; }
            public bool LookingAtTable { get; set; }
            public bool UsingCraftMenu { get; set; }
            public int SelectedModIndex { get; set; }
            public int SelectedPage { get; set; }
            public int SelectedItemIndex { get; set; }
            public int CraftAmount { get; set; }

            public PlayerData(BasePlayer player)
            {
                _player = player;
            }

            public PlayerData()
            {

            }


        }

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

        public static Dictionary<BasePlayer, PlayerData> playerData { get; set; } = new Dictionary<BasePlayer, PlayerData>();

        #endregion

        #region Recipe Classes

        public class CraftRecipe
        {
            public List<ItemAmount> RequiredIngredients { get; set; } = new List<ItemAmount>();
            public string ItemName { get; set; }
            public string Description { get; set; }
            public ItemAmount ResultItem { get; set; }
            public float CraftTime { get; set; }

            public CraftRecipe()
            {

            }

            public CraftRecipe(string itemName, float craftTime, string description, string productShortname, int productAmount, ulong productSkin, params ItemAmount[] ingredients)
            {
                ItemName = itemName;
                CraftTime = craftTime;
                Description = description;
                ResultItem = new ItemAmount(productShortname, productAmount, productSkin);
                RequiredIngredients.AddRange(ingredients);
            }

            public bool HasItems(BasePlayer player, int amount)
            {
                if (amount > 10000 || amount < 1)
                {
                    _plugin.PrintError($"HasItems() out of range! {amount}");
                    return false;
                }
                Item[] items = player.inventory.AllItems();

                foreach (var item in RequiredIngredients)
                {
                    if (items.Where(x => x.skin == item.skinID && x.info.shortname == item.shortname).Sum(x => x.amount) < item.amount * amount)
                    {
                        return false;
                    }
                }
                return true;
            }

            public void AddIngredient(string shortname, int amount = 1, ulong skin = 0)
            {
                RequiredIngredients.Add(new ItemAmount(shortname, amount, skin));
            }
        }

        public class ItemAmount
        {
            public string shortname { get; set; }
            public int amount { get; set; }
            public ulong skinID { get; set; }

            public ItemAmount()
            {

            }

            public ItemAmount(string shortname, int amount = 1, ulong skinID = 0)
            {
                this.shortname = shortname;
                this.amount = amount;
                this.skinID = skinID;
            }

            public int ItemID()
            {
                var item = ItemManager.itemList.FirstOrDefault(x => x.shortname == shortname);
                if (item == null)
                {
                    return -1;
                }
                return item.itemid;
            }
        }

        #endregion

        #region Classes

        public class CraftTableTrigger : LookingAtTrigger
        {
            private BaseEntity _this { get; set; }

            void Awake()
            {
                Init();
            }

            public override void Init()
            {
                base.Init();
                _this = GetComponent<BaseEntity>();
            }

            public override void OnPlayerLookAtEntity(BasePlayer player, BaseEntity entity)
            {
                base.OnPlayerLookAtEntity(player, entity);
                if (entity == _this)
                {
                    CraftController.ShowIcon(player);
                }
            }

            public override void OnPlayerStopLookingAtEntity(BasePlayer player, BaseEntity entity)
            {
                base.OnPlayerStopLookingAtEntity(player, entity);
                if (entity == _this)
                {
                    CraftController.HideIcon(player);
                }
            }

            public override void OnPlayerEnter(BasePlayer player)
            {
                base.OnPlayerEnter(player);
            }

            public override void OnPlayerExit(BasePlayer player)
            {
                base.OnPlayerExit(player);
                CraftController.HideIcon(player);
                CraftController.HidePlayer(player);
            }
        }

        #endregion

        #region ImageLibary

        [PluginReference]
        RustPlugin ImageLibrary;

        public static string GetItemURL(string shortname, ulong skinID)
        {
            if (_plugin.ImageLibrary == null)
            {
                _plugin.PrintError("ImageLibrary not found!");
                return "";
            }
            string url = _plugin.ImageLibrary.Call("GetImage", shortname, skinID, true) as string;
            _plugin.Puts($"GetImage() returned {url}");
            return url;
        }

        public static void GetSteamWorkshopURL(string shortname, ulong skinID)
        {
            string targetURL = $"http://steamcommunity.com/sharedfiles/filedetails/?id={skinID}";
            _plugin.webrequest.EnqueueGet(targetURL, (code, response) =>
            {
                if (code != 200)
                {
                    return;
                }
                //_plugin.Puts(response);
                int startIndex = response.IndexOf("ShowEnlargedImagePreview(");
                int index = response.IndexOf('\'', startIndex);
                int endIndex = response.IndexOf('\'', index + 1);
                string url = response.Substring(index + 1, endIndex - index - 1);
                //_plugin.Puts($"URL: {url} Start: {startIndex} {index} {endIndex}");
                AddImage(url, "pistol.eoka", skinID);
            }, _plugin, null, 30f);
        }

        public static void AddImage(string url, string shortname, ulong skinID)
        {
            _plugin.ImageLibrary.Call("AddImage", url, shortname, skinID);
        }

        #endregion

        public class ModSettings
        {
            public string Identifier { get; set; }
            public string PluginName { get; set; }
            public List<CraftRecipe> Recipes { get; set; } = new List<CraftRecipe>();
            public int Index { get; set; }
        }

        ItemManager manager;
        List<ItemBlueprint> itemList;

        public class CraftMenuController
        {
            private UIPanel MainPanel { get; set; }
            private UIPanel PluginNamePanel { get; set; }
            private UILabel PluginNameLabel { get; set; }
            private UIPanel ItemGrid { get; set; }
            private UIPanel ItemDescription { get; set; }
            private UILabel ItemAmountLabel { get; set; }
            private UIButton ExitButton { get; set; }
            private List<UIBaseElement> RequiredIngredients { get; set; } = new List<UIBaseElement>();
            private Timer _timer { get; set; }
            //private UIPanel CraftTimePanel { get; set; }
            //private UIPanel MainPage { get; set; }

            private List<ModSettings> Mods { get { return _mods; } }

            private List<ModSettings> _mods = new List<ModSettings>();
            private int _itemsPerPage = 0;

            public CraftMenuController()
            {
                _plugin.itemList = ItemManager.bpList;

                Mods.Add(new ModSettings()
                {
                    PluginName = "TestPlugin",
                    Recipes = new List<CraftRecipe>()
                    {
                        new CraftRecipe()
                        {
                            CraftTime = 1f,
                            ItemName = "Stones",
                            Description = "Totally not a test description! Crafting one wood into 5 stones is 100% a balanced recipe ready for rust! Perhaps if we make this description really long and rambly, we will be able to see how word wrap works, and how long descriptions look! (Hint: They look pretty damn decent)",
                            ResultItem= new ItemAmount()
                            {
                                amount = 5,
                                shortname = "stones",
                                skinID = 0,
                            },
                            RequiredIngredients = new List<ItemAmount>()
                            {
                                new ItemAmount()
                                {
                                    amount = 1,
                                    skinID = 0,
                                    shortname = "wood",
                                },
                            },
                        },
                        new CraftRecipe()
                        {
                            CraftTime = 10f,
                            ItemName = "AK Magazine",
                            Description = "Ak47 Magazine. Holds 30 bullets",
                            ResultItem= new ItemAmount()
                            {
                                amount = 1,
                                shortname = "pistol.eoka",
                                skinID = 946778674,
                            },
                            RequiredIngredients = new List<ItemAmount>()
                            {
                                new ItemAmount()
                                {
                                    amount = 100,
                                    skinID = 0,
                                    shortname = "wood",
                                },
                                new ItemAmount()
                                {
                                    amount = 1000,
                                    skinID = 0,
                                    shortname = "metal.fragments",
                                }
                            },
                        },
                        new CraftRecipe()
                        {
                             CraftTime = 30f, 
                             ItemName = "5Rnd Bolt Clip",
                             ResultItem = new ItemAmount("pistol.eoka", 1, 946779217),
                             Description = "A 5 Round Clip for the bolt action rifle.",
                             RequiredIngredients = new List<ItemAmount>()
                             {
                                 new ItemAmount("wood", 100),
                                 new ItemAmount("metal.fragments", 500),
                             },
                        }
                    },
                });

                SetupUI();

                foreach (var plugin in Interface.Oxide.RootPluginManager.GetPlugins())
                {
                    OnPluginAdded(plugin);
                }

                Interface.Oxide.RootPluginManager.OnPluginAdded += OnPluginAdded;
                Interface.Oxide.RootPluginManager.OnPluginRemoved += OnPluginRemoved;

                foreach (var mod in Mods)
                {
                    SetupImages(mod);
                }

                _timer = _plugin.timer.Every(0.1f, TimerLoop);
            }

            private void SetupImages(ModSettings mod)
            {
                foreach (var recipe in mod.Recipes)
                {
                    if (GetItemURL(recipe.ResultItem.shortname, recipe.ResultItem.skinID) != "39274839")
                    {
                        continue;
                    }
                    GetSteamWorkshopURL(recipe.ResultItem.shortname, recipe.ResultItem.skinID);
                }
            }

            private void OnPluginAddedOrRemoved()
            {
                //TODO: Fix any changes based on a mod's setings being added or removed
                MainPanel.HideAll();
                ExitButton.HideAll(); //TODO: Combine all these elements under one central base element
                PluginNamePanel.HideAll(); //TODO: Update craft grid without hiding for all players
                _mods = _mods.OrderBy(x=>x.PluginName).ToList();
                foreach (var mod in _mods)
                {
                    mod.Recipes = mod.Recipes.OrderBy(x => x.ItemName).ToList();
                }
                _plugin.Puts($"{Mods.Count}");
            }

            private void OnPluginAdded(Core.Plugins.Plugin plugin)
            {
                var response = plugin.Call("GetCraftingRecipes") as string;
                if (response == null)
                {
                    return;
                }
                ModSettings pluginSettings;
                try
                {
                    pluginSettings = JsonConvert.DeserializeObject<ModSettings>(response);
                }
                catch
                {
                    return;
                }
                if (pluginSettings == null)
                {
                    //_plugin.Puts($"OnPluginAdded({plugin.Name})");
                    return;
                }
                _plugin.Puts($"Loaded Mod for {plugin.Name}");
                if (pluginSettings.PluginName == "" || pluginSettings.PluginName == null)
                {
                    pluginSettings.PluginName = plugin.Name;
                }
                pluginSettings.Identifier = $"{plugin.Name}{plugin.Author}";
                Mods.Add(pluginSettings);
                SetupImages(pluginSettings);
                _plugin.Puts(JsonConvert.SerializeObject(pluginSettings));
                OnPluginAddedOrRemoved();
            }

            private void OnPluginRemoved(Core.Plugins.Plugin plugin)
            {
                Mods.RemoveAll(x => x.Identifier == $"{plugin.Name}{plugin.Author}");
                OnPluginAddedOrRemoved();
            }

            public void OnUnload()
            {
                _timer?.Destroy();
                Interface.Oxide.RootPluginManager.OnPluginAdded -= OnPluginAdded;
                Interface.Oxide.RootPluginManager.OnPluginRemoved -= OnPluginRemoved;
                MainPanel.HideAll();
                PluginNamePanel.HideAll();
                ExitButton.HideAll();
                //CraftTimePanel.HideAll();
            }

            //Public stuff here
            public void ShowPlayer(BasePlayer player)
            {
                _plugin.ImageLibrary.Call("AddImage", "https://steamuserimages-a.akamaihd.net/ugc/868480752631015567/4E31DE0C85B1EA124A757B52AA63939D70721310/", "pistol.eoka", 946778674);
                var data = GetPlayerData(player);
                data.UsingCraftMenu = true;
                data.SelectedModIndex = -1;
                data.SelectedPage = -1;
                data.SelectedItemIndex = -1;
                CraftTableIcon.Hide(player);

                MainPanel.Show(player);
                PluginNamePanel.Show(player);
                ExitButton.Show(player);
            }

            public void HidePlayer(BasePlayer player)
            {
                var data = GetPlayerData(player);
                data.UsingCraftMenu = false;
                data.SelectedModIndex = -1;
                data.SelectedPage = -1;
                data.SelectedItemIndex = -1;
                MainPanel.Hide(player);
                PluginNamePanel.Hide(player);
                ExitButton.Hide(player);
            }

            public void ShowIcon(BasePlayer player)
            {
                var data = GetPlayerData(player);
                if (data.UsingCraftMenu)
                {
                    return;
                }
                data.LookingAtTable = true;
                CraftTableIcon.Show(player);
            }

            public void HideIcon(BasePlayer player)
            {
                var data = GetPlayerData(player);
                data.LookingAtTable = false;
                CraftTableIcon.Hide(player);
            }

            private void SetupUI()
            {

                MainPanel = new UIPanel(new Vector2(0.15f, 0.175f), new Vector2(0.95f, 0.90f), null, "0 0 0 0.70");
                MainPanel.CursorEnabled = true;

                PluginNamePanel = new UIPanel(new Vector2(0.4f, 0.92f), new Vector2(0.6f, 0.98f));
                
                PluginNamePanel.conditionalShow = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    return data.SelectedModIndex != -1;
                };

                PluginNameLabel = new UILabel(new Vector2(0, 0), new Vector2(1, 1), "Plugin Name Here", 16, parent: PluginNamePanel);

                PluginNameLabel.variableText = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    if (data.SelectedModIndex == -1)
                    {
                        return "";
                    }
                    if (data.SelectedModIndex < 0 || data.SelectedModIndex > Mods.Count)
                    {
                        return "";
                    }
                    return Mods[data.SelectedModIndex].PluginName;
                };

                Vector2 craftTimePos = new Vector2(0.85f, 0.1525f);

                ExitButton = new UIButton(new Vector2(0.93f, 0.93f), new Vector2(0.96f, 0.96f), "X", "1 0 0 1", "1 1 1 1", 20);
                ExitButton.AddCallback((player) =>
                {
                    HidePlayer(player);
                });

                //CraftTimePanel = new UIPanel(craftTimePos, 0.13f, 0.039f, null, "1 1 0 1");

                /*CraftTimePanel.conditionalPosition = delegate (BasePlayer player)
                {
                    int statuses = player.inventory.crafting.queue.Count > 0 ? 1 : 0;

                    if (player.metabolism.comfort.value > 0)
                    {
                        statuses++;
                    }

                    if (player.metabolism.bleeding.value > 0)
                    {
                        statuses++;
                    }

                    if (player.metabolism.calories.value < 40f)
                    {
                        statuses++;
                    }

                    if (player.metabolism.hydration.value < 40f)
                    {
                        statuses++;
                    }

                    if (player.metabolism.temperature.value < 7f)
                    {
                        statuses++;
                    }

                    return craftTimePos + new Vector2(0, 0.046f * statuses);
                };

                CraftTimePanel.Show(BasePlayer.activePlayerList);    */   

                SetupUI_ModList();
                SetupUI_CraftGrid();
                SetupUI_ItemDescription();
            }

            private void SetupUI_ModList()
            {
                float height = 0.08f;
                for (int i = 0; i< Mathf.FloorToInt((1f / height)); i++)
                {
                    int index = i;
                    UIButton modPanel = new UIButton(new Vector2(0f, 1f - (((index + 1) * height) - 0.01f)), new Vector2(0.125f, 1f - (index * height)), "",  "1 0 0 0.7", parent: MainPanel);
                    modPanel.AddCallback((player)=>
                    {
                        _plugin.Puts($"Clicked mod {index} / {Mods.Count}!");
                        SetModPage(player, index);
                    });

                    modPanel.conditionalShow = delegate (BasePlayer player)
                    {
                        return index < Mods.Count;
                    };

                    UILabel modName = new UILabel(new Vector2(0.03f, 0f), new Vector2(1f, 1f), "", parent: modPanel, alignment: TextAnchor.MiddleLeft);
                    modName.variableText = delegate (BasePlayer player)
                    {
                        if (index > Mods.Count - 1 || index < 0)
                        {
                            return "";
                        }
                        return Mods[index].PluginName;
                    };

                    UILabel recipeCount = new UILabel(new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), "", parent: modPanel, alignment: TextAnchor.MiddleRight);
                    recipeCount.variableText = delegate (BasePlayer player)
                    {
                        if (index > Mods.Count - 1 || index < 0)
                        {
                            return "";
                        }
                        return Mods[index].Recipes.Count.ToString();
                    };
                }
            }

            private void SetupUI_CraftGrid()
            {
                ItemGrid = new UIPanel(new Vector2(0.14f, 0.01f), new Vector2(0.6f, 1f), MainPanel,  "0 1 0 0");

                float width = 6;
                float height = 6;
                _itemsPerPage = (int)(width * height);
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index = (int)(y * height + x); //Need to delcare a new variable for each callback
                        Vector2 size = new Vector2(1f / width, 1f / height);
                        Vector2 pos = new Vector2(x * size.x, 1f -(y + 1) * size.y);
                        UIRawImage itemIcon = new UIRawImage(pos + new Vector2(0.005f, 0f), pos + size * 0.95f, ItemGrid);
                        UIButton itemButton = new UIButton(pos + new Vector2(0.005f, 0f),pos + size * 0.95f, $"", "0 1 1 0.00001", fontSize: 12, parent: ItemGrid);
                        //UIRawImage itemIcon = new UIRawImage(new Vector2(0f, 0f), new Vector2(1f, 1f), itemButton);
                        
                        itemIcon.variablePNGURL = delegate (BasePlayer player)
                        {
                            var data = GetPlayerData(player);
                            if (data.SelectedModIndex == -1)
                            {
                                return null;
                            }
                            var mod = GetModPage(data);
                            if (mod == null)
                            {
                                return "";
                            }
                            if (index < 0 || index > mod.Recipes.Count - 1)
                            {
                                return "";
                            }
                            var recipe = mod.Recipes[index];
                            return GetItemURL(recipe.ResultItem.shortname, recipe.ResultItem.skinID);
                        };
                        itemButton.AddCallback((player) =>
                        {
                            _plugin.Puts($"Clicked mod {index}!");
                            SetCraftItemIndex(player, index);
                        });
                        itemButton.conditionalShow = delegate (BasePlayer player)
                        {
                            var data = GetPlayerData(player);
                            var mod = GetModPage(data);
                            if (mod == null)
                            {
                                return false;
                            }
                            if (mod.Recipes.Count <= index)
                            {
                                return false;
                            }
                            return true;
                        };
                        /*itemButton.variableText = delegate (BasePlayer player)
                        {
                            var data = GetPlayerData(player);
                            var mod = GetModPage(data);
                            if (mod == null)
                            {
                                return "";
                            }
                            if (index < 0 || index > mod.CustomRecipes.Count - 1)
                            {
                                return "";
                            }
                            var recipe = mod.CustomRecipes[index];
                            return recipe.ResultItem.shortname;
                        };*/
                    }
                }
            }

            private void SetupUI_ItemDescription()
            {
                ItemDescription = new UIPanel(new Vector2(0.605f, 0f), new Vector2(1f, 1f), MainPanel, "0 1 0 0.50");
                ItemDescription.conditionalShow = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    return data.SelectedItemIndex != -1;
                };

                #region Top Right Values

                UIPanel craftTimePanel = new UIPanel(new Vector2(0.8f, 0.93f), 0.18f, 0.05f, ItemDescription);

                UILabel craftTimeLabel = new UILabel(new Vector2(0, 0), new Vector2(1, 1), "", parent: craftTimePanel);
                craftTimeLabel.variableText = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    var recipe = GetRecipe(data);
                    if (recipe == null)
                    {
                        return "";
                    }
                    return $"{Math.Round(recipe.CraftTime,1)} second{(recipe.CraftTime > 1f ? "s" : "")}";
                };

                UIPanel resultAmountPanel = new UIPanel(new Vector2(0.8f, 0.875f), 0.18f, 0.05f, ItemDescription);
                UILabel resultAmountText = new UILabel(new Vector2(0, 0), new Vector2(1, 1), "", parent: resultAmountPanel);

                resultAmountText.variableText = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    var recipe = GetRecipe(data);
                    if (recipe == null)
                    {
                        return "";
                    }
                    return $"x{recipe.ResultItem.amount}";
                };

                #endregion

                #region Item Icon
                UIRawImage itemIcon = new UIRawImage(new Vector2(0.05f, 0.91f), 0.150f, 0.150f, ItemDescription);
                itemIcon.variablePNGURL = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    var recipe = GetRecipe(data);
                    if (recipe == null)
                    {
                        return "";
                    }
                    return GetItemURL(recipe.ResultItem.shortname, recipe.ResultItem.skinID);
                };


                #endregion

                #region Name and Description

                UILabel itemName = new UILabel(new Vector2(0.2f, resultAmountPanel.min.y), new Vector2(0.80f, craftTimePanel.max.y), "Item Name Here", 20, "0.9 0.9 0.9  s 1", ItemDescription);
                itemName.variableText = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    var recipe = GetRecipe(data);
                    if (recipe == null)
                    {
                        return "";
                    }
                    return recipe.ItemName;
                };

                UILabel itemDescription = new UILabel(new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.85f), "", 13, "1 1 1 1", ItemDescription, TextAnchor.UpperLeft);
                itemDescription.variableText = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    var recipe = GetRecipe(data);
                    if (recipe == null)
                    {
                        return "";
                    }
                    return recipe.Description;
                };

                #endregion

                #region Required Materials

                int panelCount = 6;

                float MaxY = 0.35f;
                float MinY = 0.08f;

                float height = (MaxY - MinY) / panelCount;

                UILabel requiredItemsLabel1 = new UILabel(new Vector2(0f, MaxY + 0.015f), new Vector2(0.2f, MaxY + 0.08f), "Amount", parent: ItemDescription, alignment: TextAnchor.LowerCenter);
                UILabel requiredItemsLabel2 = new UILabel(new Vector2(0.2f, MaxY + 0.015f), new Vector2(0.6f, MaxY + 0.08f), "Item Name", parent: ItemDescription, alignment: TextAnchor.LowerCenter);
                UILabel requiredItemsLabel3 = new UILabel(new Vector2(0.6f, MaxY + 0.015f), new Vector2(0.8f, MaxY + 0.08f), "Total Amount", parent: ItemDescription, alignment: TextAnchor.LowerCenter);
                UILabel requiredItemsLabel4 = new UILabel(new Vector2(0.8f, MaxY + 0.015f), new Vector2(1.0f, MaxY + 0.08f), "Have", parent: ItemDescription, alignment: TextAnchor.LowerCenter);

                for (int i = 0; i < panelCount; i++)
                {
                    int index = i;
                    UIPanel panel = new UIPanel(new Vector2(0.01f, MaxY - height * (i + 1) + 0.005f), new Vector2(0.97f, MaxY - height * i), ItemDescription);
                    panel.conditionalShow = delegate (BasePlayer player)
                    {
                        var data = GetPlayerData(player);
                        var recipe = GetRecipe(data);
                        if (recipe == null)
                        {
                            return false;
                        }
                        return index < recipe.RequiredIngredients.Count;
                    };

                    UILabel requiredAmountPerItem = new UILabel(new Vector2(0f, 0f), new Vector2(0.2f, 1f), "", 12, "1 1 1 1", panel, TextAnchor.MiddleCenter);
                    requiredAmountPerItem.variableText = delegate (BasePlayer player)
                    {
                        var data = GetPlayerData(player);
                        var recipe = GetRecipe(data);
                        if (recipe == null)
                        {
                            return "";
                        }
                        if (index > recipe.RequiredIngredients.Count - 1)
                        {
                            return "";
                        }
                        return recipe.RequiredIngredients[index].amount.ToString();
                    };

                    UILabel requiredItemName = new UILabel(new Vector2(0.2f, 0f), new Vector2(0.6f, 1f), "", 12, "1 1 1 1", panel, TextAnchor.MiddleCenter);
                    requiredItemName.variableText = delegate (BasePlayer player)
                    {
                        var data = GetPlayerData(player);
                        var recipe = GetRecipe(data);
                        if (recipe == null)
                        {
                            return "";
                        }
                        if (index > recipe.RequiredIngredients.Count - 1)
                        {
                            return "";
                        }
                        var shortname = recipe.RequiredIngredients[index].shortname;
                        return ItemManager.itemList.First(x => x.shortname == shortname).displayName.english;
                    };

                    UILabel requiredTotalAmount = new UILabel(new Vector2(0.6f, 0f), new Vector2(0.8f, 1f), "", 12, "1 1 1 1", panel, TextAnchor.MiddleCenter);
                    requiredTotalAmount.variableText = delegate (BasePlayer player)
                    {
                        var data = GetPlayerData(player);
                        var recipe = GetRecipe(data);
                        if (recipe == null)
                        {
                            return "";
                        }
                        if (index > recipe.RequiredIngredients.Count - 1)
                        {
                            return "";
                        }
                        return (recipe.RequiredIngredients[index].amount * data.CraftAmount).ToString();
                    };

                    RequiredIngredients.Add(requiredTotalAmount);

                    /*UILabel requiredItemName = new UILabel(new Vector2(0.2f, 0f), new Vector2(0.6f, 1f), "", 12, "1 1 1 1", panel, TextAnchor.MiddleCenter);
                    requiredItemName.variableText = delegate (BasePlayer player)
                    {
                        var data = GetPlayerData(player);
                        var recipe = GetRecipe(data);
                        if (recipe == null)
                        {
                            return "";
                        }
                        if (index > recipe.RequiredIngredients.Count - 1)
                        {
                            return "";
                        }
                        return recipe.RequiredIngredients[index].shortname;
                    };*/

                    //UILabel requiredAmount = new UILabel(new Vector2(0.01f, MaxY - height * i), new Vector2(0.97f, MaxY - height * i), "Text"); 
                }

                #endregion

                #region Craft Amount

                UIPanel itemAmountPanel = new UIPanel(new Vector2(0.15f, 0.01f), new Vector2(0.3f, 0.07f), ItemDescription, "0 0 0 0.70");

                ItemAmountLabel = new UILabel(new Vector2(0.1f,0), new Vector2(1,1), parent: itemAmountPanel, labelText: "1", alignment: TextAnchor.MiddleLeft);
                ItemAmountLabel.variableText = delegate (BasePlayer player)
                {
                    var data = GetPlayerData(player);
                    return data.CraftAmount.ToString();
                };


                UIButton itemAmountLess = new UIButton(new Vector2(0.03f, 0.01f), new Vector2(0.13f, 0.07f), "", fontSize: 24, parent: ItemDescription);
                itemAmountLess.AddCallback((player) =>
                {
                    AddCraftAmount(player, -1);
                });
                itemAmountLess.textComponent.Align = TextAnchor.UpperCenter;

                UILabel itemAmountLessLabel = new UILabel(new Vector2(0f, 0.3f), new Vector2(1f, 1.3f), "_", 26, parent: itemAmountLess);

                UIButton itemAmountMore = new UIButton(new Vector2(0.32f, 0.01f), new Vector2(0.42f, 0.07f), "+", fontSize: 26, parent: ItemDescription);
                itemAmountMore.AddCallback((player) =>
                {
                    AddCraftAmount(player, 1);
                });

                #endregion

                #region Craft Button

                UIButton craftButton = new UIButton(new Vector2(0.75f, 0.01f), new Vector2(0.97f, 0.07f), "CRAFT", fontSize: 14, parent: ItemDescription);
                craftButton.AddCallback((player) =>
                {
                    CraftButtonClicked(player);
                });

                #endregion

            }

            private void SetCraftPage(BasePlayer player, ModSettings mod, int index)
            {

            }

            private void SetModPage(BasePlayer player, int index)
            {
                if (index < 0 || index > Mods.Count - 1)
                {
                    _plugin.PrintError($"SwitchModPage() index out of range! ({index})");
                    return;
                }
                var data = GetPlayerData(player);
                data.SelectedModIndex = index;
                ModSettings mod = Mods[index];  
                PluginNamePanel.Refresh(player);
                ItemGrid.Refresh(player);
            }

            private void SetCraftItemIndex(BasePlayer player, int index)
            {
                _plugin.Puts($"SetCraftItemIndex() {index}");
                var data = GetPlayerData(player);
                var mod = GetModPage(data);
                if (mod == null)
                {
                    return;
                }
                data.SelectedItemIndex = index;
                data.CraftAmount = 1;
                ItemDescription.Refresh(player);
            }

            private void SetCraftAmount(BasePlayer player, int amount)
            {
                var data = GetPlayerData(player);
                data.CraftAmount = amount;
                data.CraftAmount = Mathf.Clamp(data.CraftAmount, 1, 10000);
                ItemAmountLabel.Refresh(player);
                foreach (var label in RequiredIngredients)
                {
                    //label.Refresh(player);
                }
            }

            private void AddCraftAmount(BasePlayer player, int amount)
            {
                var data = GetPlayerData(player);
                data.CraftAmount += amount;
                data.CraftAmount = Mathf.Clamp(data.CraftAmount, 1, 10000);
                ItemAmountLabel.Refresh(player);
                foreach(var label in RequiredIngredients)
                {
                    //label.Refresh(player);
                }
            }

            private void CraftButtonClicked(BasePlayer player)
            {
                _plugin.Puts("CraftButtonClicked()");
                var data = GetPlayerData(player);
                var recipe = GetRecipe(data);
                if (recipe == null)
                {
                    _plugin.PrintError($"CraftButtonClicked() recipe is null!");
                    return;
                }
                if (!recipe.HasItems(player, data.CraftAmount))
                {
                    _plugin.PrintError($"CraftButtonClicked() player doesn't have items!");
                    return;
                }
                QueueRecipe(player, recipe, data.CraftAmount);
            }

            private void QueueRecipe(BasePlayer player, CraftRecipe recipe, int amount)
            {
                var crafter = player.inventory.crafting;
                //TODO: Collect ingredients
                //TODO: Put taken items into task
                //TODO: Need to create a ItemBlueprint per custom recipe (or do something custom)
                ItemCraftTask task = Pool.Get<ItemCraftTask>();
                task.endTime = 0f;
                crafter.taskUID++;
                task.taskUID = crafter.taskUID;
                task.owner = player;
                task.amount = amount;
                task.blueprint = ItemManager.bpList.FirstOrDefault(x => x.targetItem.shortname == recipe.ResultItem.shortname);
                task.skinID = (int)recipe.ResultItem.skinID;

                crafter.queue.Enqueue(task);
                if (task.owner != null)
                {
                    task.owner.Command("note.craft_add", new object[]
                    {
                        task.taskUID,
                        recipe.ResultItem.ItemID(),
                        amount,
                        task.skinID
                    });
                }
            }

            private void TimerLoop()
            {
                //CraftTimePanel.RefreshAll();
                //_plugin.Puts($"TimerLoop()");
            }

            private ModSettings GetModPage(PlayerData playerdata)
            {
                if (playerdata.SelectedModIndex < 0 || playerdata.SelectedModIndex > Mods.Count - 1)
                {
                    //_plugin.PrintError($"GetModPage() index out of range! ({playerdata.SelectedModIndex})");
                    return null;
                }
                return Mods[playerdata.SelectedModIndex];
            }

            private CraftRecipe GetRecipe(PlayerData playerData)
            {
                var mod = GetModPage(playerData);
                if (mod == null)
                {
                    return null;
                }
                if (playerData.SelectedItemIndex < 0 || playerData.SelectedItemIndex > mod.Recipes.Count - 1)
                {
                    return null;
                }
                return mod.Recipes[playerData.SelectedItemIndex];
            }
        }

    }

    #region JakeUIFramework class
    public partial class CraftingTable : RustPlugin
    {
        #region Jake's UI Framework

        private Dictionary<string, UIButton> UIButtonCallBacks { get; set; } = new Dictionary<string, UIButton>();

        void OnButtonClick(ConsoleSystem.Arg arg)
        {
            UIButton button;
            if (UIButtonCallBacks.TryGetValue(arg.cmd.Name, out button))
            {
                button.OnClicked(arg);
                return;
            }
            Puts("Unknown button command: {0}", arg.cmd.Name);
        }

        public class UIElement : UIBaseElement
        {
            public CuiElement Element { get; protected set; }
            public UIOutline Outline { get; set; }
            public CuiRectTransformComponent transform { get; protected set; }
            public float FadeOut
            {
                get
                {
                    return Element == null ? _fadeOut : Element.FadeOut;
                }
                set
                {
                    if (Element != null)
                    {
                        Element.FadeOut = value;
                    }
                    _fadeOut = value;
                }
            }
            private float _fadeOut = 0f;

            public string Name { get { return Element.Name; } }

            public UIElement(UIBaseElement parent = null) : base(parent)
            {

            }

            public UIElement(Vector2 position, float width, float height, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), parent)
            {

            }

            public UIElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent)
            {
                transform = new CuiRectTransformComponent();
                Element = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = this.parent == null ? this.Parent : this.parent.Parent,
                    Components =
                        {
                            transform,
                        },
                    FadeOut = _fadeOut,
                };
                UpdatePlacement();

                Init();
            }

            public void AddOutline(string color = "0 0 0 1", string distance = "1 -1")
            {
                Outline = new UIOutline(color, distance);
                Element.Components.Add(Outline.component);
            }

            public virtual void Init()
            {

            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (this is UIElement)
                {
                    if (!CanShow(player))
                    {
                        return;
                    }
                }
                if (AddPlayer(player))
                {
                    SafeAddUi(player, Element);
                }
                base.Show(player, children);
            }

            public override void Hide(BasePlayer player, bool children = true)
            {
                base.Hide(player, children);
                if (RemovePlayer(player))
                {
                    SafeDestroyUi(player, Element);
                }
            }

            public override void UpdatePlacement()
            {
                base.UpdatePlacement();
                if (transform != null)
                {
                    transform.AnchorMin = $"{globalPosition.x} {globalPosition.y}";
                    transform.AnchorMax = $"{globalPosition.x + globalSize.x} {globalPosition.y + globalSize.y}";
                }
                //RefreshAll();
            }

            public void SetPositionAndSize(CuiRectTransformComponent trans)
            {
                transform.AnchorMin = trans.AnchorMin;
                transform.AnchorMax = trans.AnchorMax;

                //_plugin.Puts($"POSITION [{transform.AnchorMin},{transform.AnchorMax}]");

                RefreshAll();
            }

            public void SetParent(UIElement element)
            {
                Element.Parent = element.Element.Name;
                UpdatePlacement();
            }

        }

        public class UIButton : UIElement
        {
            public CuiButtonComponent buttonComponent { get; private set; }
            public CuiTextComponent textComponent { get; private set; }
            private UILabel label { get; set; }
            private string _textColor { get; set; }
            private string _buttonText { get; set; }
            public string Text { set { textComponent.Text = value; } }
            public Func<BasePlayer, string> variableText { get; set; }

            private int _fontSize;

            public Action<ConsoleSystem.Arg> onClicked;

            public UIButton(Vector2 min = default(Vector2), Vector2 max = default(Vector2), string buttonText = "", string buttonColor = "0 0 0 0.85", string textColor = "1 1 1 1", int fontSize = 15, UIBaseElement parent = null) : base(min, max, parent)
            {
                buttonComponent = new CuiButtonComponent();

                _fontSize = fontSize;
                _textColor = textColor;
                _buttonText = buttonText;

                buttonComponent.Command = CuiHelper.GetGuid();
                buttonComponent.Color = buttonColor;

                Element.Components.Insert(0, buttonComponent);

                _plugin.cmd.AddConsoleCommand(buttonComponent.Command, _plugin, "OnButtonClick");

                _plugin.UIButtonCallBacks[buttonComponent.Command] = this;

                label = new UILabel(new Vector2(0, 0), new Vector2(1, 1), fontSize: _fontSize, parent: this);

                textComponent = label.text;

                label.text.Align = TextAnchor.MiddleCenter;
                label.text.Color = _textColor;
                label.Text = _buttonText;
                label.text.FontSize = _fontSize;

            }

            public override void Init()
            {
                base.Init();

            }

            public virtual void OnClicked(ConsoleSystem.Arg args)
            {
                onClicked.Invoke(args);
            }

            public void AddChatCommand(string fullCommand)
            {
                if (fullCommand == null)
                {
                    return;
                }
                /*
                List<string> split = fullCommand.Split(' ').ToList();
                string command = split[0];
                split.RemoveAt(0); //Split = command args now*/
                onClicked += (arg) =>
                {
                    _plugin.rust.RunClientCommand(arg.Player(), $"chat.say \"/{fullCommand}\"");
                    //plugin.Puts($"Calling chat command {command} {string.Join(" ",split.ToArray())}");
                    //Need to call chat command somehow here
                };
            }

            public void AddCallback(Action<BasePlayer> callback)
            {
                if (callback == null)
                {
                    return;
                }
                onClicked += (args) => { callback(args.Player()); };
            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableText != null)
                {
                    Text = variableText.Invoke(player);
                }
                base.Show(player, children);
            }
        }

        public class UIBackgroundText : UIPanel
        {
            public UILabel Label;

            public UIBackgroundText(Vector2 min = default(Vector2), Vector2 max = default(Vector2), UIBaseElement parent = null, string backgroundColor = "0 0 0 0.85", string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", TextAnchor alignment = TextAnchor.MiddleCenter) : base(min, max, parent)
            {
                Label = new UILabel(new Vector2(0, 0), new Vector2(1, 1), labelText, fontSize, fontColor, parent, alignment);
            }
        }

        public class UILabel : UIElement
        {
            public CuiTextComponent text { get; private set; }

            public UILabel(Vector2 min = default(Vector2), Vector2 max = default(Vector2), string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", UIBaseElement parent = null, TextAnchor alignment = TextAnchor.MiddleCenter) : base(min, max, parent)
            {

                if (min == Vector2.zero && max == Vector2.zero)
                {
                    max = Vector2.one;
                }

                text = new CuiTextComponent();

                text.Text = labelText;
                text.Color = fontColor;
                text.Align = alignment;
                text.FontSize = fontSize;

                Element.Components.Insert(0, text);
            }

            public string Text { set { text.Text = value; } }
            public TextAnchor Allign { set { text.Align = value; } }
            public Color Color { set { text.Color = value.ToString(); } }
            public string ColorString { set { text.Color = value; } }

            public Func<BasePlayer, string> variableText { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableText != null)
                {
                    Text = variableText.Invoke(player);
                }
                base.Show(player, children);
            }

            public override void Init()
            {
                base.Init();

                if (parent != null)
                {
                    if (parent is UIButton)
                    {
                        Element.Parent = (parent as UIButton).Name;
                        transform.AnchorMin = $"{localPosition.x} {localPosition.y}";
                        transform.AnchorMax = $"{localPosition.x + localSize.x} {localPosition.y + localSize.y}";
                    }
                }
            }

        }

        public class UIImageBase : UIElement
        {
            public UIImageBase(Vector2 min, Vector2 max, UIBaseElement parent) : base(min, max, parent)
            {
            }

            private CuiNeedsCursorComponent needsCursor { get; set; }

            private bool requiresFocus { get; set; }

            public bool CursorEnabled
            {
                get
                {
                    return requiresFocus;
                }
                set
                {
                    if (value)
                    {
                        needsCursor = new CuiNeedsCursorComponent();
                        Element.Components.Add(needsCursor);
                    }
                    else
                    {
                        Element.Components.Remove(needsCursor);
                    }

                    requiresFocus = value;
                }
            }
        }

        public class UIPanel : UIImageBase
        {
            private CuiImageComponent panel;

            public UIPanel(Vector2 min, Vector2 max, UIBaseElement parent = null, string color = "0 0 0 0.85") : base(min, max, parent)
            {
                panel = new CuiImageComponent
                {
                    Color = color,
                };

                Element.Components.Insert(0, panel);
            }

            public UIPanel(Vector2 position, float width, float height, UIBaseElement parent = null, string color = "0 0 0 .85") : this(position, new Vector2(position.x + width, position.y + height), parent,  color)
            {

            }
        }

        public class UIButtonContainer : UIBaseElement
        {
            private IEnumerable<UIButtonConfiguration> _buttonConfiguration;
            private Vector2 _position;
            private float _width;
            private float _height;
            private string _title;
            private string _panelColor;
            private bool _stackedButtons;
            private float _paddingPercentage;
            private int _titleSize;
            private int _buttonFontSize;


            const float TITLE_PERCENTAGE = 0.20f;

            private float _paddingAmount;
            private bool _hasTitle;

            public UIButtonContainer(IEnumerable<UIButtonConfiguration> buttonConfiguration, string panelBgColor, Vector2 position, float width, float height, float paddingPercentage = 0.05f, string title = "", int titleSize = 30, int buttonFontSize = 15, bool stackedButtons = true, UIBaseElement parent = null) : base(parent)
            {
                _buttonConfiguration = buttonConfiguration;
                _position = position;
                _width = width;
                _height = height;
                _title = title;
                _titleSize = titleSize;
                _panelColor = panelBgColor;
                _stackedButtons = stackedButtons;
                _paddingPercentage = paddingPercentage;
                _buttonFontSize = buttonFontSize;

                Init();
            }

            private void Init()
            {
                var panel = new UIPanel(new Vector2(_position.x, _position.y), _width, _height, this, _panelColor);

                _paddingAmount = (_stackedButtons ? _height : _width) * _paddingPercentage / _buttonConfiguration.Count();

                var firstButtonPosition = new Vector2(_position.x + _paddingAmount, _position.y + _paddingAmount);
                var titleHeight = TITLE_PERCENTAGE * _height;

                if (!string.IsNullOrEmpty(_title))
                {
                    _hasTitle = true;

                    var titlePanel = new UIPanel(new Vector2(_position.x, _position.y + _height - titleHeight), _width, titleHeight, this);
                    var titleLabel = new UILabel(Vector2.zero, Vector2.zero, _title, fontSize: _titleSize, parent: titlePanel);
                }

                var buttonHeight = (_height - (_paddingAmount * 2) - (_hasTitle ? titleHeight : 0) - (_paddingAmount * (_buttonConfiguration.Count() - 1))) / (_stackedButtons ? _buttonConfiguration.Count() : 1);
                var buttonWidth = _stackedButtons
                    ? (_width - (_paddingAmount * 2))
                    : ((_width - (_paddingAmount * 2) - (_paddingAmount * (_buttonConfiguration.Count() - 1))) / _buttonConfiguration.Count());

                for (var buttonId = 0; buttonId < _buttonConfiguration.Count(); buttonId++)
                {
                    var buttonConfig = _buttonConfiguration.ElementAt(buttonId);
                    var button = new UIButton(buttonText: buttonConfig.ButtonName, buttonColor: buttonConfig.ButtonColor, fontSize: _buttonFontSize);

                    if (!_stackedButtons)
                    {
                        button.SetPosition(
                            firstButtonPosition.x + ((buttonWidth + _paddingAmount) * buttonId + _paddingAmount),
                            firstButtonPosition.y + (_paddingAmount) * 2);
                    }
                    else
                    {
                        button.SetPosition(
                            firstButtonPosition.x,
                            firstButtonPosition.y + ((buttonHeight + _paddingAmount) * buttonId + _paddingAmount));
                    }

                    button.SetSize(
                        buttonWidth - (_stackedButtons ? 0 : _paddingAmount * 2),
                        buttonHeight - (_stackedButtons ? _paddingAmount * 2 : 0));

                    button.AddCallback(buttonConfig.callback);
                    button.AddChatCommand(buttonConfig.ButtonCommand);
                }
            }
        }

        public class UIButtonConfiguration
        {
            public string ButtonName { get; set; }
            public string ButtonCommand { get; set; }
            public string ButtonColor { get; set; }
            public Action<BasePlayer> callback { get; set; }
        }

        public class UIImage : UIImageBase
        {
            public CuiImageComponent Image { get; private set; }

            public UIImage(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent)
            {
                Image = new CuiImageComponent();
                Element.Components.Insert(0, Image);
            }

            public UIImage(Vector2 position, float width, float height, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), parent)
            {
                Image = new CuiImageComponent();
                Element.Components.Insert(0, Image);
            }

            public Func<BasePlayer, string> variableSprite { get; set; }
            public Func<BasePlayer, string> variablePNG { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableSprite != null)
                {
                    Image.Sprite = variableSprite.Invoke(player);
                }
                if (variablePNG != null)
                {
                    Image.Png = variablePNG.Invoke(player);
                }
                base.Show(player, children);
            }
        }

        public class UIRawImage : UIImageBase
        {
            public CuiRawImageComponent Image { get; private set; }

            public UIRawImage(Vector2 position, float width, float height, UIBaseElement parent = null, string url = "") : this(position, new Vector2(position.x + width, position.y + height), parent, url)
            {

            }

            public UIRawImage(Vector2 min, Vector2 max, UIBaseElement parent = null, string url = "") : base(min, max, parent)
            {
                Image = new CuiRawImageComponent()
                {
                    Url = url,
                    Sprite = "assets/content/textures/generic/fulltransparent.tga"
                };

                Element.Components.Insert(0, Image);
            }

            public Func<BasePlayer, string> variablePNG { get; set; }

            public Func<BasePlayer, string> variableURL { get; set; }

            public Func<BasePlayer, string> variablePNGURL { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variablePNGURL != null)
                {
                    string url = variablePNGURL.Invoke(player);
                    if (string.IsNullOrEmpty(url))
                    {
                        Image.Png = null;
                        Image.Url = null;
                    }
                    ulong num;
                    if (ulong.TryParse(url, out num))
                    {
                        Image.Png = url;
                    }
                    else
                    {
                        Image.Url = url;
                    }
                }
                else
                {
                    if (variablePNG != null)
                    {
                        Image.Png = variablePNG.Invoke(player);
                        if (string.IsNullOrEmpty(Image.Png))
                        {
                            Image.Png = null;
                        }
                    }
                    if (variableURL != null)
                    {
                        Image.Url = variableURL.Invoke(player);
                        if (string.IsNullOrEmpty(Image.Url))
                        {
                            Image.Url = null;
                        }
                    }
                }

                base.Show(player, children);
            }
        }

        public class UIBaseElement
        {
            public Vector2 localPosition { get; set; } = new Vector2();
            public Vector2 localSize { get; set; } = new Vector2();
            public Vector2 globalSize { get; set; } = new Vector2();
            public Vector2 globalPosition { get; set; } = new Vector2();
            public HashSet<BasePlayer> players { get; set; } = new HashSet<BasePlayer>();
            public UIBaseElement parent { get; set; }
            public HashSet<UIBaseElement> children { get; set; } = new HashSet<UIBaseElement>();
            public Vector2 min { get { return localPosition; } }
            public Vector2 max { get { return localPosition + localSize; } }
            public string Parent { get; set; } = "Hud.Menu";

            public Func<BasePlayer, bool> conditionalShow { get; set; }
            public Func<BasePlayer, Vector2> conditionalSize { get; set; }
            public Func<BasePlayer, Vector2> conditionalPosition { get; set; }

            public UIBaseElement(UIBaseElement parent = null)
            {
                this.parent = parent;
            }

            public UIBaseElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : this(parent)
            {
                localPosition = min;
                localSize = max - min;
                if (parent != null && this != parent)
                {
                    parent.AddElement(this);
                }
                UpdatePlacement();
            }

            public void AddElement(UIBaseElement element)
            {
                if (element == this)
                {
                    _plugin.Puts("[UI FRAMEWORK] WARNING: AddElement() trying to add self as parent!");
                    return;
                }
                if (!children.Contains(element))
                {
                    children.Add(element);
                }
            }

            public void RemoveElement(UIBaseElement element)
            {
                children.Remove(element);
            }

            public void Refresh(BasePlayer player)
            {
                Hide(player);
                Show(player);
            }

            public bool AddPlayer(BasePlayer player)
            {
                if (!players.Contains(player))
                {
                    players.Add(player);
                    return true;
                }

                foreach (var child in children)
                {
                    child.AddPlayer(player);
                }

                return false;
            }

            public bool RemovePlayer(BasePlayer player)
            {
                return players.Remove(player);
            }

            public void Show(List<BasePlayer> players)
            {
                foreach (BasePlayer player in players)
                {
                    Show(player);
                }
            }

            public void Show(HashSet<BasePlayer> players)
            {
                foreach (BasePlayer player in players)
                {
                    Show(player);
                }
            }

            public virtual void Hide(BasePlayer player, bool hideChildren = true)
            {
                foreach (var child in children)
                {
                    child.Hide(player, hideChildren);
                }

                if (GetType() == typeof(UIBaseElement))
                {
                    RemovePlayer(player);
                }
            }

            public virtual void Show(BasePlayer player, bool showChildren = true)
            {
                if (GetType() == typeof(UIBaseElement))
                {
                    if (!CanShow(player))
                    {
                        return;
                    }
                }
                if (conditionalSize != null)
                {
                    Vector2 returnSize = conditionalSize.Invoke(player);
                    if (returnSize != null)
                    {
                        SetSize(returnSize.x, returnSize.y);
                    }
                }

                if (conditionalPosition != null)
                {
                    Vector2 returnPos = conditionalPosition.Invoke(player);
                    if (returnPos != null)
                    {
                        SetPosition(returnPos.x, returnPos.y);
                    }
                }

                if (GetType() == typeof(UIBaseElement))
                {
                    AddPlayer(player);
                }

                foreach (var child in children)
                {
                    child.Show(player, showChildren);
                }
            }

            public bool CanShow(BasePlayer player)
            {
                if (conditionalShow == null)
                {
                    return true;
                }
                if (conditionalShow(player))
                {
                    return true;
                }
                return false;
            }

            public void HideAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Hide(player);
                }
            }

            public void RefreshAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Refresh(player);
                }
            }

            public void SafeAddUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts(JsonConvert.SerializeObject(element));
                    List<CuiElement> elements = new List<CuiElement>();
                    elements.Add(element);
                    CuiHelper.AddUi(player, elements);
                }
                catch (Exception ex)
                {

                }
            }

            public void SafeDestroyUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts($"Deleting {element.Name} to {player.userID}");
                    CuiHelper.DestroyUi(player, element.Name);
                }
                catch (Exception ex)
                {

                }
            }

            public void SetSize(float x, float y)
            {
                localSize = new Vector2(x, y);
                UpdatePlacement();
            }

            public void SetPosition(float x, float y)
            {
                localPosition = new Vector2(x, y);
                UpdatePlacement();
            }

            public virtual void UpdatePlacement()
            {
                if (parent == null)
                {
                    globalSize = localSize;
                    globalPosition = localPosition;
                }
                else
                {
                    globalSize = Vector2.Scale(parent.globalSize, localSize);
                    globalPosition = parent.globalPosition + Vector2.Scale(parent.globalSize, localPosition);
                }

                foreach (var child in children)
                {
                    child.UpdatePlacement();
                }
            }
        }

        public class UICheckbox : UIButton
        {
            public UICheckbox(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent: parent)
            {

            }
        }

        public class UIOutline
        {
            public CuiOutlineComponent component;

            public string Color { get { return _color; } set { _color = value; UpdateComponent(); } }
            public string Distance { get { return _distance; } set { _distance = value; UpdateComponent(); } }

            private string _color = "0 0 0 1";
            private string _distance = "0.25 0.25";

            public UIOutline()
            {

            }

            public UIOutline(string color, string distance)
            {
                _color = color;
                _distance = distance;
                UpdateComponent();
            }

            private void UpdateComponent()
            {
                if (component == null)
                {
                    component = new CuiOutlineComponent();
                }
                component.Color = _color;
                component.Distance = _distance;
            }
        }

        #endregion

        #region ColorText

        public static string ColorText(string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        }

        public static string ColorText(string text, string color)
        {
            return $"<color=#{color}>{text}</color>";
        }

        #endregion
    }
    #endregion

    #region MonoBehavior Class
    public partial class CraftingTable : RustPlugin
    {
        #region MonoBehaviors

        public const float ColliderRadius = 2.5f;
        public const float ColliderHeight = 3f;
        public const float ColliderHeightDifference = 0.3f;

        public class MyBaseTrigger : MonoBehaviour
        {
            public List<BasePlayer> _players { get; set; }
            private CapsuleCollider _collider { get; set; }
            private Rigidbody rigidbody { get; set; }
            private Timer _timer { get; set; }

            void Awake()
            {
                Init();
            }

            void OnDestroy()
            {
                OnDestroyed();
            }

            public virtual void OnDestroyed()
            {
                GameObject.Destroy(_collider);
            }

            public virtual void Init()
            {
                if (_players == null)
                {
                    _players = new List<BasePlayer>();
                }
                gameObject.layer = 3; //hack to get all trigger layers (Found in zone manager)

                _collider = gameObject.AddComponent<CapsuleCollider>();
                _collider.radius = ColliderRadius;
                _collider.height = ColliderHeight;
                _collider.isTrigger = true;
            }

            void OnTriggerEnter(Collider collider)
            {
                BasePlayer player = collider.gameObject?.GetComponentInParent<BasePlayer>();
                if (player)
                {
                    if (!player.IsConnected || player.IsSleeping())
                    {
                        return;
                    }
                    OnPlayerEnter(player);
                }
            }

            void OnTriggerExit(Collider collider)
            {
                BasePlayer player = collider.gameObject?.GetComponentInParent<BasePlayer>();
                if (player)
                {
                    OnPlayerExit(player);
                }
            }

            public virtual void OnPlayerExit(BasePlayer player)
            {
                _players.Remove(player);
            }

            public virtual void OnPlayerEnter(BasePlayer player)
            {
                _players.Add(player);
            }
        }

        public class LookingAtTrigger : MyBaseTrigger
        {
            private Timer _timer { get; set; }
            private Dictionary<BasePlayer, BaseEntity> _lookingEntity { get; set; } = new Dictionary<BasePlayer, BaseEntity>();

            void Awake()
            {
                Init();
            }

            public override void Init()
            {
                base.Init();
                _timer = _plugin.timer.Every(0.2f, TimerLoop);
            }

            private BaseEntity RaycastLookDirection(BasePlayer player, string prefabName = "")
            {
                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 5f, int.MaxValue))
                {
                    return null;
                }
                BaseEntity entity = hit.collider.GetComponentInParent<BaseEntity>();
                return entity;
            }

            private void TimerLoop()
            {
                if (_players.Count == 0)
                {
                    return;
                }
                foreach (var player in _players)
                {
                    var ent = RaycastLookDirection(player);
                    BaseEntity lastEntity;
                    if (!_lookingEntity.TryGetValue(player, out lastEntity))
                    {
                        _lookingEntity[player] = ent;
                        return;
                    }
                    if (ent != lastEntity)
                    {
                        OnPlayerStopLookingAtEntity(player, lastEntity);
                    }
                    _lookingEntity[player] = ent;
                    if (ent == null)
                    {
                        continue;
                    }
                    OnPlayerLookAtEntity(player, ent);
                }
            }

            public virtual void OnPlayerLookAtEntity(BasePlayer player, BaseEntity entity)
            {
                if (player.input.state.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    OnPlayerLeftClickEntity(player, entity);
                }
                if (player.input.state.WasJustPressed(BUTTON.FIRE_SECONDARY))
                {
                    OnPlayerRightClickEntity(player, entity);
                }
            }

            public virtual void OnPlayerStopLookingAtEntity(BasePlayer player, BaseEntity entity)
            {

            }

            public virtual void OnPlayerLeftClickEntity(BasePlayer player, BaseEntity entity)
            {

            }

            public virtual void OnPlayerRightClickEntity(BasePlayer player, BaseEntity entity)
            {

            }

            public override void OnDestroyed()
            {
                base.OnDestroyed();
                _timer?.Destroy();
            }

            public BaseEntity GetLookingEntity(BasePlayer player)
            {
                BaseEntity entity;
                if (!_lookingEntity.TryGetValue(player, out entity))
                {
                    return null;
                }
                return entity;
            }

            public override void OnPlayerExit(BasePlayer player)
            {
                base.OnPlayerExit(player);
                _lookingEntity.Remove(player);
            }
        }

        #endregion
    }
    #endregion
}