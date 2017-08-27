using Oxide.Core;
using Oxide.Core.Plugins;
// Reference: Oxide.Core.MySql
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Connection = Oxide.Core.Database.Connection;

namespace Oxide.Plugins
{
    [Info("BanSync", "Jake_Rich", 0.1)]
    [Description("Syncs bans across servers")]
    class BanSync : RustPlugin
    {
        //Make sure table is setup   | steamid | name | reason |


        public class MySqlConstants
        {
            //public static string ADD_BAN_TO_DATABASE = "INSERT IGNORE INTO `{0}`.`{1}` (`steamid`, `name`, `reason`) VALUES {2}";
            public static string ADD_BAN_TO_DATABASE = "INSERT INTO `{0}`.`{1}` (`steamid`, `name`, `reason`) VALUES {2} ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `reason` = VALUES(`reason`)";
            public static string SELECT_ALL_BANS = "SELECT * FROM `{0}`.`{1}`";
            //public static string ADD_UNBAN = "INSERT IGNORE INTO `{0}`.`{1}` (`steamid`, `name`, `reason`) VALUES {2}";
            public static string REMOVE_BAN = "DELETE FROM `{0}`.`{1}` WHERE `steamid` = '{2}'";
        }

        public Timer _syncTimer { get; set; }
        public static BanSync _plugin { get; set; }
        public bool Initialized { get; set; }
        #region Hooks

        void OnServerInitialized()
        {
            Settings = new ConfigurationAccessor<BanConfig>("BanSync-Config");
            if (Settings.Instance.DatabaseServerIP == "")
            {
                Puts("Please enter the database's IP in oxide/data/BanSync-Config.data");
                return;
            }
            if (Settings.Instance.DatabaseUserName == "")
            {
                Puts("Please enter the database username in oxide/data/BanSync-Config.data");
                return;
            }
            if (Settings.Instance.DatabaseName == "")
            {
                Puts("Please enter the ban database's name in oxide/data/BanSync-Config.data");
                return;
            }
            if (Settings.Instance.TableName == "")
            {
                Puts("Please enter the ban table's name in oxide/data/BanSync-Config.data");
                return;
            }

            SyncBans();
            _syncTimer?.Destroy();
            _syncTimer = timer.Every(60f, SyncBans);
            Initialized = true;
        }

        void OnServerCommand(ConsoleSystem.Arg args)
        {
            if (!Initialized)
            {
                return;
            }
            if (!args.IsAdmin)
            {
                return;
            }
            if (args.cmd == null)
            {
                return;
            }
            switch(args.cmd.Name)
            {
                case "banid":
                    {
                        BanID(args);
                        break;
                    }
                case "unban":
                    {
                        Unban(args);
                        break;
                    }
            }
        }

        void Init()
        {
            _plugin = this;
        }

        void Unload()
        {
            _syncTimer?.Destroy();
        }

        #endregion

        public ConfigurationAccessor<BanConfig> Settings { get; set; }

        public List<PlayerBan> UnnamedBans { get; set; } = new List<PlayerBan>();
        public List<PlayerBan> BansToSend { get; set; } = new List<PlayerBan>();

        #region Configuration classes

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

        #endregion

        #region SteamAPI

        public class SteamAlias_Ajax
        {
            public string newname { get; set; }
            public string timechanged { get; set; }
            public DateTime lastChange { get { return DateTime.Parse(timechanged); } }
        }

        public const string SteamAPI_PastUsernameFormat = "http://steamcommunity.com/profiles/{0}/ajaxaliases";

        #endregion

        public class BanConfig
        {
            public string DatabaseServerIP = ""; //Database IP address
            public int DatabaseServerPort = 3306; //Usually defaults to 3306
            public string DatabaseUserName = ""; //Username of SQL user
            public string DatabasePassword = ""; //Password of SQL user
            public string DatabaseName = ""; //Database Name
            public string TableName = "";
            public string UnbanTableName = "";

            public bool PushedAllLocalBansToDatabase = false;

            public Dictionary<ulong,PlayerBan> databaseBans { get; set; } = new Dictionary<ulong, PlayerBan>();
        }

        public class PlayerBan
        {
            public ulong SteamID { get; set; } = 0;
            public string Username { get; set; } = "";
            public string Reason { get; set; } = "";

            public PlayerBan(ulong steamid, string reason, string username = "")
            {
                SteamID = steamid;
                Reason = reason;
                if (username == "")
                {
                    GetUsername();
                }
            }

            public PlayerBan(ServerUsers.User user)
            {
                SteamID = user.steamid;
                Username = user.username;
                Reason = user.notes;
            }

            public PlayerBan()
            {

            }

