#!/usr/bin/env python3
"""
Generates winui/Models/DeviceConfigs.g.cs: every device's device_config_t table from the 86Box sources,
keyed by the device's display name (the engine stores a device's options in [<device name>] in 86box.cfg,
with " #N" appended for a second instance).

  python tools/gen_device_configs.py
"""
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
OUT = os.path.join(ROOT, 'winui', 'Models', 'DeviceConfigs.g.cs')

TYPES = {
    'CONFIG_BINARY': 'Binary', 'CONFIG_SELECTION': 'Selection', 'CONFIG_SPINNER': 'Spinner',
    'CONFIG_MIDI_IN': 'MidiIn', 'CONFIG_MIDI_OUT': 'MidiOut', 'CONFIG_MIDI': 'MidiOut',
    'CONFIG_STRING': 'String', 'CONFIG_FNAME': 'File', 'CONFIG_BIOS': 'Bios', 'CONFIG_VARIANT': 'Bios',
    'CONFIG_HEX16': 'Hex16', 'CONFIG_HEX20': 'Hex20', 'CONFIG_MAC': 'Mac',
}


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def match_brace(text, i):
    """Index just past the brace block starting at text[i] == '{'."""
    depth = 0
    in_str = False
    j = i
    while j < len(text):
        c = text[j]
        if in_str:
            if c == '\\':
                j += 1
            elif c == '"':
                in_str = False
        elif c == '"':
            in_str = True
        elif c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0:
                return j + 1
        j += 1
    return len(text)


def split_entries(body):
    """Top-level { ... } blocks inside an initializer body (without its outer braces)."""
    out, i = [], 0
    while i < len(body):
        if body[i] == '{':
            j = match_brace(body, i)
            out.append(body[i:j])
            i = j
        else:
            i += 1
    return out


def num(s):
    s = s.strip()
    try:
        return int(s, 0)
    except ValueError:
        m = re.fullmatch(r'\(?\s*(0x[0-9a-fA-F]+|\d+)\s*(?:<<\s*(\d+))?\s*\)?', s)
        if m:
            v = int(m.group(1), 0)
            return v << int(m.group(2)) if m.group(2) else v
    return None


def field(entry, name):
    m = re.search(r'\.' + name + r'\s*=\s*("(?:[^"\\]|\\.)*"|[^,\n}]+)', entry)
    return m.group(1).strip() if m else None


def unq(v):
    if v is None or v == 'NULL':
        return None
    if v.startswith('"'):
        return bytes(v[1:-1], 'utf-8').decode('unicode_escape')
    return v


def sub_block(entry, name):
    m = re.search(r'\.' + name + r'\s*=\s*\{', entry)
    if not m:
        return ''
    start = m.end() - 1
    return entry[start:match_brace(entry, start)]


def parse_flags(flags):
    """BIOS_LIMIT_* flag expression -> (min, max, min2, max2), -1 when a limit is not set."""
    mn = mx = mn2 = mx2 = -1
    if not flags:
        return mn, mx, mn2, mx2
    if 'BIOS_LIMIT_MIN_MEMORY_2' in flags:
        m = re.search(r'\(\s*(\d+)\s*<<\s*16\s*\)', flags)
        mn2 = int(m.group(1)) if m else -1
    if 'BIOS_LIMIT_MAX_MEMORY_2' in flags:
        m = re.search(r'\(\s*(\d+)\s*<<\s*24\s*\)', flags)
        mx2 = int(m.group(1)) if m else -1
    if re.search(r'BIOS_LIMIT_MIN_MEMORY(?!_2)', flags):
        m = re.search(r'\|\s*(\d+)\b(?!\s*<<)', flags)
        mn = int(m.group(1)) if m else -1
    if re.search(r'BIOS_LIMIT_MAX_MEMORY(?!_2)', flags):
        m = re.search(r'\(\s*(\d+)\s*<<\s*8\s*\)', flags)
        mx = int(m.group(1)) if m else -1
    return mn, mx, mn2, mx2


