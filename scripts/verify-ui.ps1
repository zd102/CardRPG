param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/ui-rework-captures'),
    [string]$Executable,
    [switch]$Headless
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\common.ps1"
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class BackgroundVerificationDesktop {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
 public struct STARTUPINFO {
  public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
  public int dwX,dwY,dwXSize,dwYSize,dwXCountChars,dwYCountChars,dwFillAttribute,dwFlags;
  public short wShowWindow,cbReserved2; public IntPtr lpReserved2,hStdInput,hStdOutput,hStdError;
 }
 [StructLayout(LayoutKind.Sequential)] public struct PROCESS_INFORMATION { public IntPtr process,thread; public uint pid,tid; }
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
 [DllImport("user32.dll",SetLastError=true)] static extern bool CloseDesktop(IntPtr desktop);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcess(string app,StringBuilder command,IntPtr pa,IntPtr ta,bool inherit,uint flags,IntPtr environment,string cwd,ref STARTUPINFO si,out PROCESS_INFORMATION pi);
 [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
 [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr handle,out uint code);
 [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr handle,uint code);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 public static uint Run(string exe,string args,string cwd) {
  string name="CardRPG_Audit_"+Guid.NewGuid().ToString("N");
  IntPtr desktop=CreateDesktop(name,IntPtr.Zero,IntPtr.Zero,0,0x10000000,IntPtr.Zero);
  if(desktop==IntPtr.Zero) throw new Win32Exception();
  PROCESS_INFORMATION pi=new PROCESS_INFORMATION();
  try {
   STARTUPINFO si=new STARTUPINFO(); si.cb=Marshal.SizeOf(si); si.lpDesktop=name; si.dwFlags=1; si.wShowWindow=0;
   if(!CreateProcess(exe,new StringBuilder("\""+exe+"\" "+args),IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,cwd,ref si,out pi)) throw new Win32Exception();
   Console.WriteLine("ISOLATED_DESKTOP_STARTED pid="+pi.pid+"; desktop never activated");
   uint wait=WaitForSingleObject(pi.process,600000);
   if(wait!=0) { TerminateProcess(pi.process,124); throw new Exception("Background capture timeout"); }
   uint code; GetExitCodeProcess(pi.process,out code); return code;
  } finally { if(pi.thread!=IntPtr.Zero) CloseHandle(pi.thread); if(pi.process!=IntPtr.Zero) CloseHandle(pi.process); CloseDesktop(desktop); }
 }
}
'@
$auditCapture = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $auditCapture -Force | Out-Null
$auditLog = Join-Path $auditCapture 'render.log'
$auditExecutable = if ($Executable) { (Resolve-Path -LiteralPath $Executable).Path } else { Find-Godot }
$auditProject = if ($Executable) { '' } else { '--path "' + $GameRoot + '" ' }
$auditMode = if ($Headless) { '--headless ' } else { '--disable-vsync --max-fps 60 ' }
$auditCaptureArgument = if ($Headless) { '' } else { ' "--capture-dir=' + $auditCapture + '"' }
$auditArguments = $auditProject + $auditMode + '--log-file "' + $auditLog + '" -- --verify-chapter' + $auditCaptureArgument
$auditWorkingDirectory = if ($Executable) { Split-Path -Parent $auditExecutable } else { $GameRoot }
$auditExit = [BackgroundVerificationDesktop]::Run($auditExecutable, $auditArguments, $auditWorkingDirectory)
$captureOutput = Get-Content -LiteralPath $auditLog -Encoding UTF8 -Raw
Write-Output $captureOutput
if ($auditExit -ne 0 -or $captureOutput -match '(?m)^ERROR:' -or $captureOutput -notmatch 'CHAPTER_VERIFIED' -or $captureOutput -notmatch 'UI_REWORK_VERIFIED') {
    throw "Background verification failed or did not finish: $auditExit"
}