            public void GetUsername()
            {
                
                _plugin.webrequest.EnqueueGet(string.Format(SteamAPI_PastUsernameFormat, SteamID), (code, response) =>
                {
                    if (code == 200)
                    {
                        List<SteamAlias_Ajax> lastNames;
                        try
                        {
                            lastNames = JsonConvert.DeserializeObject<List<SteamAlias_Ajax>>(response);
                        }
                        catch
                        {
                            //_plugin.Puts("Failed to parse JSON GetUsername()");
                            Username = "Invalid_SteamID";
                            return;
                        }
                        if (lastNames.Count == 0)
                        {
                            //_plugin.Puts($"GetUsername() returned no usernames for {SteamID}!");
                            Username = "No_Aliases";
                            return;
                        }
                        Username = lastNames.First().newname;
                        var user = ServerUsers.Get(SteamID);
                        if (user != null)
                        {
                            user.username = Username;
                        }
                    }
                    else
                    {
                        _plugin.Puts($"GetUsername() error code: {code}");
                    }
                }, _plugin, null, 60);
            }
        }

        private void SyncBans()
        {
            if (!Settings.Instance.PushedAllLocalBansToDatabase)
            {
                Puts($"Please use \"pushbans\" to sync the database with this server.");
                return;
            }

            PullBansFromServer();
        }

