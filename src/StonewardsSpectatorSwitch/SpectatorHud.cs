using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsSpectatorSwitch
{
    /// <summary>
    /// A compact, local spectator picker in the game's own HUD document.
    /// The transparent root does not block game UI. Only the two buttons
    /// participate in pointer picking. Clicks are ignored outside death mode.
    /// </summary>
    internal sealed class SpectatorHud
    {
        private static readonly Color PanelColor = new Color(0.075f, 0.065f, 0.055f, 0.93f);
        private static readonly Color BorderColor = new Color(0.53f, 0.40f, 0.23f, 1f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.18f, 0.13f, 0.98f);
        private static readonly Color GoldColor = new Color(0.91f, 0.75f, 0.42f, 1f);
        private static readonly Color IvoryColor = new Color(0.95f, 0.91f, 0.82f, 1f);
        private static readonly Color MutedColor = new Color(0.69f, 0.67f, 0.62f, 1f);

        private readonly Action<int> _cycle;
        private VisualElement _root;
        private Label _name;
        private Label _hint;
        private Button _previous;
        private Button _next;
        private Hud _attachedHud;

        public SpectatorHud(Action<int> cycle)
        {
            _cycle = cycle;
        }

        public bool Show(string playerName, int index, int count)
        {
            if (!EnsureAttached())
                return false;

            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();

            bool hasPlayers = count > 0;
            _previous.SetEnabled(hasPlayers);
            _next.SetEnabled(hasPlayers);

            if (!hasPlayers)
            {
                SetText(_name, "NO LIVING DWARFS");
                SetText(_hint, "Waiting for a teammate");
            }
            else if (index < 0)
            {
                SetText(_name, "SELECT A DWARF");
                SetText(_hint, count + " alive  |  LMB / RMB  |  Hold ALT to click");
            }
            else
            {
                SetText(_name, ShortName(playerName) + "  (" + (index + 1) + "/" + count + ")");
                SetText(_hint, "LMB / RMB  |  Hold ALT to click");
            }

            return true;
        }

        public void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        public void Dispose()
        {
            if (_root != null)
                _root.RemoveFromHierarchy();
            _root = null;
            _attachedHud = null;
        }

        private bool EnsureAttached()
        {
            var hud = Hud.Instance;
            var document = hud != null ? hud.hudDocument : null;
            var parent = document != null ? document.rootVisualElement : null;
            if (parent == null)
            {
                Hide();
                return false;
            }

            if (_root == null)
                Build();

            // The native HUD is recreated between scenes/rounds.
            if (_attachedHud != hud || _root.panel == null)
            {
                _root.RemoveFromHierarchy();
                parent.Add(_root);
                _attachedHud = hud;
            }

            return true;
        }

        private void Build()
        {
            _root = new VisualElement { name = "SpectatorSwitchRoot", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0f;
            _root.style.right = 0f;
            _root.style.bottom = 96f;
            _root.style.height = 94f;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.display = DisplayStyle.None;

            var panel = new VisualElement { name = "SpectatorSwitchPanel", pickingMode = PickingMode.Ignore };
            panel.style.width = 468f;
            panel.style.height = 80f;
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.SpaceAround;
            panel.style.backgroundColor = PanelColor;
            panel.style.borderTopWidth = 2f;
            panel.style.borderRightWidth = 2f;
            panel.style.borderBottomWidth = 2f;
            panel.style.borderLeftWidth = 2f;
            panel.style.borderTopColor = BorderColor;
            panel.style.borderRightColor = BorderColor;
            panel.style.borderBottomColor = BorderColor;
            panel.style.borderLeftColor = BorderColor;
            panel.style.borderTopLeftRadius = 5f;
            panel.style.borderTopRightRadius = 5f;
            panel.style.borderBottomLeftRadius = 5f;
            panel.style.borderBottomRightRadius = 5f;
            _root.Add(panel);

            _previous = CreateArrow("<", "Previous living dwarf");
            _previous.clicked += () => _cycle(-1);
            panel.Add(_previous);

            var center = new VisualElement { pickingMode = PickingMode.Ignore };
            center.style.width = 330f;
            center.style.flexShrink = 1f;
            center.style.alignItems = Align.Center;
            center.style.justifyContent = Justify.Center;
            panel.Add(center);

            var title = new Label("SPECTATING") { pickingMode = PickingMode.Ignore };
            title.style.fontSize = 10f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = GoldColor;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 1f;
            center.Add(title);

            _name = new Label("SELECT A DWARF") { pickingMode = PickingMode.Ignore };
            _name.style.fontSize = 17f;
            _name.style.unityFontStyleAndWeight = FontStyle.Bold;
            _name.style.color = IvoryColor;
            _name.style.unityTextAlign = TextAnchor.MiddleCenter;
            _name.style.marginBottom = 2f;
            center.Add(_name);

            _hint = new Label("LMB / RMB  |  Hold ALT to click") { pickingMode = PickingMode.Ignore };
            _hint.style.fontSize = 11f;
            _hint.style.color = MutedColor;
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            center.Add(_hint);

            _next = CreateArrow(">", "Next living dwarf");
            _next.clicked += () => _cycle(1);
            panel.Add(_next);
        }

        private static Button CreateArrow(string symbol, string tooltip)
        {
            var button = new Button { text = symbol, tooltip = tooltip };
            button.style.width = 47f;
            button.style.height = 47f;
            button.style.flexShrink = 0f;
            button.style.backgroundColor = ButtonColor;
            button.style.color = GoldColor;
            button.style.fontSize = 27f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = BorderColor;
            button.style.borderRightColor = BorderColor;
            button.style.borderBottomColor = BorderColor;
            button.style.borderLeftColor = BorderColor;
            return button;
        }

        private static void SetText(Label label, string text)
        {
            if (label.text != text)
                label.text = text;
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "DWARF";
            // Do not allow unbounded user-supplied Steam names to cover the buttons.
            return name.Length > 26 ? name.Substring(0, 25) + "…" : name;
        }
    }
}
