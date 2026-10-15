using Godot;
using System;
using System.Runtime.InteropServices;

namespace CardRPG;

public partial class ChapterGame
{
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(nint window);
    private double _environmentPoll;
    private int _lastUiScreen=-1,_lastUiDpi;
    private int CurrentUiDpi()
    {
        if(OS.GetName()=="Windows" && DisplayServer.GetName()!="headless")
        {
            try
            {
                var handle=DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle,GetWindow().GetWindowId());
                if(handle!=0 && GetDpiForWindow((nint)handle) is var dpi && dpi>0)return (int)dpi;
            }
            catch(EntryPointNotFoundException) { }
        }
        return Math.Max(96,DisplayServer.ScreenGetDpi(GetWindow().CurrentScreen));
    }
    internal static float UiScaleFor(int width,int height,int dpi,float preferred)=>Math.Min(preferred*Math.Clamp(dpi/96f,1,3),Math.Min(width/1100f,height/760f));
    private void PollUiEnvironment(double elapsed)
    {
        _environmentPoll+=elapsed;if(_environmentPoll<.5)return;_environmentPoll=0;
        var screen=GetWindow().CurrentScreen;var dpi=CurrentUiDpi();
        if(_lastUiScreen==screen&&_lastUiDpi==dpi)return;
        var initialized=_lastUiScreen>=0;_lastUiScreen=screen;_lastUiDpi=dpi;
        if(initialized)RefreshLayout();
    }
}
