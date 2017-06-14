using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OxidePluginV1
{
    public partial class JakeUIFramework : RustPlugin
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
                    AddPlayer(player);
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

        #region Global _plugin

        void Init()
        {
            _plugin = this;
        }

        public static JakeUIFramework _plugin;

        #endregion
    }
}
