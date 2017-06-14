
using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Combatlog", "Jake_Rich", 1.0)]
    [Description("Get combatlog data on other players")]

    public class Combatlog : RustPlugin
    {
        [ConsoleCommand("getcombatlog")]
        void GetCombatlog_ChatCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                arg.ReplyWith("This command is for admins only!");
                return;
            }

            if (arg.Args.Length != 1)
            {
                arg.ReplyWith("Need 1 argument!");
                return;
            }

            ulong userID;
            if (!ulong.TryParse(arg.Args[0], out userID))
            {
                arg.ReplyWith($"{ arg.Args[0]} is not a valid userID!");
                return;
            }

            BasePlayer targetPlayer = BasePlayer.FindByID(userID);
            if (targetPlayer == null)
            {
                arg.ReplyWith($"{ userID} was not found!");
                return;
            }

            arg.ReplyWith(targetPlayer.stats.combat.Get(100));
        }

        public struct CombatEvent
        {
            public float time;

            public ulong attacker_id;

            public ulong victim_id;

            public string weapon;

            public string ammo;

            public string bone;

            public HitArea area;

            public float distance;

            public float health_old;

            public float health_new;

            public string info;
        }

        public void LogEvent(CombatEvent attack)
        {
            savedData.Instance.Add(attack);
        }

        public void Log(HitInfo info, float health_old, float health_new)
        {
            LogEvent(new CombatEvent
            {
                time = Time.realtimeSinceStartup,
                attacker_id = ((!info.Initiator || info.Initiator.net == null) ? 0u : info.Initiator.net.ID),
                victim_id = ((!info.HitEntity || info.HitEntity.net == null) ? 0u : info.HitEntity.net.ID),
                weapon = ((!info.WeaponPrefab) ? "N/A" : info.WeaponPrefab.name),
                ammo = ((!info.ProjectilePrefab) ? "N/A" : info.ProjectilePrefab.name),
                bone = info.boneName,
                area = info.boneArea,
                distance = ((!info.IsProjectile()) ? Vector3.Distance(info.PointStart, info.HitPositionWorld) : info.ProjectileDistance),
                health_old = health_old,
                health_new = health_new,
            });
        }

        public CompressedSaveData<SavedData> savedData { get; set; }

        #region Hooks

        public static Oxide.Plugins.Combatlog _plugin { get; set; }

        void Init()
        {
            _plugin = this;
            savedData = new CompressedSaveData<SavedData>("Combatlog-History");
        }

        void Unload()
        {
            savedData.Save();
        }

        #endregion

        public static void Log(string str)
        {
            _plugin.Puts(str);
        }

        public static void Log(object obj)
        {
            _plugin.Puts(obj.ToString());
        }

        public class ConfigurationAccessor<Type> where Type : class
        {
            #region Typed Configuration Accessors

            private Type GetTypedConfigurationModel(string storageName)
            {
                return Interface.Oxide.DataFileSystem.ReadObject<Type>(storageName);
            }

            private void SaveTypedConfigurationModel(string storageName, Type storageModel)
            {
                Interface.Oxide.DataFileSystem.WriteObject(storageName, storageModel);
            }

            #endregion

            public string name { get; set; }
            public Type Instance { get; set; }

            public ConfigurationAccessor(string name)
            {
                this.name = name;
                Init();
            }

            public virtual void Init()
            {
                Reload();
            }

            public virtual void Load()
            {
                Instance = GetTypedConfigurationModel(name);
            }

            public virtual void Save()
            {
                SaveTypedConfigurationModel(name, Instance);
            }

            public virtual void Reload()
            {
                Load(); //Need to load and save to init list
                Save();
                Load();
            }
        }

        public class CompressedSaveData<T> where T : class
        {
            public string Name { get; private set; }
            private string SerializedJSON { get; set; }

            public T Instance { get; set; }

            public CompressedSaveData(string name)
            {
                Name = name;
                Load();
            }

            private void OnLoad()
            {
                string str = Interface.Oxide.DataFileSystem.ReadObject<StringClass>(Name).String;
                Log($"OnLoad() String is {str}");
                if (str == null)
                {
                    Log("OnLoad() null string, initializing as default value");
                    Instance = default(T);
                    return;
                }
                var json = System.Text.Encoding.UTF8.GetString(Facepunch.Utility.Compression.Uncompress(System.Convert.FromBase64String(str)));
                Log($"OnLoad() decompressed JSON is {json}");
                Instance = JsonConvert.DeserializeObject<T>(json); //Reverse of below
            }

            private void OnSave()
            {
                string json = JsonConvert.SerializeObject(Instance);
                var stringClass = new StringClass(System.Convert.ToBase64String(Facepunch.Utility.Compression.Compress(System.Text.Encoding.UTF8.GetBytes(json))));
                Log($"Json Length: {json.Length} CompressedLength {stringClass.String.Length}");

                Interface.Oxide.DataFileSystem.WriteObject(Name, stringClass);
            }

            public void Save()
            {
                OnSave();
            }

            public void Load()
            {
                OnLoad();
            }

            public class StringClass
            {
                public string String { get; set; }

                public StringClass()
                {

                }

                public StringClass(string str)
                {
                    String = str;
                }
            }
        }

        public class SavedData
        {
            private List<CombatEvent> events { get; set; } = new List<CombatEvent>();

            public SavedData()
            {

            }

            public void Add(CombatEvent action)
            {
                events.Add(action);
            }
        }

        [ChatCommand("fakecombatlog")]
        void ChatCmd_FakeAttacks(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            if (savedData.Instance == null)
            {
                _plugin.Puts($"Instance is null");
            }
            for(int i = 0; i < 100; i++)
            {
                savedData.Instance.Add(new CombatEvent()
                {
                });
            }
            savedData.Save();
        }
    }
}


