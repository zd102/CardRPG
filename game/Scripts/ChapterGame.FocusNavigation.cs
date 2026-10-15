using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private readonly HashSet<Key> _heldActivation = new(), _blockedActivation = new();
    private readonly Dictionary<string, string> _panelFocus = new();
    private string _inputBoundary = "";

    private bool HandleActivationBoundary(InputEvent input)
    {
        if (input is not InputEventKey key || key.Keycode is not (Key.Enter or Key.KpEnter or Key.Space)) return false;
        if (key.Pressed) _heldActivation.Add(key.Keycode); else _heldActivation.Remove(key.Keycode);
        var blocked = _blockedActivation.Contains(key.Keycode);
        if (!key.Pressed) _blockedActivation.Remove(key.Keycode);
        if (blocked) GetViewport().SetInputAsHandled();
        return blocked;
    }
    private void UpdateInputBoundary()
    {
        var boundary = _panel + ":" + Session.State.Phase;
        if (boundary == _inputBoundary) return;
        _inputBoundary = boundary;
        _blockedActivation.UnionWith(_heldActivation);
    }
    private string InitialPanelFocus()
    {
        if (_panel == "confirm") return "cancel_action";
        if (_panel == "startup") return _loadedSave ? "startup_continue" : "startup_new";
        if (_panel == "quantity") return _quotaMode ? "CarryQuota" : "StockQuantity";
        if (_panelFocus.TryGetValue(_panel, out var remembered) && (remembered.StartsWith("stock_") && remembered.Length > 6 && char.IsDigit(remembered[6]) || new[] { "inspect_", "recipe_", "quest_detail_", "map_place_" }.Any(remembered.StartsWith))
            && Buttons.TryGetValue(remembered, out var previous) && !previous.Disabled) return remembered;
        var candidate = _panel switch
        {
            "gear" when _gearFilter == "cargo" => _stockPaired ? "paired_batch_" + _selectedStock : $"stock_{_selectedStock}_0_0",
            "gear" => "inspect_" + _selectedGear,
            "craft" when _craftTab == "recipes" => "recipe_" + _selectedRecipe,
            "craft" when _craftTab == "mods" => "mod_choice_" + _selectedModGear + "_" + _selectedMod,
            "journal" => "quest_detail_" + _selectedQuest,
            "map" => "map_place_" + _mapPlace,
            "prepare" => "start_" + Session.State.Start,
            "detail" => "DetailReading",
            _ => "close_panel"
        };
        if (Buttons.TryGetValue(candidate, out var button) && !button.Disabled || candidate == "DetailReading") return candidate;
        foreach (var region in _pageScrolls)
        {
            var first = Buttons.FirstOrDefault(p => region.IsAncestorOf(p.Value) && !p.Value.Disabled);
            if (first.Value != null) return first.Key;
        }
        return _pageScrolls.FirstOrDefault()?.Name.ToString() ?? "close_panel";
    }
    private void FocusNextRegion(bool backwards)
    {
        var regions = _pageScrolls.Where(s => s.IsVisibleInTree()).Select(s => (Control)s).ToList();
        if (_root.FindChild("StockActionBar", true, false) is Control stock) regions.Add(stock);
        else
        {
            var action = Buttons.LastOrDefault(p => !p.Value.Disabled && p.Value.IsVisibleInTree() && p.Key != "close_panel").Value;
            if (action != null) regions.Add(action);
        }
        if (regions.Count == 0) return;
        var focus = GetViewport().GuiGetFocusOwner();
        var index = regions.FindIndex(r => r == focus || focus != null && r.IsAncestorOf(focus));
        if (index < 0 && backwards) index = 0;
        for (var step = 1; step <= regions.Count; step++)
        {
            var region = regions[(index + (backwards ? -step : step) + regions.Count * 2) % regions.Count];
            var target = Buttons.Values.FirstOrDefault(b => b.IsVisibleInTree() && !b.Disabled && (b == region || region.IsAncestorOf(b)));
            if (target != null) { FocusLater(target); return; }
            if (region is ScrollContainer) { FocusLater(region); return; }
        }
    }
    private bool SpatialNavigation(Key key)
    {
        if (key is not (Key.Left or Key.Right or Key.Up or Key.Down) || TextEntryFocused() || ExclusivePanel) return false;
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus is ScrollContainer || _panel == "" && Session.State.Phase == ChapterPhase.Battle) return false;
        var candidates = Buttons.Values.Where(b => !b.Disabled && b.IsVisibleInTree() && b.FocusMode != FocusModeEnum.None && !b.Name.ToString().StartsWith("inspection_")).Cast<Control>();
        if (_panel == "") candidates = candidates.OfType<ChapterHotspot>();
        else
        {
            var region = _pageScrolls.FirstOrDefault(s => focus != null && s.IsAncestorOf(focus));
            if (region != null) candidates = candidates.Where(region.IsAncestorOf);
            else candidates = candidates.Where(b => !_pageScrolls.Any(s => s.IsAncestorOf(b)) && !b.Name.ToString().StartsWith("nav_"));
        }
        var all = candidates.ToArray(); if (all.Length == 0) return false;
        if (focus == null || !all.Contains(focus)) { FocusLater(all[0]); return true; }
        var direction = key switch { Key.Left => Vector2.Left, Key.Right => Vector2.Right, Key.Up => Vector2.Up, _ => Vector2.Down };
        var center = focus.GetGlobalRect().GetCenter();
        var next = all.Where(c => c != focus).Select(c => (Control: c, Delta: c.GetGlobalRect().GetCenter() - center))
            .Where(x => x.Delta.Dot(direction) > 1).OrderBy(x => x.Delta.Length() + Math.Abs(x.Delta.Cross(direction)) * 3).FirstOrDefault().Control;
        if (next != null)
        {
            foreach (var scroll in _pageScrolls.Where(s => s.IsAncestorOf(next))) scroll.EnsureControlVisible(next);
            FocusLater(next);
        }
        return true;
    }
}
