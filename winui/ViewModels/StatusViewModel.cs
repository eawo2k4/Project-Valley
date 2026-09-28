using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Box86_WinUI.Models;
using Box86_WinUI.Services;

namespace Box86_WinUI.ViewModels
{
    /// <summary>
    /// Bindable state of the bottom status bar's hardware indicators that comes from the machine's
    /// 86box.cfg: which hard disks exist, and which network card is configured and how it is connected.
    /// (Live activity flags stay on <see cref="VirtualMachine"/>: IsHddActive / IsNetActive.)
    /// </summary>
    public sealed class StatusViewModel : ObservableObject
    {
        private readonly VirtualMachine _vm;

        private string _hddTooltip = "Hard disk";
        public string HddTooltip { get => _hddTooltip; private set => SetProperty(ref _hddTooltip, value); }

        private string _netTooltip = "Network";
        public string NetTooltip { get => _netTooltip; private set => SetProperty(ref _netTooltip, value); }

        private bool _hasHardDisk;
        /// <summary>The HDD icon is muted when the machine has no hard disk.</summary>
        public double HddOpacity => _hasHardDisk ? 1.0 : 0.5;

        private bool _netConnected;
        /// <summary>0.5 when the adapter is disabled or has no host connection; 1.0 when connected.</summary>
        public double NetOpacity => _netConnected ? 1.0 : 0.5;

        public StatusViewModel(VirtualMachine vm)
        {
            _vm = vm;
            Refresh();
        }

        /// <summary>Re-reads 86box.cfg (call after the settings were saved or the VM started).</summary>
        public void Refresh()
        {
            try
            {
                if (!File.Exists(_vm.ConfigFilePath)) return;
                var cfg = MachineConfig.Load(_vm.ConfigFilePath);
                RefreshHardDisks(cfg);
                RefreshNetwork(cfg);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StatusViewModel] refresh failed: {ex.Message}");
            }
        }

        private void RefreshHardDisks(MachineConfig cfg)
        {
            var lines = new List<string>();
            for (int n = 1; n <= 30; n++)
            {
                string k = $"hdd_{n:D2}_";
                if (!Cfg86Reader.TryParseHdd(cfg.GetString("Hard disks", k + "parameters", ""), out int spt, out int hpc, out int tracks, out string bus))
                    continue;

                long bytes = (long)tracks * hpc * spt * 512L;
                string size = bytes >= 1024L * 1024 * 1024
                    ? $"{bytes / (1024.0 * 1024 * 1024):0.##} GB"
                    : $"{bytes / (1024.0 * 1024):0.#} MB";

                string channel = bus switch
                {
                    "ide" or "atapi" => " " + Cfg86Reader.ChannelLabel(cfg.GetString("Hard disks", k + "ide_channel", "")),
                    "scsi" => " " + Cfg86Reader.ChannelLabel(cfg.GetString("Hard disks", k + "scsi_location", "")),
                    _ => string.Empty
                };

                string fn = cfg.GetString("Hard disks", k + "fn", "");
                string path = fn.Length == 0 ? "(no image)"
                    : Path.IsPathRooted(fn) ? fn : Path.Combine(_vm.Path, fn);

                lines.Add($"HDD {lines.Count + 1} ({Cfg86Reader.BusLabel(bus)}{channel}) - {size}\n{path}");
            }

            _hasHardDisk = lines.Count > 0;
            HddTooltip = lines.Count > 0 ? string.Join("\n\n", lines) : "No hard disks configured";
            OnPropertyChanged(nameof(HddOpacity));
        }

        private static string ModeLabel(string type) => type.ToLowerInvariant() switch
        {
            "slirp" => "SLiRP",
            "pcap" => "PCap",
            "vde" => "VDE",
            "tap" => "TAP",
            "nlswitch" => "Local switch",
            "nrswitch" => "Remote switch",
            _ => string.Empty
        };

        // net_NN_link (config.c): the default is all speeds up; a set NET_LINK_DOWN bit (2) means unplugged.
        private const int NetLinkDown = 2;
        private const int NetLinkDefault = 8 | 16 | 32 | 64 | 128 | 256;

        /// <summary>A configured network card: its slot in the engine, name, host connection and cable state.</summary>
        public sealed record NetCard(int Slot, string Name, string Mode, bool LinkUp);

        private List<NetCard> _netCards = new();
        public IReadOnlyList<NetCard> NetCards => _netCards;

        private void RefreshNetwork(MachineConfig cfg)
        {
            var cards = new List<NetCard>();
            for (int i = 1; i <= 4; i++)
            {
                string k = $"net_{i:D2}_";
                string card = cfg.GetString("Network", k + "card", "");
                if (card.Length == 0 || card.Equals("none", StringComparison.OrdinalIgnoreCase)) continue;

                string name = Cfg86Reader.DeviceLabel("network", card, System.Array.Empty<string>());
                string mode = ModeLabel(cfg.GetString("Network", k + "net_type", ""));
                bool linkUp = (cfg.GetInt("Network", k + "link", NetLinkDefault) & NetLinkDown) == 0;
                cards.Add(new NetCard(i - 1, name, mode, linkUp));
            }

            _netCards = cards;
            UpdateNetworkState();
        }

        /// <summary>
        /// Pulls the running engine's real cable state into the indicator so it can never disagree with the VM
        /// (for example after a guest-side or config-side change).
        /// </summary>
        public void SyncLinkState(Func<int, bool?> isConnected)
        {
            bool changed = false;
            for (int i = 0; i < _netCards.Count; i++)
            {
                var c = _netCards[i];
                if (isConnected(c.Slot) is { } live && live != c.LinkUp)
                {
                    _netCards[i] = c with { LinkUp = live };
                    changed = true;
                }
            }
            if (changed) UpdateNetworkState();
        }

        /// <summary>Records a connect/disconnect the user just made (the engine call is done by the caller).</summary>
        public void SetLink(int slot, bool up)
        {
            for (int i = 0; i < _netCards.Count; i++)
                if (_netCards[i].Slot == slot) _netCards[i] = _netCards[i] with { LinkUp = up };
            UpdateNetworkState();
        }

        /// <summary>Persists the cable state as the engine does: net_NN_link, absent when the link is up.</summary>
        public void SaveLink(int slot, bool up)
        {
            var cfg = MachineConfig.Load(_vm.ConfigFilePath);
            string key = $"net_{slot + 1:D2}_link";
            if (up) cfg.RemoveKey("Network", key);
            else cfg.SetInt("Network", key, NetLinkDefault | NetLinkDown);
            cfg.Save(_vm.ConfigFilePath);
        }

        private void UpdateNetworkState()
        {
            _netConnected = _netCards.Any(c => c.Mode.Length > 0 && c.LinkUp);

            NetTooltip = _netCards.Count == 0
                ? "No network adapter configured"
                : string.Join("\n", _netCards.Select(c =>
                    $"{c.Name} - {(c.Mode.Length == 0 ? "disconnected (null driver)" : c.Mode)}" +
                    (c.Mode.Length > 0 ? (c.LinkUp ? " - connected" : " - cable unplugged") : string.Empty)));

            OnPropertyChanged(nameof(NetOpacity));
            OnPropertyChanged(nameof(NetCards));
        }
    }
}
