using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using UnityEngine;
using Network;
using System.Reflection;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("RustGuard", "i_love_code / Jake_Rich", "1.0.0")] //MAKE SURE VERSION NUMBER IS INCREMENTED WHEN CHANGING PLUGIN! (Keep at 1.0.0 for now)
    [Description("Custom Anti-Cheat")]
    public class RustGuard : RustPlugin
    {
        public ServerUpdate CurrentUpdate;

        public static RustGuard _plugin { get; set; }
        private Timer _updateTimer { get; set; }

        private string _isWorking { get; set; } = "ok";

        public string GetStatus()
        {
            return _isWorking;
        }

        void Init()
        {
            _plugin = this;
        }

        #region Load / Unload

        public Action<Message> networkMessageHook;
        public Action<Message> originalNetworkHook;

        void OnServerInitialized()
        {
            try //I really don't like these try catch, but they should notify if there is an error after updating when reloading plugin
            {   //Perhaps I can intercept error messages
                originalNetworkHook = Net.sv.onMessage;
                networkMessageHook = new Action<Message>(OnNetworkMessage); // Null reference before this could break server networking
                Net.sv.onMessage = networkMessageHook; // DONT PUT ANYTHING BEFORE THIS
            } catch { _isWorking = "OnNetworkMessage() override failed!"; }

            try
            {
                LoadConfigVariables();
            } catch { _isWorking = "Failed to load configuration variables!"; }

            try
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    OnPlayerInit(player);
                }
            } catch { _isWorking = "Failed to call OnPlayerInit() at startup!"; }

            try
            {
                InitServerUpdate();
                SendServerUpdate();
                _updateTimer = timer.Every(30f, SendServerUpdate);
            } catch { _isWorking = "Failed to send initial server update!"; }

        }

        void Unload()
        {
            Net.sv.onMessage = originalNetworkHook; //Also DONT PUT ANYTHING BEFORE THIS

            _updateTimer?.Destroy();

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerDisconnected(player, "PluginReload");
            }
        }

        #endregion

        #region Hooks

        public void OnNetworkMessage(Message msg)
        {
            if (msg.type != Message.Type.ConsoleCommand)
            {
                originalNetworkHook(msg);
                return;
            }
            string text = msg.read.String();

            var playerUpdate = GetPlayerUpdate(msg.Player());
            
            playerUpdate.CommandsExecuted.Add(BuildCommandAction(text));

            if (msg.connection == null || !msg.connection.connected)
            {
                Debug.LogWarning("Client without connection tried to run command: " + text);
                return;
            }
            string text2 = ConsoleSystem.Run(ConsoleSystem.Option.Server.FromConnection(msg.connection).Quiet(), text, new object[0]);
            if (!string.IsNullOrEmpty(text2))
            {
                if (!Net.sv.IsConnected())
                {
                    return;
                }
                Net.sv.write.Start();
                Net.sv.write.PacketID(Message.Type.ConsoleMessage);
                Net.sv.write.String(text2);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            var playerUpdate = GetPlayerUpdate(player);

            playerUpdate.ConnectEvents.Add(BuildConnectAction(player));
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var playerUpdate = GetPlayerUpdate(player);

            playerUpdate.DisconnectEvents.Add(BuildDisconnectAction(player, reason));
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null)
            {
                return;
            }

            if (info.Weapon is BaseMelee)
            {
                GetPlayerData(attacker).OnProjectileHit(info, true);
                return;
            }

            if (!(info.HitEntity is BasePlayer))
            {
                return;
            }

            GetPlayerData(attacker).OnProjectileHit(info, false);
        }

        void OnWeaponFired(BaseProjectile weapon, BasePlayer player, ItemModProjectile projectile, ProjectileShoot projectileShoot)
        {
            GetPlayerData(player).OnWeaponFired(weapon, player, projectile, projectileShoot);
        }

        void OnPlayerInput(BasePlayer player, InputState state)
        {
            //GetPlayerUpdate(player).ticks.Add(BuildTickAction(player, state));
        }

        #endregion

        #region Config (Oxide Template Config)    

        class ConfigData
        {
            public string ApiAppId;
            public string ApiAppSecret;
        }

        private ConfigData configData;

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        private HashSet<string> blockedServerCommands = new HashSet<string>()
        {
            "ownerid",
            "moderatorid",
            "give",
            "del",
        };

        #region Server Reply

        public string GetAssemblyHash()
        {
            return (string)ServerMgr.Instance.GetType().GetProperty("AssemblyHash", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(ServerMgr.Instance, null);
        }
        
        public void OnServerReply(ServerReply reply)
        {
            foreach (var cmd in reply.commands)
            {
                OnCommand(cmd);
            }
            /*
            foreach (var action in reply.actions)
            {
                HandleAction(action);
            }*/
        }

        public void OnCommand(ClientCommand cmd)
        {
            if (cmd.userID == 0)
            {
                string command = cmd.data.LoadString();
                if (blockedServerCommands.Contains(command.Split(' ')[0]))
                {
                    return;
                }
                rust.RunServerCommand(command);
                Log("RustGuard-RemoteCommands.log", command);
                return;
            }
            BasePlayer player = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == cmd.userID); //There must be a faster way to lookup players
            if (player == null)
            {
                Puts("Warning! Player not found!");
                return;
            }
            if (player?.net?.connection == null)
            {
                return;
            }
            if (Net.sv.write.Start())
            {
                Net.sv.write.Write(cmd.data.LoadBytes(), 0, cmd.data.Length());
                Net.sv.write.Send(new SendInfo(player.net.connection));
            }
        }
        
        #endregion
   
        #region Grouping Up Attacks And Ticks

        public Dictionary<BasePlayer, PlayerData> playerData { get; set; } = new Dictionary<BasePlayer, PlayerData>();

        public Dictionary<ulong, PlayerUpdate> playerUpdates { get; set; } = new Dictionary<ulong, PlayerUpdate>();

        public class PlayerData
        {
            public ulong playerID;
            private Dictionary<int, PlayerAttackAction> projectiles = new Dictionary<int, PlayerAttackAction>();
            private List<PlayerAttackAction> finishedAttacks = new List<PlayerAttackAction>();

            public PlayerData(BasePlayer player)
            {
                playerID = player.userID;
            }

            public void OnProjectileHit(HitInfo info, bool melee)
            {
                PlayerAttackAction projectile;
                if (!melee)
                {
                    if (!projectiles.TryGetValue(info.ProjectileID, out projectile))
                    {
                        return;
                    }
                }
                else
                {
                    projectile = new PlayerAttackAction();
                }
                projectile.hit = true;
                projectile.hitBone = info.HitBone;
                if (info.HitEntity is BasePlayer)
                {
                    projectile.hitArea = (int)info.boneArea;
                }
                projectile.hitPosition = info.HitPositionWorld;
                projectile.distance = info.ProjectileDistance;
                projectile.projectileID = info.ProjectileID;
                projectile.weaponShortname = info.Weapon.ShortPrefabName;
                if (info.Weapon is BaseMelee)
                {
                    projectile.startPosition = info.PointStart;
                    projectile.timeStamp = DateTime.UtcNow;
                    projectile.attackType = PlayerAttackAction.AttackType.Melee;
                }
                else if (info.Weapon is BaseProjectile)
                {
                    projectile.attackType = PlayerAttackAction.AttackType.Projectile;
                }
                finishedAttacks.Add(projectile);
            }

            public void OnWeaponFired(BaseProjectile weapon, BasePlayer player, ItemModProjectile projectile, ProjectileShoot projectileShoot)
            {
                foreach (var shoot in projectileShoot.projectiles)
                {
                    PlayerAttackAction action = new PlayerAttackAction() //Hate to have constructor here, but it's easiest way
                    {
                        startPosition = shoot.startPos,
                        timeStamp = DateTime.UtcNow,
                        weaponShortname = weapon.ShortPrefabName,
                        xRecoil = weapon.recoil.recoilYawMax - weapon.recoil.recoilYawMin,
                        yRecoil = weapon.recoil.recoilPitchMax - weapon.recoil.recoilPitchMin,
                        aimcone = weapon.aimCone,
                        aimAngles = (Vector2)player.serverInput.current.aimAngles,
                    };
                    projectiles.Add(shoot.projectileID, action);
                }
            }

            public List<PlayerAttackAction> GetAttacks()
            {
                var returnVar = finishedAttacks;
                finishedAttacks = new List<PlayerAttackAction>();
                return returnVar;
            }
        }

        public PlayerData GetPlayerData(BasePlayer player)
        {
            PlayerData data;
            if (!playerData.TryGetValue(player, out data))
            {
                data = new PlayerData(player);
                playerData.Add(player, data);
            }
            return data;
        }

        public PlayerUpdate GetPlayerUpdate(BasePlayer player)
        {
            PlayerUpdate data;
            if (!playerUpdates.TryGetValue(player.userID, out data))
            {
                data = new PlayerUpdate()
                {
                    AuthLevel = player.net.connection.authLevel,
                    DisplayName = player.displayName,
                    SteamId = player.UserIDString
                };

                playerUpdates.Add(player.userID, data);
            }
            return data;
        }

        #endregion

        #region Build Shared Classes

        //Keep all the building of actions in plugin

        public PlayerStringAction BuildCommandAction(string cmd)
        {
            var action = new PlayerStringAction()
            {
                text = cmd,
                timeStamp = DateTime.UtcNow,
            };

            return action;
        }

        public PlayerDisconnectAction BuildDisconnectAction(BasePlayer player, string reason)
        {
            var action = new PlayerDisconnectAction()
            {
                reason = reason,
                timeStamp = DateTime.UtcNow,
                userID = player.UserIDString,
            };
            return action;
        }

        public PlayerConnectAction BuildConnectAction(BasePlayer player)
        {
            var split = player.net.connection.ipaddress.Split(':');
            var action = new PlayerConnectAction()
            {
                authLevel = player.net.connection.authLevel,
                displayName = player.displayName,
                IPAddress = split[0],
                port = split[1],
                timeStamp = DateTime.UtcNow,
                userID = player.UserIDString,
            };
            return action;
        }

        public PlayerSavedTick BuildTickAction(BasePlayer player, InputState state)
        {
            PlayerSavedTick action = new PlayerSavedTick()
            {
                aimAngles = (Vector2)state.current.aimAngles,
                position = player.transform.position,
                timeStamp = DateTime.UtcNow,

            };
            return action;
        }

        #endregion

        #region Shared Classes (stored in plugin)

        public class ServerReply
        {
            public List<ClientCommand> commands { get; set; } = new List<ClientCommand>();
        }

        public class ClientCommand
        {
            public ulong userID { get; set; } = 0;
            public BaseDataClass data { get; set; }

            public ClientCommand()
            {

            }

            public ClientCommand(string command)
            {
                userID = 0;
                data = new BaseDataClass();
                data.StoreString(command);
            }
        }

        public class BaseDataClass
        {
            public string data { get; set; }

            public int Length() { return data.Length; }

            public void StoreString(string str)
            {
                data = str;
            }

            public void StoreBytes(byte[] bytes)
            {
                data = Encoding.UTF8.GetString(bytes);
            }

            public string LoadString()
            {
                return data;
            }

            public byte[] LoadBytes()
            {
                return Encoding.UTF8.GetBytes(data);
            }
        }

        public class PlayerAction
        {
            public DateTime timeStamp;
        }

        public class PlayerAttackAction : PlayerAction
        {
            public bool hit { get; set; } = false;
            public ulong victim { get; set; }
            public uint hitBone { get; set; }
            public int hitArea { get; set; }
            public SerializableVector3 hitPosition { get; set; }
            public float distance { get; set; }
            public int projectileID { get; set; }
            public string weaponShortname { get; set; }
            public SerializableVector2 aimAngles { get; set; }
            public SerializableVector3 startPosition { get; set; }
            public float xRecoil { get; set; }
            public float yRecoil { get; set; }
            public float aimcone { get; set; }
            public AttackType attackType { get; set; }

            public PlayerAttackAction()
            {

            }

            public enum AttackType
            {
                None = 0,
                Melee = 1,
                Projectile = 2,
                Throw = 3,
            }
        }

        public class PlayerStringAction : PlayerAction
        {
            public string text { get; set; }
        }

        public class PlayerConnectAction : PlayerServerAction
        {
            public string displayName { get; set; }
            public string IPAddress { get; set; }
            public string port { get; set; }
            public int authLevel { get; set; }
        }

        public class PlayerDisconnectAction : PlayerServerAction
        {
            public string reason;
        }

        public class PlayerSavedTick : PlayerAction
        {
            public SerializableVector2 aimAngles { get; set; }
            public SerializableVector3 position { get; set; }
        }

        public class PlayerServerAction : PlayerAction
        {
            public string userID { get; set; }
        }

        public class PlayerUpdate
        {
            public string SteamId { get; set; }
            public int AuthLevel { get; set; }
            public string DisplayName { get; set; }

            public List<PlayerConnectAction> ConnectEvents { get; set; } = new List<PlayerConnectAction>();

            public List<PlayerDisconnectAction> DisconnectEvents { get; set; } = new List<PlayerDisconnectAction>();

            public List<PlayerAttackAction> Attacks { get; set; } = new List<PlayerAttackAction>();

            public List<PlayerSavedTick> Ticks { get; set; } = new List<PlayerSavedTick>();

            public List<PlayerStringAction> CommandsExecuted { get; set; } = new List<PlayerStringAction>();
        }

        public struct SerializableVector3
        {
            /// <summary>
            /// x component
            /// </summary>
            public float x;

            /// <summary>
            /// y component
            /// </summary>
            public float y;

            /// <summary>
            /// z component
            /// </summary>
            public float z;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="rX"></param>
            /// <param name="rY"></param>
            /// <param name="rZ"></param>
            public SerializableVector3(float rX, float rY, float rZ)
            {
                x = rX;
                y = rY;
                z = rZ;
            }

            /// <summary>
            /// Returns a string representation of the object
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return String.Format("[{0}, {1}, {2}]", x, y, z);
            }

            /// <summary>
            /// Automatic conversion from Vector3 to SerializableVector3
            /// </summary>
            /// <param name="rValue"></param>
            /// <returns></returns>
            public static implicit operator SerializableVector3(Vector3 rValue)
            {
                return new SerializableVector3(rValue.x, rValue.y, rValue.z);
            }
        }

        public struct SerializableVector2
        {
            /// <summary>
            /// x component
            /// </summary>
            public float x;

            /// <summary>
            /// y component
            /// </summary>
            public float y;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="rX"></param>
            /// <param name="rY"></param>
            /// <param name="rZ"></param>
            public SerializableVector2(float rX, float rY)
            {
                x = rX;
                y = rY;
            }

            /// <summary>
            /// Returns a string representation of the object
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return String.Format("[{0}, {1}]", x, y);
            }

            /// <summary>
            /// Automatic conversion from Vector3 to SerializableVector3
            /// </summary>
            /// <param name="rValue"></param>
            /// <returns></returns>
            public static implicit operator SerializableVector2(Vector2 rValue)
            {
                return new SerializableVector2(rValue.x, rValue.y);
            }
        }

        public class ServerUpdate
        {
            public string ServerIp { get; set; }
            public string ServerPort { get; set; }
            public string ServerName { get; set; }
            public DateTime TimeStamp { get; set; }

            public List<PlayerUpdate> PlayerUpdates { get; set; } = new List<PlayerUpdate>();

            public string Version { get; set; }
        }

        #endregion

        private void SendServerUpdate()
        {
            try
            {
                ServerUpdate update = CurrentUpdate;
                /*
                update.PlayerUpdates.Add(
                new PlayerUpdate()
                {
                    ConnectEvents = new List<PlayerConnectAction>()
                    {
                    new PlayerConnectAction()
                    {
                        userID = "76561198104673895",
                        IPAddress = "127.0.0.1",
                        timeStamp = DateTime.UtcNow,
                        displayName = "test",
                        authLevel = 0,
                        port = "1234",
                    }
                    },
                    SteamId = "76561198104673895",
                    AuthLevel = 0,
                    DisplayName = "test",
                });*/

                PreSendUpdate(update);

                var jsonBody = JsonConvert.SerializeObject(update);
                Puts($"Trying to send update to the server");
                Puts($"{jsonBody}");
                SendAuthenticatedPostRequest("Server", jsonBody, (responseCode, response) =>
                {
                    if (responseCode != 200)
                    {
                        Puts($"ServerResponse ERROR: POST Response from server: {responseCode} - {System.Text.RegularExpressions.Regex.Unescape(response)}");
                        return;
                    }
                    else
                    {
                    //Puts($"ServerUpdate POST Response from server: {responseCode} - {response}");
                }
                    ServerReply reply;
                    try
                    {
                        reply = JsonConvert.DeserializeObject<ServerReply>(response);
                    }
                    catch
                    {
                        Puts($"Failed to parse server reply: {response}");
                        return;
                    }
                    Puts($"Recieved update from server");
                    OnServerReply(reply);
                });

                ResetUpdate();
            }
            catch (Exception ex)
            {
                Puts($"Failed To Send Server Update!\n{ex}");
            }
        }

        private void ResetUpdate()
        {
            playerUpdates.Clear();
            InitServerUpdate();
        }

        private void InitServerUpdate()
        {
            CurrentUpdate = new ServerUpdate()
            {
                ServerIp = "127.0.0.1",
                //ServerIp = ConVar.Server.ip,
                ServerPort = ConVar.Server.port.ToString(),
                ServerName = ConVar.Server.hostname,
                Version = Version.ToString(),
            };
        }

        private void PreSendUpdate(ServerUpdate update)
        {
            foreach(var data in playerData)
            {
                try
                {
                    GetPlayerUpdate(data.Key).Attacks = data.Value.GetAttacks();
                }
                catch { }
            }

            update.PlayerUpdates = playerUpdates.Values.ToList();
            update.TimeStamp = DateTime.UtcNow;
        }

        #region Logging

        void Log(string filename, string text, bool logToConsole = true)
        {
            LogToFile(filename, $"[{DateTime.Now}] {text}", this);
            if (logToConsole) Puts(text);
        }

        #endregion

        #region Network Calls

        private const string WebAppRemoteUrl = "http://www.rustgameguard.com/api";
        //private const string WebAppRemoteUrl = "http://localhost:63264/api";

        private void SendAuthenticatedGetRequest(string relativeUrl, Action<int, string> callback)
        {
            var requestUri = $"{WebAppRemoteUrl}/{relativeUrl}";
            var headers = GetHeadersFor(requestUri, "GET");
            webrequest.EnqueueGet(requestUri, callback, this, headers);
        }

        private void SendAuthenticatedPostRequest(string relativeUrl, string body, Action<int, string> callback)
        {
            var requestUri = $"{WebAppRemoteUrl}/{relativeUrl}";
            var headers = GetHeadersFor(requestUri, "POST", body);
            headers.Add("Content-Type", "application/json");
            webrequest.EnqueuePost(requestUri, body, callback, this, headers);
        }

        private Dictionary<string, string> GetHeadersFor(string requestUri, string method, string body = null)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();

            string requestTimestamp = ((ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds).ToString();

            string requestContentBase64String = string.Empty;
            //create random nonce for each request
            string nonce = Guid.NewGuid().ToString("N");
            if (body != null)
            {
                byte[] content = UTF8Encoding.UTF8.GetBytes(body);
                MD5 md5 = MD5.Create();
                //Hashing the request body, any change in request body will result in different hash, we'll incure message integrity
                byte[] requestContentHash = md5.ComputeHash(content);
                requestContentBase64String = Convert.ToBase64String(requestContentHash);
            }
            string signatureRawData = String.Format("{0}{1}{2}{3}{4}{5}", configData.ApiAppId, method, requestUri.ToLower(), requestTimestamp, nonce, requestContentBase64String);
            var secretKeyByteArray = Convert.FromBase64String(configData.ApiAppSecret);
            byte[] signature = Encoding.UTF8.GetBytes(signatureRawData);
            using (HMACSHA256 hmac = new HMACSHA256(secretKeyByteArray))
            {
                byte[] signatureBytes = hmac.ComputeHash(signature);
                string requestSignatureBase64String = Convert.ToBase64String(signatureBytes);

                var headerValue = $"amx {configData.ApiAppId}:{requestSignatureBase64String}:{nonce}:{requestTimestamp}";
                
                //Setting the values in the Authorization header using custom scheme (amx)
                headers.Add("Authorization", headerValue);
            }
            return headers;
        }
        
        #endregion

        #region Testing

        public void TestCases()
        {

        }

        #endregion
    }
}