def parse_config_table(body):
    items = []
    for e in split_entries(body):
        t = field(e, 'type')
        if t is None or t == 'CONFIG_END':
            continue
        name = unq(field(e, 'name'))
        if not name:
            continue
        kind = TYPES.get(t, 'Unknown')
        desc = unq(field(e, 'description')) or name
        dint = num(field(e, 'default_int') or '0') or 0
        dstr = unq(field(e, 'default_string')) or ''
        ffilter = unq(field(e, 'file_filter')) or ''

        sp = sub_block(e, 'spinner')
        smin = num(field(sp, 'min') or '0') or 0
        smax = num(field(sp, 'max') or '0') or 0
        sstep = num(field(sp, 'step') or '1') or 1

        sel = []
        for se in split_entries(sub_block(e, 'selection')[1:-1] if sub_block(e, 'selection') else ''):
            d = unq(field(se, 'description'))
            v = num(field(se, 'value') or '')
            if d and v is not None:
                sel.append((d, v))

        bios = []
        for be in split_entries(sub_block(e, 'bios')[1:-1] if sub_block(e, 'bios') else ''):
            bn = unq(field(be, 'name'))
            bi = unq(field(be, 'internal_name'))
            if bn and bi:
                mn, mx, mn2, mx2 = parse_flags(field(be, 'flags'))
                bios.append((bn, bi, mn, mx, mn2, mx2))

        items.append((name, desc, kind, dint, dstr, ffilter, smin, smax, sstep, sel, bios))
    return items


def main():
    tables = {}
    devices = {}   # device display name -> (internal, config symbol)
    for dp, _, files in os.walk(os.path.join(ROOT, 'src')):
        for f in files:
            if not f.endswith('.c') or 'qt' in dp.replace('\\', '/').split('/'):
                pass
            if not f.endswith('.c'):
                continue
            txt = open(os.path.join(dp, f), encoding='utf-8', errors='ignore').read()
            for m in re.finditer(r'device_config_t\s+(\w+)\s*\[\s*\]\s*=\s*\{', txt):
                start = m.end() - 1
                body = txt[start + 1:match_brace(txt, start) - 1]
                tables.setdefault(m.group(1), parse_config_table(body))
            for m in re.finditer(r'device_t\s+(\w+)\s*=\s*\{(.*?)\n\};', txt, re.S):
                b = m.group(2)
                n = re.search(r'\.name\s*=\s*"([^"]+)"', b)
                i = re.search(r'\.internal_name\s*=\s*"([^"]+)"', b)
                c = re.search(r'\.config\s*=\s*(\w+)', b)
                if n and i and c and c.group(1) != 'NULL':
                    devices.setdefault(n.group(1), (i.group(1), c.group(1)))

    out = ['// <auto-generated>', '// Generated by tools/gen_device_configs.py from the 86Box device_config_t tables. Do not edit.',
           '// </auto-generated>', 'using System.Collections.Generic;', '', 'namespace Box86_WinUI.Models', '{',
           '    public enum ConfigKind { Binary, Selection, Spinner, MidiIn, MidiOut, String, File, Bios, Hex16, Hex20, Mac, Unknown }', '',
           '    public readonly record struct ConfigBios(string Name, string Internal, int MinMem, int MaxMem, int MinMem2, int MaxMem2);',
           '    public readonly record struct ConfigChoice(string Label, int Value);', '',
           '    public sealed record ConfigItem(string Name, string Description, ConfigKind Kind, int DefaultInt, string DefaultString,',
           '                                    string FileFilter, int Min, int Max, int Step, ConfigChoice[] Choices, ConfigBios[] Bios);', '',
           '    public sealed record DeviceConfigEntry(string InternalName, ConfigItem[] Items);', '',
           '    public static class DeviceConfigCatalog', '    {',
           '        /// <summary>Device display name (= its [section] in 86box.cfg) -> its options.</summary>',
           '        public static readonly Dictionary<string, DeviceConfigEntry> Devices = new()', '        {']
    count = 0
    for name in sorted(devices):
        internal, sym = devices[name]
        items = tables.get(sym)
        if not items:
            continue
        count += 1
        out.append('            [%s] = new(%s, new ConfigItem[] {' % (cs(name), cs(internal)))
        for (n, d, k, di, ds, ff, mn, mx, st, sel, bios) in items:
            ch = ', '.join('new(%s, %d)' % (cs(a), b) for a, b in sel)
            bi = ', '.join('new(%s, %s, %d, %d, %d, %d)' % (cs(a), cs(b), c, d2, e, f) for a, b, c, d2, e, f in bios)
            out.append('                new(%s, %s, ConfigKind.%s, %d, %s, %s, %d, %d, %d, new ConfigChoice[] { %s }, new ConfigBios[] { %s }),'
                       % (cs(n), cs(d), k, di, cs(ds), cs(ff), mn, mx, st, ch, bi))
        out.append('            }),')
    out += ['        };', '    }', '}', '']
    open(OUT, 'w', encoding='utf-8').write('\n'.join(out))
    print('devices with options: %d (of %d device tables)' % (count, len(tables)))


if __name__ == '__main__':
    main()
