using Network;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GyroHeli", "Jake_Rich", 0.2)]
    [Description("Helis bby!")]
    public class GyroHeli : RustPlugin
    {
        public static GyroHeli _plugin;

        public static UILabel DebugLabel { get; set; }

        private Timer _debugLabelTimer { get; set; }

        private Vector3 originalPos { get; set; }

        void Init()
        {
            _plugin = this;
            DebugLabel = new UILabel(new Vector2(0.4f, 0.85f), new Vector2(0.6f, 1f), "No Heli Found", 12, fontColor: "0 0 0 1");
            DebugLabel.variableText = delegate (BasePlayer player)
            {
                if (testHeli == null)
                {
                    return "No Heli found";
                }
                return testHeli.ReturnDebugLabelDisplay();
            };
            DebugLabel.Show(BasePlayer.activePlayerList);
            _debugLabelTimer = timer.Every(0.05f, UpdateLoop);
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var parent = entity.GetParentEntity();
            if (parent == null)
            {
                return;
            }
            if (parent.GetComponent<Heli>() != null)
            {
                Puts($"Shot heli!");
                info.damageTypes.ScaleAll(5f);
                Effect.server.Run("assets/bundled/prefabs/fx/impacts/bullet/metal/metal1.prefab", info.HitPositionWorld, info.HitNormalWorld, null);
            }
        }

        void UpdateLoop()
        {
            DebugLabel.RefreshAll();
        }

        void Unload()
        {
            DebugLabel.HideAll();
            _debugLabelTimer?.Destroy();
            foreach(var heli in GameObject.FindObjectsOfType<Heli>())
            {
                GameObject.Destroy(heli.gameObject);
            }
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            Puts($"OnLootEntity() {entity.GetType()}");
        }

        void CanUseLockedEntity()
        {
            Puts($"CanUseLockedEntity()");
        }

        object CanUnlock(CodeLock codeLock, BasePlayer player)
        {
            Heli heli = codeLock.parentEntity.Get(true)?.GetComponent<Heli>();
            if (heli == null)
            {
                return null;
            }
            heli.OnCodelockPressed(codeLock, true);
            Puts($"CanUnlock()");
            return true;
        }

        object CanLock(CodeLock codeLock, BasePlayer player)
        {
            Heli heli = codeLock.parentEntity.Get(true)?.GetComponent<Heli>();
            if (heli == null)
            {
                return null;
            }
            heli.OnCodelockPressed(codeLock, false);
            Puts($"CanLock()");
            return true;
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (testHeli != null && playerMoveDebug)
            {
                testHeli.OnInput(input);
            }
        }

        Heli testHeli;

        private Dictionary<BasePlayer, PlayerData> _playerData = new Dictionary<BasePlayer, PlayerData>();

        public class PlayerData
        {
            private Heli _heli { get; set; }

            public void OnPlayerInput(InputState input)
            {
                if (_heli == null)
                {
                    return;
                }
                _heli.OnInput(input);
            }
        }

        //I actually want to simulate tair rotor, main rotor physics, etc now
        //That way if the rotor gets shot out, then the heli will go down spectatularily

        object CanUseLockedEntity(BasePlayer player, CodeLock codeLock)
        {
            return true;
        }

        //TODO: Serialize (contents of cargo, position of heli)
        public class Heli : LookingAtTrigger
        {
            public BasePlayer _pilot { get; set; }
            private Timer _soundTimer { get; set; }
            public BaseEntity _centerEntity { get; set; }


            public class HeliButton
            {
                public CodeLock Lock { get; set; }
                private Action<bool> _callback { get; set; }
                private List<MoveableDoor> _doors { get; set; } = new List<MoveableDoor>();
                public string Sprite { get; set; }
                public string Text { get; set; }
                

                public HeliButton()
                {

                }

                public HeliButton(Vector3 position, Action<bool> callback, string sprite, string text, bool defaultLocked = true)
                {
                    Lock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", position, Quaternion.Euler(0, 90f, 90f)) as CodeLock;
                    Lock.code = "12345"; //Good luck getting a 5 character code!
                    Lock.guestCode = "12345";
                    Lock.SetFlag(BaseEntity.Flags.Locked, defaultLocked);
                    Text = text;
                    Sprite = sprite;

                    _callback = callback;
                }

                public void OnPressed(bool state)
                {
                    _callback.Invoke(state);
                    Lock.SetFlag(BaseEntity.Flags.Locked, !state);
                }

                public void AddDoor(MoveableDoor door)
                {
                    if (_doors.Contains(door))
                    {
                        return;
                    }
                    _doors.Add(door);
                }
            }

            public class MoveableDoor
            {
                public BaseEntity Entity { get; set; }
                private bool _targetState { get; set; }
                private float _percentChanged { get; set; }
                private Vector3 _pos1 { get; set; }
                private Vector3 _pos2 { get; set; }
                private Quaternion _rot1 { get; set; }
                private Quaternion _rot2 { get; set; }
                private float _openTime { get; set; }
                private float _closeTime { get; set; }

                public MoveableDoor(BaseEntity entity, Vector3 pos1, Vector3 pos2, Quaternion rot1, Quaternion rot2, float timeToOpen, float timeToClose)
                {
                    Entity = entity;
                    entity.transform.position = pos1;
                    entity.transform.rotation = rot1;
                    _pos1 = pos1;
                    _pos2 = pos2;
                    _rot1 = rot1;
                    _rot2 = rot2;
                    _openTime = timeToOpen;
                    _closeTime = timeToClose;
                }

                public void ChangeState(bool state)
                {
                    _targetState = state;
                }

                public void Update() //Use in fixed update, so we assume 0.1f update rate
                {
                    if (Entity == null)
                    {
                        return;
                    }
                    float amountToChange = _targetState ? (0.1f / _openTime) : (-0.1f / _closeTime);
                    _percentChanged += _targetState ? (0.1f / _openTime) : (-0.1f / _closeTime);
                    _percentChanged = Mathf.Clamp(_percentChanged, 0f, 1f);
                    Entity.transform.position = Vector3.Lerp(_pos1, _pos2, _percentChanged);
                    Entity.transform.rotation = Quaternion.Lerp(_rot1, _rot2, _percentChanged);
                }
            }

            #region Thrust control

            #region Variables
            private Rigidbody _rigidBody { get; set; }
            private float _weight { get; set; } = 1900f;
            private float _throttle { get; set; } = 0f;
            private float _pitch { get; set; }
            private float _yaw { get; set; }
            private float _roll { get; set; }

            private float maxRotorForce = 22241f;
            private float maxRotorVelocity = 7200f;
            private float RotorVelocity = 0f;
            private float RotorRotation = 0f;

            private float maxTailRotorForce = 15000f;
            private float maxTailRotorVelocity = 2200f;
            private float TailRotorVelocity = 0f;
            private float TailRotorRotation = 0f;

            private float forwardRotorTorqueMultiplier = 0.5f;
            private float sidewaysRotorTorqueMultipler = 0.5f;

            private bool MainRotorActive = true;
            private bool TailRotorActive = true;

            private float throttleIncrease = 0.05f;
            private float pitchIncrease = 0.1f;
            private float rollIncrease = 0.1f;
            private float yawIncrease = 0.1f;
            private float maxSpeed = 30f;

            private float pitchControlTorque = 0.3f;
            private float yawControlTorque = 0.45f;
            private float rollControlTorque = 0.05f;
            #endregion

            private void ThrustUpdate()
            {
                var hoverRotorVelocity = _rigidBody.mass * Mathf.Abs(Physics.gravity.y) / maxRotorForce;

                RotorVelocity += _throttle * 0.02f;
                RotorVelocity = Mathf.Clamp(RotorVelocity, 0f, 1f);

                if (_throttle == 0)
                {
                    RotorVelocity = Mathf.Lerp(RotorVelocity, hoverRotorVelocity, 0.02f);
                }

                var hoverTailRotorVelocity = maxRotorForce * RotorVelocity / maxTailRotorForce;

                TailRotorVelocity = hoverTailRotorVelocity;

                TailRotorVelocity += _yaw * yawControlTorque;

                Vector3 torque = Vector3.zero;

                Vector3 controlTorque = new Vector3(_pitch * pitchControlTorque, 1.0f, -_roll * rollControlTorque);
                if (MainRotorActive)
                {
                    torque += controlTorque * maxRotorForce * RotorVelocity;
                    _rigidBody.AddRelativeForce(Vector3.up * maxRotorForce * RotorVelocity); //Adds it in local coordinates, that makes so much sense
                }

                if (Vector3.Angle(Vector3.up, _centerEntity.transform.up) > 80f)
                {
                    _centerEntity.transform.rotation = Quaternion.Slerp(_centerEntity.transform.rotation,
                        Quaternion.Euler(0, _centerEntity.transform.rotation.eulerAngles.y, 0),
                        Time.fixedDeltaTime * RotorVelocity * 2);
                }

                if (TailRotorActive)
                {
                    torque -= (Vector3.up * maxTailRotorForce * TailRotorVelocity);
                }

                _rigidBody.AddRelativeTorque(torque);
            }

            public void OnInput(InputState input)
            {
                #region Throttle

                bool throttleChanged = false;

                if (input.IsDown(BUTTON.JUMP))
                {
                    _throttle += throttleIncrease;
                    _throttle = Mathf.Min(_throttle, 1f);
                    throttleChanged = true;
                }

                if (input.IsDown(BUTTON.SPRINT))
                {
                    _throttle -= throttleIncrease;
                    _throttle = Mathf.Max(_throttle, -1f);
                    throttleChanged = true;
                }

                if (!throttleChanged)
                {
                    _throttle *= 0.75f;
                    if (Mathf.Abs(_throttle) < 0.05f)
                    {
                        _throttle = 0f;
                    }
                }

                #endregion

                #region Roll

                bool rollChanged = false;

                if (input.IsDown(BUTTON.RIGHT))
                {
                    _roll += rollIncrease;
                    _roll = Mathf.Min(_roll, 1f);
                    rollChanged = true;
                }

                if (input.IsDown(BUTTON.LEFT))
                {
                    _roll -= rollIncrease;
                    _roll = Mathf.Max(_roll, -1f);
                    rollChanged = true;
                }

                if (!rollChanged)
                {
                    _roll *= 0.7f;
                    if (Mathf.Abs(_roll) < 0.05f)
                    {
                        _roll = 0f;
                    }
                }

                #endregion

                #region Pitch

                bool pitchChanged = false;

                if (input.IsDown(BUTTON.FORWARD))
                {
                    _pitch += pitchIncrease;
                    _pitch = Mathf.Min(_pitch, 1f);
                    pitchChanged = true;
                }

                if (input.IsDown(BUTTON.BACKWARD))
                {
                    _pitch -= pitchIncrease;
                    _pitch = Mathf.Max(_pitch, -1f);
                    pitchChanged = true;
                }

                if (!pitchChanged)
                {
                    _pitch *= 0.7f;
                    if (Mathf.Abs(_pitch) < 0.05f)
                    {
                        _pitch = 0f;
                    }
                }

                #endregion

                #region Yaw

                bool yawChanged = false;

                if (input.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    _yaw += yawIncrease;
                    _yaw = Mathf.Min(_yaw, 1f);
                    yawChanged = true;
                }

                if (input.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    _yaw -= yawIncrease;
                    _yaw = Mathf.Max(_yaw, -1f);
                    yawChanged = true;
                }

                if (!yawChanged)
                {
                    _yaw *= 0.7f;
                    if (Mathf.Abs(_yaw) < 0.05f)
                    {
                        _yaw = 0f;
                    }
                }
                #endregion
            }

            #endregion

            #region UI

            private UIBaseElement _iconPanel = new UIRawImage(new Vector2(0.40f, 0.40f), new Vector2(0.6f, 0.6f));

            private void SetupUI()
            {
                #region OpenIcon

                float dotSize = 0.007f;
                float fadeIn = 0.25f;

                UIImage crosshairDot = new UIImage(new Vector2(0.5f - dotSize, 0.5f - dotSize * 2), new Vector2(0.5f + dotSize, 0.5f + dotSize * 2), _iconPanel);
                crosshairDot.Image.Sprite = "assets/icons/circle_closed.png";

                UIImage openIconSprite = new UIImage(new Vector2(0.46f, 0.73f), new Vector2(0.54f, 0.90f), _iconPanel);
                openIconSprite.variableSprite = delegate (BasePlayer player)
                {
                    var lookEnt = GetLookingEntity(player);
                    if (lookEnt == null)
                    {
                        return "";
                    }
                    HeliButton button = _buttons.FirstOrDefault(x => x.Lock == lookEnt as CodeLock);
                    if (button == null)
                    {
                        return "";
                    }
                    return button.Sprite;
                };
                openIconSprite.Image.Sprite = "assets/icons/open.png";
                openIconSprite.Image.FadeIn = fadeIn;

                UILabel openIconText = new UILabel(new Vector2(0, 0.5f), new Vector2(1f, 0.8f), "", 14, parent: _iconPanel);
                openIconText.text.FadeIn = fadeIn;
                openIconText.variableText = delegate (BasePlayer player)
                {
                    var lookEnt = GetLookingEntity(player);
                    if (lookEnt == null)
                    {
                        return "";
                    }
                    HeliButton button = _buttons.FirstOrDefault(x => x.Lock == lookEnt as CodeLock);
                    if (button == null)
                    {
                        return "";
                    }
                    return button.Text;
                };

                #endregion
            }

            public override void OnPlayerLookAtEntity(BasePlayer player, BaseEntity entity)
            {
                base.OnPlayerLookAtEntity(player, entity);
                _plugin.Puts($"{player.displayName} looking at {entity.GetType().Name}");
                return;
                HeliButton button = _buttons.FirstOrDefault(x => x.Lock == entity);
                if (button == null)
                {
                    _iconPanel.Hide(player);
                    return;
                }
                _iconPanel.Show(player);
                
            }


            #endregion

            #region Hooks

            public override void Awake()
            {
                base.Awake();
                if (_players == null)
                {
                    _players = new List<BasePlayer>();
                }
                _players.AddRange(BasePlayer.activePlayerList);
            }

            private void OnDestroy()
            {
                StopSpectate();
                _soundTimer?.Destroy();
                //BasePlayer.activePlayerList.FirstOrDefault().SetParent(null);
                //BasePlayer.activePlayerList.FirstOrDefault().MovePosition(Vector3.zero);
                foreach (var part in _parts)
                {
                    if (!part.IsDestroyed)
                    {
                        part.Kill();
                    }
                }
                _centerEntity.Kill();
            }

            private void FixedUpdate()
            {
                ThrustUpdate();
                _centerEntity.SendNetworkUpdateImmediate();

                foreach (var door in _doors)
                {
                    door.Update();
                }

                //RearDoor = Euler(-110, 0 0)

                foreach (var part in _parts)
                {
                    part.SendNetworkUpdateImmediate();
                }

                var player = BasePlayer.activePlayerList.FirstOrDefault();
                if (player != null)
                {

                    if (playerMoveDebug)
                    {
                        Vector3 pos = _centerEntity.transform.position + _centerEntity.transform.TransformDirection(new Vector3(0, 0, 7f));
                        player.MovePosition(pos);
                        player.ClientRPCPlayer(null, player, "ForcePositionTo", pos, null, null, null, null);
                    }
                    else
                    {/*
                        if (lastPos != Vector3.zero)
                        {
                            Vector3 lastChange = _centerEntity.transform.position - lastPos;
                            combinedChange += lastChange;
                            if (combinedChange.magnitude > 2f)
                            {
                                player.transform.position += combinedChange;
                                player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position, null, null, null, null);
                                combinedChange = Vector3.zero;
                            }
                        }
                        lastPos = _centerEntity.transform.position;*/

                    }
                }

            }

            #endregion

            public void SetPosition(Vector3 pos)
            {
                _centerEntity.transform.position = pos;
                _centerEntity.SendNetworkUpdateImmediate();
            }

            #region Entities

            private List<BaseEntity> _parts { get; set; } = new List<BaseEntity>();
            private List<SupplyDrop> _cargo { get; set; } = new List<SupplyDrop>();//So many lists, could be compacted and optimized later
            private List<HeliButton> _buttons { get; set; } = new List<HeliButton>();
            private List<BaseEntity> _landingGear { get; set; } = new List<BaseEntity>();
            private List<MoveableDoor> _doors { get; set; } = new List<MoveableDoor>();

            public void SpawnEntities()
            {
                _plugin.Puts($"1");
                _centerEntity.syncPosition = true;
                _centerEntity.SetFlag(BaseEntity.Flags.Locked, true);

                _rigidBody = _centerEntity.gameObject.GetComponent<Rigidbody>();

                if (_rigidBody == null)
                {
                    _rigidBody = _centerEntity.gameObject.AddComponent<Rigidbody>();
                }

                _rigidBody.isKinematic = false;
                _rigidBody.useGravity = true;
                _rigidBody.mass = _weight;
                _rigidBody.drag = 0.3f;
                _rigidBody.angularDrag = 0.5f;
                _plugin.Puts($"2");
                string xlSign = "assets/prefabs/deployable/signs/sign.pictureframe.xxl.prefab";
                string mediumWoodenSign = "assets/prefabs/deployable/signs/sign.medium.wood.prefab";
                string smallWoodenSign = "assets/prefabs/deployable/signs/sign.small.wood.prefab";
                string chair = "assets/prefabs/deployable/chair/chair.deployed.prefab";

                /*
                for(int i = -2; i < 2; i++) //Right wall
                {
                    _parts.Add(GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.large.wood.prefab", new Vector3(1.5f, -1.0f, 3f * i), Quaternion.Euler(0f, 90f, 0f)));
                }

                for (int i = -2; i < 2; i++) //Left wall
                {
                    _parts.Add(GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.large.wood.prefab", new Vector3(-1.5f, -1.0f, 3f * i), Quaternion.Euler(0f, -90f, 0f)));
                }

                for (int x = 0; x < 2; x++) //Floor
                {
                    for(int z = -2; z<2; z++)
                    {
                        _parts.Add(GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.large.wood.prefab", new Vector3(x * 1.5f, -1f, z * 3f), Quaternion.Euler(90f, 0, 90f)));
                    }
                }*/
                _plugin.Puts($"3");
                float xlSignHeight = 2.8f;
                float xlSignWidth = 5.2f;

                //Right wall
                for (int i = -2; i < 2; i++)
                {
                    if (i == 0)
                    {
                        continue;
                    }
                    _parts.Add(GameManager.server.CreateEntity(xlSign, new Vector3(xlSignHeight, 0, xlSignWidth * i), Quaternion.Euler(0f, 90f, 0f)));
                }
                //Left wall
                for (int i = -2; i < 2; i++) //Left wall
                {
                    if (i == 0)
                    {
                        continue;
                    }
                    _parts.Add(GameManager.server.CreateEntity(xlSign, new Vector3(-xlSignHeight, 0, xlSignWidth * i), Quaternion.Euler(0f, -90f, 0f)));
                }

                for (int x = 0; x < 2; x++) //Floor
                {
                    for (int z = -2; z < 2; z++)
                    {
                        _parts.Add(GameManager.server.CreateEntity(xlSign, new Vector3(x * xlSignHeight, 0, z * xlSignWidth), Quaternion.Euler(90f, 0, 90f)));
                    }
                }
                _plugin.Puts($"4");
                _cargo.Add(GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", new Vector3(1.60f, 0, -11.6f)) as SupplyDrop);
                _cargo.Add(GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", new Vector3(-1.60f, 0, -11.6f)) as SupplyDrop);
                _cargo.Add(GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", new Vector3(1.60f, 0, -9.1f)) as SupplyDrop);
                _cargo.Add(GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", new Vector3(-1.60f, 0, -9.1f)) as SupplyDrop);
                _parts.AddRange(_cargo.Cast<BaseEntity>());

                _doors.Add(new MoveableDoor(GameManager.server.CreateEntity(xlSign),
                    new Vector3(0f, 0f, -13f), new Vector3(0, 0, -13f),
                    Quaternion.Euler(0f, 0f, 0f), Quaternion.Euler(-110f, 0f, 0f),
                    10f, 10f));

                _buttons.Add(new HeliButton(new Vector3(0.4f, 0f, 7.5f), ToggleRearDoor, "", "Test text"));
                _parts.AddRange(_buttons.Select(x => x.Lock as BaseEntity));

                //Landing gear
                _landingGear.Add(GameManager.server.CreateEntity("assets/bundled/prefabs/autospawn/resource/logs_dry/dead_log_c.prefab", new Vector3(-3f, -1.75f, 2f), Quaternion.Euler(new Vector3(0, 90, 0))));
                _landingGear.Add(GameManager.server.CreateEntity("assets/bundled/prefabs/autospawn/resource/logs_dry/dead_log_c.prefab", new Vector3(3f, -1.75f, 2f), Quaternion.Euler(new Vector3(0, 90, 0))));
                _landingGear.Add(GameManager.server.CreateEntity("assets/bundled/prefabs/autospawn/resource/logs_dry/dead_log_c.prefab", new Vector3(-3f, -1.75f, -8f), Quaternion.Euler(new Vector3(0, 90, 0))));
                _landingGear.Add(GameManager.server.CreateEntity("assets/bundled/prefabs/autospawn/resource/logs_dry/dead_log_c.prefab", new Vector3(3f, -1.75f, -8f), Quaternion.Euler(new Vector3(0, 90, 0))));
                _parts.AddRange(_landingGear);
                _plugin.Puts($"5");
                #region Right Doors
                _doors.Add(new MoveableDoor(GameManager.server.CreateEntity(xlSign),
                    new Vector3(xlSignHeight + 0.1f, 0f, xlSignWidth / 2f), new Vector3(xlSignHeight + 0.1f, 0, xlSignWidth),
                    Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 90f, 0f),
                    6f, 10f));
                _doors.Add(new MoveableDoor(GameManager.server.CreateEntity(xlSign),
                    new Vector3(xlSignHeight + 0.1f, 0f, -xlSignWidth / 2f), new Vector3(xlSignHeight + 0.1f, 0, -xlSignWidth),
                    Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 90f, 0f),
                    6f, 10f));
                #endregion

                #region Left Doors

                _doors.Add(new MoveableDoor(GameManager.server.CreateEntity(xlSign),
                    new Vector3(-xlSignHeight - 0.1f, 0f, xlSignWidth / 2f), new Vector3(-xlSignHeight - 0.1f, 0, xlSignWidth),
                    Quaternion.Euler(0f, -90f, 0f), Quaternion.Euler(0f, -90f, 0f),
                    6f, 10f));
                _doors.Add(new MoveableDoor(GameManager.server.CreateEntity(xlSign),
                    new Vector3(-xlSignHeight - 0.1f, 0f, -xlSignWidth / 2f), new Vector3(-xlSignHeight - 0.1f, 0, -xlSignWidth),
                    Quaternion.Euler(0f, -90f, 0f), Quaternion.Euler(0f, -90f, 0f),
                    6f, 10f));

                #endregion

                //Left doors
                //_doors.Add(new MoveableDoor(GameManager.server.CreateEntity(xlSign), new Vector3(xlSignHeight + 0.1f, 0, xlSignWidth / 2), new Vector3(xlSignHeight + 0.1f, 0, 0), Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 90f, 0f), 3f, 6f));

                foreach (var ent in _landingGear)
                {
                    //ent.gameObject.layer = (int)Layer.Default;
                }

                _plugin.Puts($"6");
                _parts.AddRange(_doors.Select(x => x.Entity));

                foreach (var part in _parts)
                {
                    part.SetParent(_centerEntity);
                    part.Spawn();
                    part.globalBroadcast = true;
                    part.net.SwitchGroup(BaseNetworkable.GlobalNetworkGroup);
                    if (part is Signage)
                    {
                        part.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                    if (part.GetComponent<Collider>() != null)
                    {
                        part.GetComponent<Collider>().enabled = true;
                    }
                }

                _centerEntity.globalBroadcast = true;
                _centerEntity.net.SwitchGroup(BaseNetworkable.GlobalNetworkGroup);

                foreach (var supply in _cargo)
                {
                    supply.GetComponent<Rigidbody>().isKinematic = true; //TODO: Move this to a dedicated function to spawn loot boxes
                    supply.inventory.Clear();
                    ItemManager.DoRemoves();
                    supply.CancelInvoke(supply.SpawnLoot);
                    supply.initialLootSpawn = false;
                    supply.GetType().GetMethod("RemoveParachute", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(supply, null);
                    supply.destroyOnEmpty = false;
                    supply.inventory.capacity = 30;
                    supply.panelName = "generic";
                }
                _plugin.Puts($"7");
                float delay = 0.07f;

                /*_soundTimer = _plugin.timer.Every(delay, () =>
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/player/swing_weapon.prefab", _centerEntity.transform.position + new Vector3(0,3,0), Vector3.zero, null);
                });*/

                //BasePlayer.activePlayerList.FirstOrDefault().SetParent(_centerEntity);
                _plugin.Puts($"8");
                AssignMinMax();
            }

            #endregion

            #region Bounds

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            private void AssignMinMax()
            {
                return;
                foreach(var entity in _parts)
                {
                    min = new Vector3(Mathf.Min(entity.transform.position.x, min.x), Mathf.Min(entity.transform.position.y, min.y), Mathf.Min(entity.transform.position.z, min.z));
                    max = new Vector3(Mathf.Max(entity.transform.position.x, max.x), Mathf.Max(entity.transform.position.y, max.y), Mathf.Max(entity.transform.position.z, max.z));
                }
                SetTriggerBounds(min - new Vector3(3,3,3), max + new Vector3(3, 3, 3));
            }

            #endregion

            int count = 0;

            Vector3 lastPos = Vector3.zero;
            Vector3 combinedChange = Vector3.zero;

            public string ReturnDebugLabelDisplay()
            {
                return $"Position: {_centerEntity.GetNetworkPosition()}\n" +
                       $"Pitch: {Math.Round(_pitch, 2)} Roll: {Math.Round(_roll, 2)} Yaw: {Math.Round(_yaw, 2)}\n" +
                       $"Rotor: {RotorVelocity} RPM Tail: {TailRotorVelocity} RPM\n" +
                       $"Speed: {Mathf.Round(_rigidBody.velocity.magnitude)}\n" +
                       $"Altitude: {Mathf.Round(_centerEntity.transform.position.y - Terrain.activeTerrain.SampleHeight(_centerEntity.transform.position) + 500)}";
            }

            private void ToggleRearDoor(bool state)
            {
                foreach(var door in _doors)
                {
                    door.ChangeState(state);
                }
            }

            public void OnCodelockPressed(CodeLock codeLock, bool unlock)
            {
                var button = _buttons.FirstOrDefault(x => x.Lock == codeLock);
                if (button == null)
                {
                    return;
                }
                button.OnPressed(unlock);
            }

            public void StartSpectate()
            {
                if (!_pilot.IsDead())
                {
                    //_pilot.DieInstantly();
                }
                _pilot.SetPlayerFlag(global::BasePlayer.PlayerFlags.Spectating, true);
                _pilot.gameObject.SetLayerRecursive(10);
                //Update spectate target
                _pilot.SetParent(_centerEntity);
                _pilot.transform.position = new Vector3(0f, 5f, -20f);
                _pilot.SendNetworkUpdateImmediate();
            }

            public void StopSpectate()
            {
                if (_pilot.IsDead())
                {
                    _pilot.Respawn();
                }
            }
        }

        public void SpawnHeli(Vector3 position, Quaternion rotation = default(Quaternion))
        {
            var ent = GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab", position);
            //var ent = GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", position, Quaternion.Euler(90f,0,0));
            ent.Spawn();
            foreach(var comp in ent.gameObject.GetComponents<Component>())
            {
                Puts($"Component: {comp.GetType()}");
            }
            ent.GetComponent<Collider>().transform.localScale = new Vector3(10f, 3f, 30f);
            var heli = ent.gameObject.AddComponent<Heli>();
            heli._centerEntity = ent;
            heli.SpawnEntities();
            heli._pilot = BasePlayer.activePlayerList.First();
            //heli.StartSpectate();
            testHeli = heli;
            //heli.SetPosition(position);
        }

        [ChatCommand("heli")]
        void SpawnHeli_ChatCmd(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            SpawnHeli(player.transform.position);
        }

        public static bool playerMoveDebug = false;

        [ChatCommand("helimove")]
        void MovePlayerDebuf(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            playerMoveDebug = !playerMoveDebug;
        }

        [ChatCommand("effect")]
        void TestSound(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            int distance = 10;
            if (args.Length > 0)
            {
                int.TryParse(args[0], out distance);
            }

            Vector3 pos = player.transform.position + player.eyes.BodyForward() * distance;

            float delay = 0.06f;

            timer.Repeat(delay, Mathf.CeilToInt(5f / delay), () =>
            {
                Effect.server.Run("assets/bundled/prefabs/fx/player/swing_weapon.prefab", pos, Vector3.zero, null);
            });
            
        }

        [ChatCommand("getbones")]
        void GetBones(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            string str = string.Join("\n", player.skeletonProperties.bones.Select(x => $"{x.name.english}").ToArray());
            Puts(str);
        }

        #region MonoBehaviors

        public const float ColliderRadius = 2.5f;
        public const float ColliderHeight = 3f;
        public const float ColliderHeightDifference = 0.3f;

        public class MyBaseTrigger : MonoBehaviour
        {
            public List<BasePlayer> _players { get; set; }
            private BoxCollider _collider { get; set; }
            private Rigidbody rigidbody { get; set; }
            private Timer _timer { get; set; }

            public virtual void Awake()
            {
                if (_players == null)
                {
                    _players = new List<BasePlayer>();
                }
                gameObject.layer = 3; //hack to get all trigger layers (Found in zone manager)

                //collider = gameObject.AddComponent<BoxCollider>();
                /*collider.radius = ColliderRadius;
                collider.height = ColliderHeight;*/
                //collider.isTrigger = true;

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
                _plugin.Puts($"OnPlayerExit");
                _players.Remove(player);
            }

            public virtual void OnPlayerEnter(BasePlayer player)
            {
                _plugin.Puts($"OnPlayerEnter");
                _players.Add(player);
            }

            public void SetTriggerBounds(Vector3 min, Vector3 max)
            {
                var box = GetComponent<BoxCollider>();
                if (box == null)
                {
                    _plugin.Puts($"Box collider is null :(");
                    return;
                }
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = (max - min) / 2;

                _plugin.Puts($"Box collider size: {box.size} Center: {box.center}");
            }
        }

        public class LookingAtTrigger : MyBaseTrigger
        {
            private Timer _timer { get; set; }
            private Dictionary<BasePlayer, BaseEntity> _lookingEntity { get; set; } = new Dictionary<BasePlayer, BaseEntity>();

            public override void Awake()
            {
                base.Awake();
            }

            public override void Init()
            {
                base.Init();
                _plugin.Puts("Init()");
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
                foreach(var player in _players)
                {
                    var ent = RaycastLookDirection(player);
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
            public CuiRectTransformComponent transform { get; protected set; }

            public string Name { get { return Element.Name; } }

            public Func<BasePlayer, bool> conditionalShow { get; set; }

            public Func<BasePlayer, Vector2> conditionalSize { get; set; }

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
                            transform
                        }
                };
                UpdatePlacement();

                Init();
            }

            public virtual void Init()
            {

            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (conditionalShow != null)
                {
                    if (!conditionalShow(player))
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
                transform.AnchorMin = $"{globalPosition.x} {globalPosition.y}";
                transform.AnchorMax = $"{globalPosition.x + globalSize.x} {globalPosition.y + globalSize.y}";
                RefreshAll();
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
            }

        }

        public class UIButton : UIElement
        {
            public CuiButtonComponent buttonComponent { get; private set; }
            public CuiTextComponent textComponent { get; private set; }
            private UILabel label { get; set; }
            private string _textColor { get; set; }
            private string _buttonText { get; set; }

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
                        transform.AnchorMin = $"{position.x} {position.y}";
                        transform.AnchorMax = $"{position.x + size.x} {position.y + size.y}";
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

            public UIPanel(Vector2 min, Vector2 max, string color = "0 0 0 .85", UIBaseElement parent = null) : base(min, max, parent)
            {
                panel = new CuiImageComponent
                {
                    Color = color
                };

                Element.Components.Insert(0, panel);
            }

            public UIPanel(Vector2 position, float width, float height, UIBaseElement parent = null, string color = "0 0 0 .85") : this(position, new Vector2(position.x + width, position.y + height), color, parent)
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

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variablePNG != null)
                {
                    Image.Png = variablePNG.Invoke(player);
                }
                base.Show(player, children);
            }
        }

        public class UIBaseElement
        {
            public Vector2 position { get; set; } = new Vector2();
            public Vector2 size { get; set; } = new Vector2();
            public Vector2 globalSize { get; set; } = new Vector2();
            public Vector2 globalPosition { get; set; } = new Vector2();
            public HashSet<BasePlayer> players { get; set; } = new HashSet<BasePlayer>();
            public UIBaseElement parent { get; set; }
            public HashSet<UIBaseElement> children { get; set; } = new HashSet<UIBaseElement>();
            public Vector2 min { get { return position; } }
            public Vector2 max { get { return position + size; } }
            public string Parent { get; set; } = "Hud.Menu";

            public UIBaseElement(UIBaseElement parent = null)
            {
                this.parent = parent;
            }

            public UIBaseElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : this(parent)
            {
                position = min;
                size = max - min;
                if (parent != null)
                {
                    parent.AddElement(this);
                }
                if (!(this is UIElement))
                {
                    UpdatePlacement();
                }
            }

            public void AddElement(UIBaseElement element)
            {
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
            }

            public virtual void Show(BasePlayer player, bool showChildren = true)
            {
                foreach (var child in children)
                {
                    child.Show(player, showChildren);
                }
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
                    //_plugin.Puts($"Adding {element.Name} to {player.userID}");
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
                size = new Vector2(x, y);
                UpdatePlacement();
            }

            public void SetPosition(float x, float y)
            {
                position = new Vector2(x, y);
                UpdatePlacement();
            }

            public virtual void UpdatePlacement()
            {
                if (parent == null)
                {
                    globalSize = size;
                    globalPosition = position;
                }
                else
                {
                    globalSize = Vector2.Scale(parent.globalSize, size);
                    globalPosition = parent.globalPosition + Vector2.Scale(parent.globalSize, position);
                }

                /*
                foreach (var child in children)
                {
                    _plugin.Puts("1.4");
                    UpdatePlacement();
                }*/
            }
        }

        public class UICheckbox : UIButton
        {
            public UICheckbox(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent: parent)
            {

            }
        }

        #endregion

    }
}
