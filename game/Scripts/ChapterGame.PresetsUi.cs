using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private string _presetName = "", _selectedPreset = "";
    private bool _presetEquipment = true, _presetCargo = true;
    private ChapterPreparationPreset? _presetDraft;
    private string _presetDraftName = "", _presetCompare = "";
    private void PreparationPresets(VBoxContainer box)
    {
        box.AddChild(Text(UiCopy.Text("UI_8A68C22F8A89"), 18, Ink));
        var saveRow = Row(10); box.AddChild(saveRow);
        var name = new LineEdit { Name = "PresetName", Text = _presetName, PlaceholderText = UiCopy.Text("UI_948D1858F725"), MaxLength = 24, SizeFlagsHorizontal = SizeFlags.ExpandFill }; saveRow.AddChild(name);
        saveRow.AddChild(Button("preset_save", UiCopy.Text("UI_4A08C8A1DFCB"), () =>
        {
            _presetName = name.Text.Trim();
            void SavePreset() { _presetDraft=null; Change(() => Session.SavePreparation(_presetName)); }
            if (Session.State.PreparationPresets.Any(p => p.Name == _presetName)) Confirm(UiCopy.Text("UI_B08B324CD5DC"), UiCopy.Text("UI_0E0F4E1C3E6E") + _presetName + UiCopy.Text("UI_816846AF8D99"), SavePreset, UiCopy.Text("UI_9FFD41B81750"));
            else SavePreset();
        }, Session.State.Phase == ChapterPhase.Camp));
        var split = Row(12); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var directory = Directory(split); var list = Scroll(directory);
        var presets = Session.State.PreparationPresets.ToList(); if (Session.State.LastPreparation != null) presets.Insert(0, Session.State.LastPreparation);
        if (!presets.Any(p => p.Name == _selectedPreset)) _selectedPreset = presets.FirstOrDefault()?.Name ?? "";
        for (var i = 0; i < presets.Count; i++)
        { var preset = presets[i]; list.AddChild(Button("preset_select_" + i, (_selectedPreset == preset.Name ? "◆ " : "") + preset.Name, () => { _selectedPreset = preset.Name; Render(); })); }
        var detail = Section(split); var selected = presets.FirstOrDefault(p => p.Name == _selectedPreset);
        if (selected == null) { detail.AddChild(Text(UiCopy.Text("UI_212A5BF7704D"), 18, Muted)); return; }
        if (_presetDraft == null || _presetDraftName != selected.Name)
        {
            _presetDraftName = selected.Name;
            _presetDraft = new() { Name = selected.Name, Equipment = new(selected.Equipment), Mods = new(selected.Mods), Carry = new(selected.Carry), FreeSupplies = selected.FreeSupplies };
        }
        selected = _presetDraft;
        detail.AddChild(Text(selected.Name, 20, Ink));
        var scope = Row(12); detail.AddChild(scope);
        var equipment = new CheckBox { Text = UiCopy.Text("UI_865DE1DE7296"), ButtonPressed = _presetEquipment }; scope.AddChild(equipment);
        var cargo = new CheckBox { Text = UiCopy.Text("UI_20B4409D5C76"), ButtonPressed = _presetCargo }; scope.AddChild(cargo);
        foreach (var check in new[] { equipment, cargo }) { check.AddThemeColorOverride("font_color", Ink); check.AddThemeColorOverride("font_pressed_color", Ink); check.AddThemeColorOverride("font_hover_color", Ink); }
        equipment.Toggled += value => { _presetEquipment = value; Render(); }; cargo.Toggled += value => { _presetCargo = value; Render(); };
        var body = Scroll(detail);
        body.AddChild(Text(UiCopy.Text("UI_E6C307F224BB"), 16, Muted));
        foreach (var (slot, kind) in selected.Equipment.ToArray())
        {
            var available = kind == "" || Session.State.Stock.Any(i => i.Kind == kind && !i.Container.StartsWith("ground:"));
            body.AddChild(Text(SlotName(slot) + "：" + (kind == "" ? UiCopy.Text("UI_8021A27AAFF0") : ChapterContent.ItemName(kind)) + (!available ? UiCopy.Text("UI_AA50EF6F0385") : "") + (kind == "" ? "" : UiCopy.Text("UI_235929584E2A") + (selected.Mods.GetValueOrDefault(kind, "") == "" ? UiCopy.Text("UI_C5EF963D2A31") : selected.Mods[kind])), 16, available ? Ink : Danger));
            var choices = Session.State.Stock.Where(i => !i.Container.StartsWith("ground:") && ChapterContent.Gear.TryGetValue(i.Kind, out var gear) && gear.Slot == slot).Select(i => i.Kind).Distinct().ToArray();
            var options = new OptionButton { Name = "PresetSubstitute_" + slot }; options.AddItem("保留此目标；或手动选择替代…");
            foreach (var candidate in choices) options.AddItem(ChapterContent.ItemName(candidate) + $" · {ChapterContent.Items[candidate].Grams / 1000m:0.##} kg" + (slot == "backpack" ? $" / {4 + ChapterContent.BagCapacity(candidate)} 格" : $" / {Session.GearCards(candidate).Length} 张供牌"));
            body.AddChild(options); options.ItemSelected += index => { if (index == 0) return; var candidate = choices[index - 1]; selected.Equipment[slot] = candidate; selected.Mods[candidate] = Session.State.Mods.GetValueOrDefault(candidate, ""); _presetCompare = candidate; Render(); };
            if (kind != "")
            {
                var activeMod=Session.State.Mods.GetValueOrDefault(kind,"");
                string ModName(string mod)=>mod==""?"基础版":mod=="D01"?"反击组件":"抽牌组件";
                body.AddChild(Text(UiCopy.Text("UI_F8D2A86F3F96")+ModName(activeMod)+UiCopy.Text("UI_08FE8F9625F6")+ModName(selected.Mods.GetValueOrDefault(kind,"")),16,Muted));
                var mods=new[]{""}.Concat(new[]{"D01","D08"}.Where(m=>Session.State.BoughtMods.Contains(kind+":"+m))).ToArray();
                if(mods.Length>1) {var picker=new OptionButton{Name="PresetMod_"+slot};foreach(var mod in mods)picker.AddItem(ModName(mod));picker.Selected=Math.Max(0,Array.IndexOf(mods,selected.Mods.GetValueOrDefault(kind,"")));body.AddChild(picker);picker.ItemSelected+=index=>{selected.Mods[kind]=mods[index];Render();};}
                var item = Session.State.Stock.FirstOrDefault(i => i.Kind == kind && i.Container is "carry" or "warehouse");
                if (item != null) body.AddChild(Button("preset_locate_" + slot, UiCopy.Text("UI_5E6393239E8F"), () => OpenStockObject(item.Id)));
                if (ChapterContent.Recipes.ContainsKey(kind) || kind.StartsWith("BP")) body.AddChild(Button("preset_recipe_" + slot, UiCopy.Text("UI_AFB599EDD957"), () => OpenRecipe(kind)));
            }
        }
        if (_presetCompare != "") { body.AddChild(Text(UiCopy.Text("UI_F7DEA568EC78"), 20, Ink)); EquipmentComparison(body, _presetCompare); }
        foreach (var (kind, count) in selected.Carry.ToArray())
        {
            var have = Session.CarriedItems.Where(i => i.Kind == kind && !i.FreeSupply).Sum(i => i.Quantity);
            var store = Session.StoredStock.Where(i => i.Kind == kind).Sum(i => i.Quantity);
            body.AddChild(Text(ChapterContent.ItemName(kind) + UiCopy.Format("UI_8727A13C646C", have, count, Math.Min(store, Math.Max(0, count - have)), Math.Max(0, count - have - store)), 16, Ink));
            if (count > have + store)
            {
                body.AddChild(Button("preset_omit_" + kind, UiCopy.Text("UI_54BEA90DF0D8"), () => { selected.Carry.Remove(kind); Render(); }));
                if (kind == "U01" || ChapterContent.Recipes.ContainsKey(kind) || kind.StartsWith("BP")) body.AddChild(Button("preset_cargo_recipe_" + kind, UiCopy.Text("UI_C64C3133061B"), () => OpenRecipe(kind)));
                if (ChapterContent.MaterialIds.Contains(kind)) body.AddChild(Button("preset_cargo_source_" + kind, UiCopy.Text("UI_55BCBD6E15E6"), () => { _materialInfoKind = kind; OpenNested("material-info"); }));
            }
        }
        body.AddChild(Text(UiCopy.Format("UI_BC84E247EFE2", selected.FreeSupplies), 16, Muted));
        var preview = Session.PreviewCopy(); var valid = preview.ApplyPreparation(selected, _presetEquipment, _presetCargo);
        detail.AddChild(Text(valid ? UiCopy.Format("UI_ED914B11F45E", preview.CarryUsed, preview.CarryCapacity, preview.DepartureLoad().Grams / 1000m) : preview.FailureReason, 16, valid ? Ink : Danger));
        detail.AddChild(Button("preset_apply", UiCopy.Text("UI_1EE3E1FA1816"), () =>
        {
            var summary = string.Join("\n", selected.Equipment.Select(p => SlotName(p.Key) + "：" + (p.Value == "" ? "空" : ChapterContent.ItemName(p.Value))));
            Confirm(UiCopy.Text("UI_F6AEA57DC4D4") + selected.Name, (_presetEquipment ? UiCopy.Text("UI_27B0FE910A31") + summary : UiCopy.Text("UI_6CA71C64359C"))
                + (_presetCargo ? UiCopy.Text("UI_ACE908DCFA6E") + selected.FreeSupplies + UiCopy.Text("UI_4531E95EB3E8") : UiCopy.Text("UI_554D9C79288F"))
                + UiCopy.Format("UI_FBDC06E27ADA", Session.CarryUsed, preview.CarryUsed, preview.CarryCapacity, Session.CarriedGrams / 1000m, preview.CarriedGrams / 1000m), () => Change(() => Session.ApplyPreparation(selected, _presetEquipment, _presetCargo)), UiCopy.Text("UI_57A1CD214175"));
        }, valid, true));
        detail.AddChild(Button("preset_equipment", UiCopy.Text("UI_A60CFB4F19D8"), () => { _gearFilter = "all"; OpenNested("gear"); }));
        if (Session.State.PreparationPresets.Any(p => p.Name == selected.Name)) detail.AddChild(Button("preset_remove", UiCopy.Text("UI_6572E725C837"), () => Confirm(UiCopy.Text("UI_28786D906975"), UiCopy.Text("UI_86C4E58479F7") + selected.Name + UiCopy.Text("UI_BCD589203B68"), () => Change(() => Session.RemovePreparation(selected.Name)), UiCopy.Text("UI_28786D906975"))));
    }
}
