using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;

namespace Box86_WinUI.Services
{
    public enum DeviceChannel { Fdd, Cd, Hdd, Net, Snd }

    /// <summary>
    /// Turns instantaneous "the device did I/O" events into a stable on/off state per channel. Every
    /// <see cref="SignalActivity"/> marks the channel active and (re)starts that channel's own 180 ms
    /// DispatcherQueueTimer; when it expires without a new pulse the channel returns to idle.
    /// Must be created on the UI thread; signals from other threads are marshalled through the DispatcherQueue.
    /// </summary>
    public sealed class DeviceTelemetryService : ObservableObject, IDisposable
    {
        public static readonly TimeSpan DecayInterval = TimeSpan.FromMilliseconds(120);

        private readonly DispatcherQueue _queue;
        private readonly Dictionary<DeviceChannel, DispatcherQueueTimer> _timers = new();
        private readonly Dictionary<DeviceChannel, bool> _active = new();

        public DeviceTelemetryService(DispatcherQueue queue)
        {
            _queue = queue;
            foreach (DeviceChannel ch in Enum.GetValues(typeof(DeviceChannel)))
            {
                _active[ch] = false;
                var channel = ch;
                var timer = queue.CreateTimer();
                timer.Interval = DecayInterval;
                timer.IsRepeating = false;
                timer.Tick += (_, _) => SetActive(channel, false);
                _timers[ch] = timer;
            }
        }

        public bool FddActive => _active[DeviceChannel.Fdd];
        public bool CdActive => _active[DeviceChannel.Cd];
        public bool HddActive => _active[DeviceChannel.Hdd];
        public bool NetActive => _active[DeviceChannel.Net];
        public bool SndActive => _active[DeviceChannel.Snd];

        public bool IsActive(DeviceChannel channel) => _active[channel];

        /// <summary>Raised on the UI thread whenever a channel goes active or idle.</summary>
        public event Action<DeviceChannel, bool>? ChannelChanged;

        /// <summary>Marks the channel active and restarts its decay timer. Safe to call from any thread.</summary>
        public void SignalActivity(DeviceChannel channel)
        {
            if (!_queue.HasThreadAccess)
            {
                _queue.TryEnqueue(() => SignalActivity(channel));
                return;
            }

            SetActive(channel, true);
            var timer = _timers[channel];
            timer.Stop();   // a new pulse restarts the 180 ms window
            timer.Start();
        }

        /// <summary>Returns a channel (or all of them) to idle immediately, e.g. when the VM stops.</summary>
        public void Reset(DeviceChannel? channel = null)
        {
            if (!_queue.HasThreadAccess)
            {
                _queue.TryEnqueue(() => Reset(channel));
                return;
            }

            foreach (var ch in channel is { } c ? new[] { c } : (DeviceChannel[])Enum.GetValues(typeof(DeviceChannel)))
            {
                _timers[ch].Stop();
                SetActive(ch, false);
            }
        }

        private void SetActive(DeviceChannel channel, bool active)
        {
            if (_active[channel] == active) return;
            _active[channel] = active;

            OnPropertyChanged(channel switch
            {
                DeviceChannel.Fdd => nameof(FddActive),
                DeviceChannel.Cd => nameof(CdActive),
                DeviceChannel.Hdd => nameof(HddActive),
                DeviceChannel.Net => nameof(NetActive),
                _ => nameof(SndActive)
            });
            ChannelChanged?.Invoke(channel, active);
        }

        public void Dispose()
        {
            foreach (var t in _timers.Values) t.Stop();
        }
    }
}
