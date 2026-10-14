using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private PanelContainer? _inspection;
    private Label? _inspectionCopy;
    private Control? _inspected;

    private static ExpeditionGlyph Glyph(string symbol, Color? color = null, float size = 26) => new()
    { Symbol = symbol, Tint = color ?? Accent, CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };

    private void Inspect(Control control, Func<string> description)
    {
        if (control is not Godot.Button)
        {
            control.Draw += () => { if (control.HasFocus()) control.DrawRect(new Rect2(Vector2.Zero,control.Size),Accent,false,2); };
            control.FocusEntered += control.QueueRedraw; control.FocusExited += control.QueueRedraw;
        }
        void Show()
        {
            if (_inspection == null || !GodotObject.IsInstanceValid(_inspection)) return;
            if (control.Name == "close_panel" || control.Name.ToString().StartsWith("target_")) return;
            var text = description();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_selectedCard >= 0 && _panel == "" && Session.State.Phase == ChapterPhase.Battle) return;
            _inspected = control; _inspectionCopy!.Text = text; _inspection.Visible = true;
            if (_panel != "")
            {
                var rect = control.GetGlobalRect(); var shortCopy = !text.Contains('\n') && text.Length <= 28;
                var width = Math.Min(shortCopy ? Math.Max(180, text.Length * 16 + 32) : 540, Size.X - 64);
                var height = shortCopy ? 52 : Math.Min(210, Math.Max(80, text.Length / 28 * 24 + 56));
                var x = Mathf.Clamp(rect.Position.X, 32, Size.X - width - 32);
                var y = rect.End.Y + height + 12 < Size.Y - 32 ? rect.End.Y + 8 : Math.Max(90, rect.Position.Y - height - 8);
                Dock(_inspection, x, y, x + width, y + height);
            }
        }
        void Hide()
        {
            if (_inspected != control || _inspection == null || !GodotObject.IsInstanceValid(_inspection)) return;
            _inspection.Visible = false; _inspected = null;
        }
        control.MouseEntered += Show; control.FocusEntered += Show;
        control.MouseExited += () => { if (!control.HasFocus()) Hide(); };
        control.FocusExited += Hide;
    }

    private void InspectionLayer()
    {
        _inspected = null;
        _inspection = FlatPanel(_root, 12); _inspection.Name = "Inspection";
        _inspection.ZIndex = 20; _inspection.MouseFilter = MouseFilterEnum.Ignore; _inspection.Visible = false;
        if (_panel != "") Dock(_inspection, 340, 120, -24, 260, ar:1);
        else if (Session.State.Phase == ChapterPhase.Battle)
        {
            Dock(_inspection,-300,-442,300,-266,.5f,1,.5f,1);
        }
        else Dock(_inspection, -300, -238, 300, -108, .5f, 1, .5f, 1);
        _inspectionCopy = Text("", 16, Ink); _inspectionCopy.AddThemeFontSizeOverride("font_size",16); _inspection.AddChild(_inspectionCopy);
    }

    private Button IconButton(string id, string symbol, string description, Action action, bool primary = false)
    {
        var button = Button(id, "", action, primary: primary);
        button.CustomMinimumSize = new Vector2(46, 46); button.TooltipText = description;
        var glyph = Glyph(symbol, primary ? new Color("f1e7ce") : Ink, 24);
        button.AddChild(glyph); Dock(glyph, -12, -12, 12, 12, .5f, .5f, .5f, .5f);
        return button;
    }

    private HBoxContainer SymbolValue(Node parent, string symbol, string value, Color color, string description = "", int size = 20)
    {
        var row = Row(6); row.MouseFilter = MouseFilterEnum.Pass; parent.AddChild(row);
        row.AddChild(Glyph(symbol, color, size + 6));
        var label = Inline(value, size, color);
        if (value.Any(char.IsDigit) && !value.Any(c => c >= 0x3400)) label.AddThemeFontOverride("font", ChapterSkin.NumberFont);
        row.AddChild(label);
        if (description != "")
        {
            row.FocusMode = FocusModeEnum.All;
            Inspect(row, () => description);
        }
        return row;
    }

    private void Pips(Node parent, string symbol, int value, int max, Color color, string description)
    {
        var row = Row(7); parent.AddChild(row); row.AddChild(Glyph(symbol, color));
        for (var i = 0; i < max; i++)
        {
            var cell = new Panel { CustomMinimumSize = new Vector2(14, 18), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            cell.AddThemeStyleboxOverride("panel", Style(i < value ? color : new Color("d3ccb3"), i < value ? color : Muted, 0)); row.AddChild(cell);
        }
        row.FocusMode = FocusModeEnum.All;
        Inspect(row, () => description + $"  {value}/{max}");
    }

    private void MaterialStrip(Node parent, int[] materials, Color? color = null)
    {
        var row = Row(20); parent.AddChild(row);
        for (var i = 0; i < 3; i++) SymbolValue(row, new[] { "steel", "wire", "spring" }[i], materials[i].ToString(), color ?? Accent, ChapterContent.MaterialName(i));
    }

    private static string CardSymbol(ChapterCard card) => card.Id switch
    { "C04" or "C05" => "bolt", "C02" or "C19" or "C35" => "bash", "C06" => "breaker", "C18" => "all", "C36" => "draw", _ => card.Damage == 0 ? "guard" : "hammer" };

    private void CardEffects(Node parent, ChapterCard card, Color ink, bool live = false, int size = 20)
    {
        var effects = Row(size <= 14 ? 6 : 12); effects.Alignment = BoxContainer.AlignmentMode.Center; parent.AddChild(effects);
        if (card.Damage > 0)
        {
            var damage = card.Damage + (live && card.Id == "C07" ? Math.Min(8, Session.State.Battle?.Shield ?? 0) : 0);
            SymbolValue(effects, card.Id == "C18" ? "all" : "hammer", damage.ToString(), ink, size: size);
        }
        if (card.Shield > 0) SymbolValue(effects, "guard", card.Shield.ToString(), ink, size: size);
        if (card.Id == "C05") SymbolValue(effects, "mana", "+1", ink, size: size);
        if (card.Id is "C26" or "C36") SymbolValue(effects, "draw", card.Id == "C36" ? "+2" : "+1", ink, size: size);
    }

    private static string CardKeyword(string id) => id switch
    {
        "C06" => "穿盾", "C07" => "耗盾 ≤8", "C18" => "全体", "C25" => "留盾 ≤4",
        "C26" or "C36" => "抽牌上限 2", _ => ""
    };

    private (string Place, string Symbol, int Step, string[] Stages) ObjectiveState()
    {
        var s = Session.State;
        if (!s.Done.Contains("Q01"))
        {
            var step = s.StoredItems.Contains("T01") ? 2 : s.Items.Contains("T01") ? 1 : 0;
            return (step == 0 ? "废料场" : step == 1 ? "安全撤离" : "岑砧", "bolt", step, ["loot", "exit", "home"]);
        }
        if (!s.Done.Contains("Q02"))
        {
            var step = s.StoredCalibration ? 3 : s.Calibrated ? 2 : s.Items.Contains("T02") || s.StoredItems.Contains("T02") ? 1 : 0;
            return (step == 0 ? "货运广场" : step == 1 ? "维修工坊" : step == 2 ? "安全撤离" : "岑砧", "craft", step, ["loot", "craft", "exit", "home"]);
        }
        if (!s.Done.Contains("S01"))
        {
            var step = s.StoredItems.Contains("T11") ? 2 : s.Items.Contains("T11") ? 1 : 0;
            return (step == 0 ? "外环货站" : step == 1 ? "安全撤离" : "岑砧", "route", step, ["craft", "exit", "home"]);
        }
        return ("自由探索", "check", 0, ["map"]);
    }
}
