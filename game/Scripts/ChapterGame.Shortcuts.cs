using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private static readonly Dictionary<string, Key> DefaultKeys = new() { ["map"] = Key.M, ["gear"] = Key.I, ["character"] = Key.C, ["journal"] = Key.J, ["help"] = Key.F1, ["endturn"] = Key.Space, ["labels"] = Key.Alt, ["inspect"] = Key.F2 };
    private Dictionary<string, Key> _keys = new(DefaultKeys);
    private string _bindingAction = "";
    private Key Shortcut(string action) => _keys[action];
    private string KeyLabel(string action) => _keys[action].ToString();
    private static string ShortcutName(string action) => action switch { "map" => "地图", "gear" => "装备仓库", "character" => "角色与卡组", "journal" => "任务", "help" => "操作指引", "endturn" => "结束回合", "labels" => "按住显示场景标签", _ => "固定完整说明" };
    private bool HandleKeyRebind(InputEvent input)
    {
        if (_panel != "keybind") return false;
        if (input is not InputEventKey key) return false;
        GetViewport().SetInputAsHandled();
        if (!key.Pressed || key.Echo) return true;
        if (key.Keycode == Key.Escape) { ClosePanel(); return true; }
        if (key.Keycode is Key.Enter or Key.KpEnter or Key.Tab or Key.F6 or Key.Left or Key.Right or Key.Up or Key.Down or Key.Shift or Key.Ctrl or Key.Alt or Key.Meta or Key.Menu or Key.F10 || key.CtrlPressed || key.AltPressed || key.ShiftPressed || key.MetaPressed)
        { _actionWarning = "此键用于确认、导航或组合操作，请选择一个单独的字母、数字或功能键。"; Render(); return true; }
        var conflict = _keys.FirstOrDefault(p => p.Key != _bindingAction && p.Value == key.Keycode);
        if (conflict.Key != null) { _actionWarning = "与“" + ShortcutName(conflict.Key) + "”冲突，请先更改该操作或换一个键。"; Render(); return true; }
        _keys[_bindingAction] = key.Keycode; SavePreferences(); ClosePanel(); return true;
    }
    private void KeyMap(VBoxContainer box)
    {
        box.AddChild(Text(UiCopy.Text("UI_68D935FD8D42"), 18, Ink));
        var page = Scroll(box);
        page.AddChild(Text(UiCopy.Text("UI_055458965265"),16,Ink));
        foreach (var action in DefaultKeys.Keys)
            page.AddChild(Button("bind_" + action, ShortcutName(action) + "    " + KeyLabel(action) + UiCopy.Text("UI_FB6B5E156577"), () => { _bindingAction = action; OpenNested("keybind"); }));
        page.AddChild(Text(UiCopy.Text("UI_7D03BE59B1F8"), 16, Ink));
        box.AddChild(Button("keys_reset", UiCopy.Text("UI_291C1FDF278D"), () => { _keys = new(DefaultKeys); SavePreferences(); Render(); }));
        box.AddChild(Button("keys_return", UiCopy.Text("UI_D00BFE7C685C"), ClosePanel));
    }
    private void ReadKeyBindings(ConfigFile file)
    {
        var loaded = new Dictionary<string, Key>();
        foreach (var (action, fallback) in DefaultKeys)
        {
            var key = (Key)(long)file.GetValue("keys", action, (long)fallback);
            if (!Enum.IsDefined(key) || key is Key.None or Key.Escape or Key.Enter or Key.Tab or Key.F6 || loaded.ContainsValue(key)) { _keys = new(DefaultKeys); return; }
            loaded[action] = key;
        }
        _keys = loaded;
    }
    private void WriteKeyBindings(ConfigFile file) { foreach (var (action, key) in _keys) file.SetValue("keys", action, (long)key); }
}
