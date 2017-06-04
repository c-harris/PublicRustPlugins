using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System;
using System.Collections;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Network;
using Facepunch;
using System.IO;
using ProtoBuf;
using Newtonsoft.Json;
using System.Text;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust;
using Rust;
//Reference: Anticheat_SharedClasses

namespace Oxide.Plugins
{
    [Info("RustGuardManager", "Jake_Rich", "1.0.0")]
    [Description("Handles updates")]

    public class RustGuardManager : RustPlugin
    {
        public static RustGuardManager _plugin { get; set; }
        private Timer _updateTimer;

        public const string PLUGIN_NAME = "RustGuard";
        public const string REPO_AUTHOR = "Aleks976";
        public const string REPO_NAME = "PublicRustPlugins";
        public const string BRANCH = "master";

        public List<string> filesToUpdate = new List<string>()
        {
            "RustGuard.cs",
            "RustGuardManager.cs",
        };

        [PluginReference]
        RustGuard RustGuard;

        string lastVersion;

        #region Hooks

        void OnServerInitialized()
        {
            Settings = new ConfigurationAccessor<ConfigData>("RustGuardManager.json");

            foreach(var file in filesToUpdate)
            {
                if (!Settings.Instance.FileHashes.ContainsKey(file))
                {
                    Settings.Instance.FileHashes.Add(file, "");
                }
            }

            CheckUpdateLoop();
            _updateTimer = timer.Every(600f, CheckUpdateLoop);
        }

        void Unload()
        {
            _updateTimer?.Destroy();

            Settings.Save();
        }

        #endregion

        public void CheckUpdateLoop()
        {
            CheckGithub();
        }

        #region Making sure plugin works / error checking

        public void CheckPluginProperlyUpdated(string name)
        {
            string errorMsg = RustGuard?.GetStatus();
            if (RustGuard == null || errorMsg == "")
            {
                LogError("RustGuard wasn't found! (Likely didn't compile)");
                return;
            }
            if (errorMsg != "ok")
            {
                LogError(errorMsg);
                return;
            }
            lastVersion = RustGuard.Version.ToString();
        }

        public void LogError(string error)
        {
            string formatedMsg = FormatErrorMsg(error);

        }

        public string FormatErrorMsg(string error)
        {
            string version = lastVersion;
            if (RustGuard?.Version != null)
            {
                version = $"{lastVersion} -> {RustGuard?.Version}";
            }
            string formatedMsg = $"RustGuard ({version}) {error}";
            return formatedMsg;
        }

        #endregion

        #region Updating From Github

        public const string gitHubURL = "https://api.github.com/repos/";
        public const string gitHubRawURL = "https://raw.githubusercontent.com/";

        public ConfigurationAccessor<ConfigData> Settings;

        #region Classes

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

            private string name { get; set; }
            public Type Instance { get; set; }

            public ConfigurationAccessor(string name)
            {
                this.name = name;
                Init();
                Reload();
            }

            public virtual void Init()
            {

            }

            public void Load()
            {
                Instance = GetTypedConfigurationModel(name);
            }

            public void Save()
            {
                SaveTypedConfigurationModel(name, Instance);
            }

            public void Reload()
            {
                Load(); //Need to load and save to init list
                Save();
                Load();
            }
        }

        public class ConfigData
        {
            public Dictionary<string, string> FileHashes = new Dictionary<string, string>();
        }

        public class GithubResponse
        {
            public string content { get; set; }
            public string name { get; set; }
            public string path { get; set; }
            public string sha { get; set; }
            public int size { get; set; }
            public string type { get; set; }
            public string encoding { get; set; }
            public string url { get; set; }
        }

        #endregion

        #region Headers

        Dictionary<string, string> headers = new Dictionary<string, string>
        {
            {"User-Agent", "aleks976" },
            {"Accept", "application/vnd.github.v3+json"}
        };

        #endregion

        public bool CheckGithub()
        { 
            foreach (var fileHash in Settings.Instance.FileHashes.ToList())
            {
                string url = $"ttps://api.github.com/repos/{REPO_AUTHOR}/{REPO_NAME}/commits/{BRANCH}?path={fileHash.Key}";
                webrequest.EnqueueGet(url,
                (code, response) =>
                {
                    GithubResponse gitReponse; //Check if SHA hash has changed
                    if (code == 200) //OK
                    {
                        gitReponse = JsonConvert.DeserializeObject<GithubResponse>(response);
                    }
                    else
                    {
                        Puts($"Error code {code} when getting commits.");
                        return;
                    }
                    if (fileHash.Value != "")
                    {
                        if (fileHash.Value == gitReponse.sha)
                        {
                            return;
                        }
                    }
                    Settings.Instance.FileHashes[fileHash.Key] = gitReponse.sha;
                    //Update plugin if it is different
                    RawWriter.Write(Convert.FromBase64String(gitReponse.content), Oxide.Core.Interface.Oxide.PluginDirectory);
                    Puts($"Updated {fileHash.Key} from Github");

                }, this, headers, 20f);
            }
            return true;
        }

        #endregion
    }
}