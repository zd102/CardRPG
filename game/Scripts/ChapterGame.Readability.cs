using Godot;

namespace CardRPG;

public partial class ChapterGame
{
    private static readonly Color ControlEdge=new("66775f");
    private void ConfigureAccessibleControls()
    {
        foreach(var type in new[]{"LineEdit","OptionButton","SpinBox"})
        {
            Theme.SetColor("font_color",type,Ink);Theme.SetColor("font_placeholder_color",type,Muted);
            Theme.SetColor("font_selected_color",type,Ink);Theme.SetColor("caret_color",type,Ink);
            Theme.SetColor("selection_color",type,new Color("c0d1b7"));
            foreach(var state in new[]{"normal","hover","pressed","read_only"})Theme.SetStylebox(state,type,Style(new Color("eee9db"),ControlEdge,10));
            Theme.SetColor("font_hover_color",type,Ink);Theme.SetColor("font_pressed_color",type,Ink);Theme.SetColor("font_focus_color",type,Ink);
            Theme.SetColor("font_disabled_color",type,Muted);
            Theme.SetColor("icon_normal_color",type,Ink);Theme.SetColor("icon_hover_color",type,Ink);Theme.SetColor("icon_pressed_color",type,Ink);
        }
        foreach(var type in new[]{"HScrollBar","VScrollBar"})
        {
            Theme.SetStylebox("grabber",type,Style(ControlEdge,ControlEdge,0));
            Theme.SetStylebox("grabber_highlight",type,Style(Ink,Ink,0));
            Theme.SetStylebox("grabber_pressed",type,Style(Accent,Accent,0));
        }
    }
}