        private void PushBansToServer()
        {
            var bans = new List<PlayerBan>(BansToSend);
            bans.AddRange(UnnamedBans.Where(x => UsernameValid(x)));
            UnnamedBans.RemoveAll(x => UsernameValid(x));
            bans.AddRange(ServerUsers.GetAll(ServerUsers.UserGroup.Banned).Where(x => !Settings.Instance.databaseBans.ContainsKey(x.steamid)).Select(x => new PlayerBan(x)));
            if (bans.Count() <= 0)
            {
                Puts("All bans have been synced with the database.");
                return;
            }
            var query = string.Format(MySqlConstants.ADD_BAN_TO_DATABASE, Settings.Instance.DatabaseName, Settings.Instance.TableName,
                string.Join(",", bans.Select(x => $"('{x.SteamID}','{x.Username}','{x.Reason.Replace(";", "").Replace("'", "")}')").ToArray())
            );
            Puts(query);
            try
            {
                PerformDatabaseQuery(query, (responseRows) =>
                {
                    Settings.Instance.PushedAllLocalBansToDatabase = true;
                    Puts($"Bans pushed to the database");
                });
            }
            catch
            {
                Puts("PushBansToServer() SQL Query failed!");
                Puts(query);
                timer.In(10f, ()=>
                {
                    PushBansToServer();
                });
            }
            Puts($"Started to send {bans.Count()} bans to database!");
        }

        private void UpdateDatabaseUsernames()
        {
            var usersToSend = UnnamedBans.Where(x => UsernameValid(x));
            UnnamedBans.RemoveAll(x => UsernameValid(x));
            if (usersToSend.Count() <= 0)
            {
                Puts("UpdateDatabaseUsernames() no users ready to update!");
                return;
            }
            var query = string.Format(MySqlConstants.ADD_BAN_TO_DATABASE, Settings.Instance.DatabaseName, Settings.Instance.TableName,
                string.Join(",", usersToSend.Select(x => $"('{x.SteamID}','{x.Username}','{x.Reason.Replace(";", "").Replace("'", "")}')").ToArray())
            );
            try
            {
                PerformDatabaseQuery(query, (responseRows) =>
                {

                });
            }
            catch
            {
                Puts("PushBansToServer() SQL Query failed!");
                Puts(query);
                timer.In(10f, UpdateDatabaseUsernames);
            }
            Puts($"Updated {usersToSend.Count()} usernames in the database!");
        }

        private void PullBansFromServer()
        {
            var query = string.Format(MySqlConstants.SELECT_ALL_BANS, Settings.Instance.DatabaseName, Settings.Instance.TableName);

            PerformDatabaseQuery(query, (responseRows) =>
            {
                if (responseRows == null)
                {
                    return;
                }

                HashSet<ulong> localBans = new HashSet<ulong>(ServerUsers.GetAll(ServerUsers.UserGroup.Banned).Select(x => x.steamid));

                Settings.Instance.databaseBans.Clear();

                if (responseRows.Any())
                {
                    //Add all database bans to local file
                    foreach (var row in responseRows)
                    {
                        var playerID = row["steamid"].ToString();

                        if (string.IsNullOrEmpty(playerID))
                        {
                            continue;
                        }

                        ulong steamID;
                        if (!ulong.TryParse(playerID, out steamID))
                        {
                            return;
                        }

                        var username = row["name"].ToString();
                        var reason = row["reason"].ToString();

                        Settings.Instance.databaseBans.Add(steamID, new PlayerBan(steamID, reason));

                        if (!UsernameValid(steamID, username))
                        {
                            UnnamedBans.Add(new PlayerBan(steamID, reason));
                        }

                        if (localBans.Remove(steamID)) //No need to reban people
                        {
                            continue;
                        }

                        ServerUsers.Set(steamID, ServerUsers.UserGroup.Banned, playerID, reason);

                        BasePlayer player = BasePlayer.FindByID(steamID);

                        if (player != null)
                        {
                            player.Kick("Bans share across servers :)");
                        }
                    }

                    //Dont want to delete local ban list accidently
                    foreach (var player in localBans) //Unban users who aren't found on the database
                    {
                        ServerUsers.Remove(player);
                    }

                    if (UnnamedBans.Count > 0)
                    {
                        timer.In(10f, UpdateDatabaseUsernames);
                    }

                    Puts($"Updated ban list - {responseRows.Count} bans pulled from database");

                    Settings.Save();
                }
            });
        }

        private void Unban(ConsoleSystem.Arg args)
        {
            ulong steamID = args.GetUInt64(0, 0uL);
            if (steamID < 70000000000000000uL)
            {
                return;
            }
            var query = string.Format(MySqlConstants.REMOVE_BAN, Settings.Instance.DatabaseName, Settings.Instance.TableName, steamID);

            PerformDatabaseQuery(query, (responseRows) =>
            {

            });
        }

        private void BanID(ConsoleSystem.Arg args)
        {
            ulong steamID = args.GetUInt64(0, 0uL);
            string name = args.GetString(1, "unnamed");
            string reason = args.GetString(2, "no reason");

            if (steamID == 0)
            {
                return;
            }

            var user = ServerUsers.Get(steamID);
            if (user != null)
            {
                if (UsernameValid(steamID, name))
                {
                    name = user.username;
                }
            }
            user.group = ServerUsers.UserGroup.Banned;

            if (Settings.Instance.PushedAllLocalBansToDatabase)
            {
                PushBansToServer();
            }
        }

        private void Ban(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.GetPlayer(0);
            if (player == null || player.net == null)
            {
                return;
            }

            string name = args.GetString(1, "unnamed");
            string reason = args.GetString(2, "no reason");

            var user = ServerUsers.Get(player.userID);
            if (user != null)
            {
                if (UsernameValid(player.userID, name))
                {
                    name = user.username;
                }
            }
            user.group = ServerUsers.UserGroup.Banned;

            if (Settings.Instance.PushedAllLocalBansToDatabase)
            {
                PushBansToServer();
            }
        }

        private bool UsernameValid(ulong steamID, string name)
        {
            return !(string.IsNullOrEmpty(name) || name.ToLower() == "unnamed" || name == steamID.ToString());
        }

        private bool UsernameValid(PlayerBan ban)
        {
            return UsernameValid(ban.SteamID, ban.Username);
        }

        private bool UsernameValid(ServerUsers.User user)
        {
            return UsernameValid(user.steamid, user.username);
        }

        [ConsoleCommand("pushbans")]
        void PushBansCmd(ConsoleSystem.Arg args)
        {
            if (!args.IsAdmin)
            {
                return;
            }
            PushBansToServer();
            Settings.Instance.PushedAllLocalBansToDatabase = true;
            _syncTimer?.Destroy();
            _syncTimer = timer.Every(60f, SyncBans);
            Initialized = true;
        }

        //MySQL stuff in C# by audi / i_love_code
        #region This stuff should not be edited. This is the guts.

        private readonly Core.MySql.Libraries.MySql _mySql = Interface.GetMod().GetLibrary<Core.MySql.Libraries.MySql>();

        private void PerformDatabaseQuery(string dbStatement, Action<List<Dictionary<string, object>>> callbackAction,
            Connection optionalConnection = null, params object[] sqlQueryArguments)
        {
            var sqlConnection = optionalConnection ?? GetSqlConnection();

            var query = _mySql.NewSql().Append(dbStatement, sqlQueryArguments);

            _mySql.Query(query, sqlConnection, callbackAction);

            CloseConnection(sqlConnection);
        }

        private void PerformDatabaseInsert(string dbStatement, Action<int> callbackAction,
            Connection optionalConnection = null, params object[] sqlQueryArguments)
        {
            var sqlConnection = optionalConnection ?? GetSqlConnection();

            var query = _mySql.NewSql().Append(dbStatement, sqlQueryArguments);

            _mySql.Insert(query, sqlConnection, callbackAction);

            CloseConnection(sqlConnection);
        }

        private void PerformDatabaseUpdate(string dbStatement, Action<int> callbackAction,
            Connection optionalConnection = null, params object[] sqlQueryArguments)
        {
            var sqlConnection = optionalConnection ?? GetSqlConnection();

            var query = _mySql.NewSql().Append(dbStatement, sqlQueryArguments);

            _mySql.Update(query, sqlConnection, callbackAction);

            CloseConnection(sqlConnection);
        }

        private void CloseConnection(Connection sqlConnection)
        {
            _mySql.CloseDb(sqlConnection);
        }

        private Connection GetSqlConnection()
        {
            return _mySql.OpenDb(Settings.Instance.DatabaseServerIP, Settings.Instance.DatabaseServerPort,
                Settings.Instance.DatabaseName, Settings.Instance.DatabaseUserName, Settings.Instance.DatabasePassword,
                this);
        }

        #endregion
    }
}