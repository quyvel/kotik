using System;
using System.Runtime.InteropServices;

namespace Kotik
{
    /// The peak level of the sound that plays right now (0..1), from the Windows audio meter. Cheap enough for every frame.
    static class Audio
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints();                                   // not used: keeps the slot order
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object face);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
        }

        static IAudioMeterInformation meter;
        static DateTime retry;

        public static float Peak()
        {
            try
            {
                if (meter == null)
                {
                    if (DateTime.Now < retry) return 0;
                    retry = DateTime.Now.AddSeconds(10);
                    var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                    IMMDevice dev;
                    if (en.GetDefaultAudioEndpoint(0, 1, out dev) != 0 || dev == null) return 0;     // speakers, multimedia
                    var iid = typeof(IAudioMeterInformation).GUID;
                    object o;
                    if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) return 0;
                    meter = (IAudioMeterInformation)o;
                }
                float p;
                if (meter.GetPeakValue(out p) != 0) { meter = null; return 0; }
                return p;
            }
            catch { meter = null; return 0; }                          // headphones unplugged etc.: try again later
        }
    }
}
