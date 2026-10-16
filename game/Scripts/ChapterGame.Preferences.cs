using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private bool _quickMove = true, _dragItems = true, _hoverTips = true, _pinEntry = true, _navigationLabels = true;
    private int _hoverDelayMs = 350;
    private float _volume = .65f, _dangerVolume = .8f, _uiScale = 1;
    private string _settingsTab = "display";
    private string _inputModality = "mouse";
    private Label? _modalityHint;
    private ulong _renderGeneration, _lastCommandFrame = ulong.MaxValue;
    private bool _leftHeld, _blockLeftRelease;
    private int _axisX, _axisY;
    private readonly System.Collections.Generic.HashSet<JoyButton> _heldJoyButtons=new();

    private void ConvenienceSettings(VBoxContainer page)
    {
        void ToggleSetting(string id, string name, Func<bool> get, Action change) => page.AddChild(Button(id, name + (get() ? UiCopy.Text("UI_DAA8256B157C") : UiCopy.Text("UI_502E394AE645")), () => { change(); SavePreferences(); Render(); }));
        ToggleSetting("setting_quick_move", "Shift 快移（按已选数量）", () => _quickMove, () => _quickMove = !_quickMove);
        ToggleSetting("setting_drag", "拖拽物品（保留按钮取放）", () => _dragItems, () => _dragItems = !_dragItems);
        ToggleSetting("setting_drag_cards", "拖拽出牌 / 整理手牌", () => _dragCards, () => _dragCards = !_dragCards);
        ToggleSetting("setting_hover", "悬浮与聚焦说明", () => _hoverTips, () => { _hoverTips = !_hoverTips; if (!_hoverTips) _inspection?.Hide(); });
        ToggleSetting("setting_pin", "说明中的固定详情入口", () => _pinEntry, () => _pinEntry = !_pinEntry);
        page.AddChild(Button("setting_hover_delay", UiCopy.Format("UI_FAD2583E55A7", _hoverDelayMs), () => { _hoverDelayMs = _hoverDelayMs == 0 ? 350 : _hoverDelayMs == 350 ? 700 : 0; SavePreferences(); Render(); }));
        page.AddChild(Text(UiCopy.Text("UI_F5FA5913601F"), 16, Muted));
        page.AddChild(Button("convenience_reset", UiCopy.Text("UI_5031675A0EF1"), () => { _quickMove = _dragItems = _dragCards = _hoverTips = _pinEntry = true; _hoverDelayMs = 350; SavePreferences(); Render(); }));
    }
    private void AudioSettings(VBoxContainer page)
    {
        page.AddChild(Button("setting_sound", UiCopy.Text("UI_063FE451A21C") + (_sound ? UiCopy.Text("UI_39EAE64CFC41") : UiCopy.Text("UI_7DB2E189ECB0")), () => { _sound = !_sound; SavePreferences(); Render(); }));
        void Volume(string id, string title, float value, Action<float> apply)
        {
            var label = Text($"{title} {value:P0}", 16, Ink); page.AddChild(label);
            var slider = new HSlider { Name = id, MinValue = 0, MaxValue = 100, Step = 5, Value = value * 100, CustomMinimumSize = new Vector2(260, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            page.AddChild(slider); slider.ValueChanged += v => { apply((float)v / 100); label.Text = $"{title} {v / 100:P0}"; SavePreferences(); };
        }
        Volume("NormalVolume", "普通操作音量", _volume, v => _volume = v);
        Volume("DangerVolume", "危险提示音量", _dangerVolume, v => _dangerVolume = v);
        var samples = Row(); page.AddChild(samples);
        samples.AddChild(Button("sample_normal", UiCopy.Text("UI_3A5C8CE09AC8"), () => Cue("select")));
        samples.AddChild(Button("sample_danger", UiCopy.Text("UI_E18815A85422"), () => Cue("danger")));
        page.AddChild(Text(UiCopy.Text("UI_D9A379AB291F"), 16, Muted));
    }
    private void ApplyUiScale()
    {
        var window = GetWindow();
        var factor = UiScaleFor(window.Size.X,window.Size.Y,CurrentUiDpi(),_uiScale);
        if (Math.Abs(factor - 1) < .001f) { window.ContentScaleSize = Vector2I.Zero; return; }
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        var logical = new Vector2I((int)Math.Ceiling(window.Size.X / factor), (int)Math.Ceiling(window.Size.Y / factor));
        if (window.ContentScaleSize != logical) window.ContentScaleSize = logical;
    }
    private bool HandleInputOwnership(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && motion.Relative.LengthSquared() > 4)
        {var changed=_inputModality!="mouse";_inputModality="mouse";if(changed)_resumePointerInspection?.Invoke();}
        if (input is InputEventKey { Pressed: true, Echo: false }) _inputModality = "keyboard";
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.Pressed) _inputModality = "mouse";
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                _leftHeld = mouse.Pressed;
                if (!mouse.Pressed && _blockLeftRelease) { _blockLeftRelease = false; GetViewport().SetInputAsHandled(); return true; }
            }
        }
        if (_modalityHint != null && GodotObject.IsInstanceValid(_modalityHint)) _modalityHint.Text = InputHelp;
        return false;
    }
    private string InputHelp => _inputModality == "controller" ? "方向键选择 · A 确认 · B 返回 · 肩键跨区"
        : _panel == "gear" && _gearFilter == "cargo" ? _inputModality == "keyboard" ? "Shift+Enter 取放 · F6 跨区 · Esc 返回" : _quickMove && Session.AtBase ? "Shift+点击 取放 · 拖放移动 · 右键更多 · Esc 返回" : "点击选中 · 拖放移动 · 右键更多 · Esc 返回"
        : _inputModality == "keyboard" ? "Tab / 方向键选择 · F6 跨区 · Esc 返回" : "点击选择 · 更多 / 右键 · Esc 返回";
    private bool HandleController(InputEvent input)
    {
        Key key = Key.None; bool pressed = true;
        if (input is InputEventJoypadButton button)
        {
            pressed = button.Pressed;
            if(pressed&&!_heldJoyButtons.Add(button.ButtonIndex)){GetViewport().SetInputAsHandled();return true;}if(!pressed)_heldJoyButtons.Remove(button.ButtonIndex);
            key = button.ButtonIndex switch { JoyButton.A => Key.Enter, JoyButton.B => Key.Escape, JoyButton.Start => Key.Escape, JoyButton.DpadLeft => Key.Left, JoyButton.DpadRight => Key.Right, JoyButton.DpadUp => Key.Up, JoyButton.DpadDown => Key.Down, JoyButton.LeftShoulder or JoyButton.RightShoulder => Key.F6, _ => Key.None };
        }
        else if (input is InputEventJoypadMotion motion && motion.Axis is JoyAxis.LeftX or JoyAxis.LeftY)
        {
            var value = Math.Abs(motion.AxisValue) < .25f ? 0 : Math.Abs(motion.AxisValue) >= .65f ? Math.Sign(motion.AxisValue) : 2;
            if (value == 2) return true;
            var previous = motion.Axis == JoyAxis.LeftX ? _axisX : _axisY;
            if (motion.Axis == JoyAxis.LeftX) _axisX = value; else _axisY = value;
            if (value == 0 || value == previous) return true;
            key = motion.Axis == JoyAxis.LeftX ? value > 0 ? Key.Right : Key.Left : value > 0 ? Key.Down : Key.Up;
        }
        else return false;
        GetViewport().SetInputAsHandled();
        if (key != Key.None)
        {
            GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = pressed, ShiftPressed=input is InputEventJoypadButton {ButtonIndex:JoyButton.LeftShoulder} });
            if (input is InputEventJoypadMotion) GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = false });
        }
        _inputModality = "controller";
        if (_modalityHint != null && GodotObject.IsInstanceValid(_modalityHint)) _modalityHint.Text = InputHelp;
        return true;
    }
}
