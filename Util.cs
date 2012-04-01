﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Forms.Application;

namespace Power8
{
    static class Util
    {
        public static Dispatcher MainDisp;

        private static readonly StringBuilder Buffer = new StringBuilder(1024);



        public static IntPtr GetHandle(this Window w)
        {
            return new WindowInteropHelper(w).Handle;
        }

        public static HwndSource GetHwndSource(this Window w)
        {
            return HwndSource.FromHwnd(w.GetHandle());
        }

        public static IntPtr MakeGlassWpfWindow(this Window w)
        {
            var source = w.GetHwndSource();
            if (source.CompositionTarget != null) 
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
            if (Environment.OSVersion.Version.Major >= 6) 
                MakeGlass(source.Handle);
            return source.Handle;
        }
        
        public static void MakeGlass(IntPtr hWnd)
        {
            var bbhOff = new API.DwmBlurbehind
                            {
                                dwFlags = API.DwmBlurbehind.DWM_BB_ENABLE | API.DwmBlurbehind.DWM_BB_BLURREGION,
                                fEnable = false,
                                hRegionBlur = IntPtr.Zero
                            };
            API.DwmEnableBlurBehindWindow(hWnd, bbhOff);
            API.DwmExtendFrameIntoClientArea(hWnd, new API.Margins { cxLeftWidth = -1, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 });
        }

        public static void RegisterHook(this Window w, HwndSourceHook hook)
        {
            w.GetHwndSource().AddHook(hook);
        }
            


        public static IntPtr GetIconForFile(string file, API.Shgfi iconType)
        {
            var shinfo = new API.Shfileinfo();
            var hImgSmall = API.SHGetFileInfo(file, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), (uint)(API.Shgfi.SHGFI_ICON | iconType));
            return hImgSmall == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero ? IntPtr.Zero : shinfo.hIcon;
        }

        public static string ResolveLink(string link)
        {
            var shLink = new API.ShellLink();
            ((API.IPersistFile)shLink).Load(link, 0);
            lock (Buffer)
            {
                API.WIN32_FIND_DATAW sd;
                ((API.IShellLink) shLink).GetPath(Buffer, 512, out sd, API.SLGP_FLAGS.SLGP_UNCPRIORITY);
                return Buffer.ToString();
            }
        }

        public static string ResolveResource(string localizeableResourceId)
        {
            //ResId = %ProgramFiles%\Windows Defender\EppManifest.dll,-1000
            var lastCommaIdx = localizeableResourceId.LastIndexOf(',');
            var resDll = Environment.ExpandEnvironmentVariables(localizeableResourceId.Substring(0, lastCommaIdx));
            var resId = uint.Parse(localizeableResourceId.Substring(lastCommaIdx + 2));
            var dllHandle = API.LoadLibrary(resDll, IntPtr.Zero,
                                            API.LLF.LOAD_LIBRARY_AS_DATAFILE | API.LLF.LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (dllHandle != IntPtr.Zero)
            {
                lock (Buffer)
                {
                    var number = API.LoadString(dllHandle, resId, Buffer, Buffer.Capacity);
                    API.FreeLibrary(dllHandle);
                    if (number > 0)
                        return Buffer.ToString();
                }
            }
            return null;
        }

        public class ShellExecuteHelper
        {
            private readonly API.ShellExecuteInfo _executeInfo;
            private int _errorCode;
            private bool _succeeded;

            public int ErrorCode{get { return _errorCode; }}

            public ShellExecuteHelper(API.ShellExecuteInfo executeInfo)
            {
                _executeInfo = executeInfo;
            }

            private void ShellExecuteFunction()
            {
                if ((_succeeded = API.ShellExecuteEx(_executeInfo)) == true)
                    return;
                _errorCode = Marshal.GetLastWin32Error();
            }

            public bool ShellExecuteOnSTAThread()
            {
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                {
                    var thread = new Thread(ShellExecuteFunction);
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                }
                else
                    ShellExecuteFunction();
                return _succeeded;
            }
        }


        
        public static void Restart(string reason)
        {
            Process.Start("explorer.exe");
            Process.Start(Application.ExecutablePath);
            Die(reason);
        }

        public static void Die(string becauseString)
        {
            Environment.FailFast(string.Format(Properties.Resources.FailFastFormat, becauseString));
        }
    }
}
