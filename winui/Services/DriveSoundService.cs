using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Box86_WinUI.Services
{
    public enum DriveSoundKind { Floppy, HardDisk }

    /// <summary>
    /// Optional, subtle drive seek ticks. Short PCM clicks are synthesized once and played
    /// through winmm PlaySound from memory (async, low latency). Off unless enabled in
    /// Manager Settings.
    /// </summary>
    public static class DriveSoundService
    {
        [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
        private static extern bool PlaySound(IntPtr pszSound, IntPtr hmod, uint fdwSound);

        private const uint SND_ASYNC = 0x0001, SND_NODEFAULT = 0x0002, SND_MEMORY = 0x0004;
        private const int SampleRate = 22050;

        private static readonly object Gate = new();
        private static bool? _enabled;
        private static GCHandle _floppyPin, _hddPin;
        private static long _lastTickMs;

        public static bool Enabled
        {
            get => _enabled ??= new ConfigService().GetDriveSeekSounds();
            set => _enabled = value;
        }

        public static event Action? SoundPlayed;

        public static void Seek(DriveSoundKind kind)
        {
            if (!Enabled) return;

            long now = Environment.TickCount64;
            // Rate limit so a sustained burst reads as a chitter, not a buzz.
            if (now - _lastTickMs < 70) return;
            _lastTickMs = now;

            try
            {
                lock (Gate)
                {
                    EnsureBuffers();
                    var pin = kind == DriveSoundKind.Floppy ? _floppyPin : _hddPin;
                    PlaySound(pin.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
                    SoundPlayed?.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriveSound] {ex.Message}");
            }
        }

        private static void EnsureBuffers()
        {
            if (_floppyPin.IsAllocated) return;
            // Floppy: low "thunk" (140 Hz body + noise), ~28 ms. HDD: tiny dry tick, ~12 ms.
            _floppyPin = GCHandle.Alloc(MakeWav(0.028, 0.006, 140, 0.6, 0.4, 0.26), GCHandleType.Pinned);
            _hddPin = GCHandle.Alloc(MakeWav(0.012, 0.0022, 1800, 0.3, 0.7, 0.14), GCHandleType.Pinned);
        }

        private static byte[] MakeWav(double seconds, double decay, double freq, double toneMix, double noiseMix, double amp)
        {
            int n = (int)(seconds * SampleRate);
            var rng = new Random(1986);
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write("RIFF"u8.ToArray()); w.Write(36 + n * 2); w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(SampleRate); w.Write(SampleRate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data"u8.ToArray()); w.Write(n * 2);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double env = Math.Exp(-t / decay);
                double v = env * amp * (toneMix * Math.Sin(2 * Math.PI * freq * t) + noiseMix * (rng.NextDouble() * 2 - 1));
                w.Write((short)(Math.Clamp(v, -1, 1) * short.MaxValue));
            }
            return ms.ToArray();
        }
    }
}
