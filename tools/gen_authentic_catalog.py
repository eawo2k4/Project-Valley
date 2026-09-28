#!/usr/bin/env python3
"""Generates winui/Models/AuthenticCatalog.g.cs from the 86Box engine sources so that the
WinUI config writer only ever emits internal identifiers the engine really understands.

  * CPU families: internal_name -> [(name, rspeed, multi)] from src/cpu/cpu_table.c
    (86box.cfg stores cpu_family + cpu_speed (Hz) + cpu_multi, never a display label)
  * Devices: (kind, internal_name, display name) from every device_t under the relevant
    source directories (video, sound, network, scsi, disk, floppy, device)

Run from anywhere:  python tools/gen_authentic_catalog.py
"""
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'winui', 'Models', 'AuthenticCatalog.g.cs')

BACKSLASH = chr(92)
QUOTE = chr(34)


def cs(s):
    return QUOTE + s.replace(BACKSLASH, BACKSLASH * 2).replace(QUOTE, BACKSLASH + QUOTE) + QUOTE


def pkg_list(expr):
    return [t.strip() for t in expr.split('|') if t.strip()]


def parse_cpus():
    text = open(os.path.join(ROOT, 'src', 'cpu', 'cpu_table.c'), encoding='utf-8', errors='ignore').read()
    text = text[text.index('cpu_families[]'):]
    fams = []
    parts = re.split(r'\.package\s*=', text)
    for part in parts[1:]:
        pm = re.match(r'\s*([A-Za-z0-9_ |]+?)\s*,', part)
        im = re.search(r'\.internal_name\s*=\s*"([^"]+)"', part)
        if not pm or not im:
            continue
        nm = re.search(r'\.name\s*=\s*"([^"]*)"', part)
        mf = re.search(r'\.manufacturer\s*=\s*"([^"]*)"', part)
        display = ((mf.group(1) + ' ') if mf else '') + (nm.group(1) if nm else im.group(1))
        # only this family's own text (up to the next family's ".package" is already ensured by the split)
        body = part[part.find('.cpus'):]
        cpus = []
        for cm in re.finditer(r'\.name\s*=\s*"([^"]*)"(.*?)\.rspeed\s*=\s*([0-9_]+)\s*,(.*?)\.multi\s*=\s*([0-9.]+)', body, re.S):
            if '.name' in cm.group(2):
                continue
            cpus.append((cm.group(1), int(cm.group(3).replace('_', '')), float(cm.group(5))))
        fams.append((im.group(1), display, pkg_list(pm.group(1)), cpus))
    return fams


def parse_machine_packages():
    """machine internal_name -> CPU_PKG_* list, from machine_table.c."""
    text = open(os.path.join(ROOT, 'src', 'machine', 'machine_table.c'), encoding='utf-8', errors='ignore').read()
    out = {}
    for blk in re.split(r'\n    \{\n\s*\.name\s*=', text)[1:]:
        im = re.search(r'\.internal_name\s*=\s*"([^"]+)"', blk)
        pm = re.search(r'\.cpu\s*=\s*\{\s*\.package\s*=\s*([A-Za-z0-9_ |]+?)\s*,', blk)
        if im and pm and im.group(1) not in out:
            out[im.group(1)] = pkg_list(pm.group(1))
    return out


_SYMBOLS = None


def all_device_symbols():
    """device_t symbol -> (internal_name, name) across the whole source tree."""
    global _SYMBOLS
    if _SYMBOLS is None:
        _SYMBOLS = {}
        for dp, _, files in os.walk(os.path.join(ROOT, 'src')):
            for f in files:
                if not f.endswith(('.c', '.cpp')):
                    continue
                txt = open(os.path.join(dp, f), encoding='utf-8', errors='ignore').read()
                for m in re.finditer(r'device_t\s+(\w+)\s*=\s*\{(.*?)\n\};', txt, re.S):
                    n = re.search(r'\.name\s*=\s*"([^"]+)"', m.group(2))
                    i = re.search(r'\.internal_name\s*=\s*"([^"]+)"', m.group(2))
                    if n and i:
                        _SYMBOLS.setdefault(m.group(1), (i.group(1), n.group(1)))
    return _SYMBOLS


def symbol_table(rel, array_name):
    """Ordered device symbols referenced by a table such as "boards[] = { { &dev }, ... };"."""
    txt = open(os.path.join(ROOT, rel), encoding='utf-8', errors='ignore').read()
    m = re.search(re.escape(array_name) + r'\s*\[\s*\]\s*=\s*\{', txt)
    body = txt[m.end():]
    body = body[:body.index('\n};')]
    return re.findall(r'&\s*(\w+)', body)


def resolve_symbols(symbols):
    syms = all_device_symbols()
    return [(syms[s][0], syms[s][1], s) for s in symbols if s in syms]


KINDS = {
    'video': ['video'],
    'sound': ['sound'],
    'network': ['network'],
    'scsi': ['scsi'],
    'hdc': ['disk', 'scsi', 'device'],
    'fdc': ['floppy'],
    'input': ['device'],
    'cdrom_if': ['cdrom'],
}


def parse_devices(dirs):
    found = {}
    for d in dirs:
        base = os.path.join(ROOT, 'src', d)
        for dp, _, files in os.walk(base):
            for f in files:
                if not f.endswith(('.c', '.cpp')):
                    continue
                txt = open(os.path.join(dp, f), encoding='utf-8', errors='ignore').read()
                for m in re.finditer(r'device_t\s+\w+\s*=\s*\{(.*?)\n\};', txt, re.S):
                    b = m.group(1)
                    n = re.search(r'\.name\s*=\s*"([^"]+)"', b)
                    i = re.search(r'\.internal_name\s*=\s*"([^"]+)"', b)
                    # ymfm chip cores (snd_ymfm.cpp) are building blocks of the real sound cards, not selectable cards.
                    if n and i and i.group(1) not in found and not i.group(1).endswith(('_ymfm', '_opl2board_device')):
                        found[i.group(1)] = n.group(1)
    return found


def parse_named(relpath):
    """Entries of the form { .name = "...", ... .internal_name = "..." } in one source file."""
    txt = open(os.path.join(ROOT, relpath), encoding='utf-8', errors='ignore').read()
    found = {}
    for m in re.finditer(r'\{[^{}]*?\.name\s*=\s*"([^"]*)"[^{}]*?\.internal_name\s*=\s*"([^"]*)"[^{}]*?\}', txt, re.S):
        if m.group(2) not in found:
            found[m.group(2)] = m.group(1)
    return found


NAMED_TABLES = [
    ('HddPresets', 'src/disk/hdd.c'),
]


def parse_drive_table(rel, array_name):
    """Rows of a static drive-type table: { "vendor", "model", "revision", ... }. The config
    stores the row INDEX ("mo_01_parameters = 0, scsi"), so order matters."""
    txt = open(os.path.join(ROOT, rel), encoding='utf-8', errors='ignore').read()
    txt = txt[txt.index(array_name + '['):]
    txt = txt[:txt.index('};')]
    if '#if 0' in txt:
        txt = txt[:txt.index('#if 0')]
    rows = []
    for m in re.finditer(r'\{\s*"([^"]*)"\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"([^\n]*)', txt):
        label = ' '.join(x for x in (m.group(1), m.group(2), m.group(3)) if x).strip()
        bus = ''
        tm = re.search(r'TAPE_DRIVE_TYPE_(\w+)', m.group(4))
        if tm:
            bus = {'LPT': 'lpt', 'FDC': 'fdc', 'SCSI_ATAPI': 'scsi', 'SCSI': 'scsi', 'ATAPI': 'atapi'}.get(tm.group(1), '')
        rows.append((label, bus))
    return rows


def parse_floppy_types():
    """fdd.c drive_types[]: { max_track, flags, "name", "internal_name" } rows."""
    txt = open(os.path.join(ROOT, 'src', 'floppy', 'fdd.c'), encoding='utf-8', errors='ignore').read()
    txt = txt[txt.index('drive_types[]'):]
    txt = txt[:txt.index('};')]
    found = {}
    for m in re.finditer(r'"((?:[^"\\]|\\.)*)"\s*,\s*"([a-z0-9_]+)"\s*\}', txt):
        found.setdefault(m.group(2), m.group(1).replace(BACKSLASH + QUOTE, QUOTE))
    return found


def main():
    fams = parse_cpus()
    out = [
        '// <auto-generated> by tools/gen_authentic_catalog.py from the 86Box sources. Do not edit. </auto-generated>',
        'using System.Collections.Generic;',
        '',
        'namespace Box86_WinUI.Models',
        '{',
        '    public readonly record struct CpuStep(string Name, long RSpeedHz, double Multi);',
        '    public sealed record CpuFamily(string Display, string[] Packages, CpuStep[] Steps);',
        '',
        '    public static class AuthenticCatalog',
        '    {',
        '        /// <summary>cpu_family internal name -> selectable CPUs (cpu_speed is RSpeedHz, plus cpu_multi).</summary>',
        '        public static readonly Dictionary<string, CpuFamily> CpuFamilies = new()',
        '        {',
    ]
    ncpu = 0
    for internal, display, packages, cpus in fams:
        pk = ', '.join(cs(x) for x in packages)
        out.append('            [%s] = new CpuFamily(%s, new[] { %s }, new CpuStep[] {' % (cs(internal), cs(display), pk))
        for n, r, mu in cpus:
            out.append('                new(%s, %dL, %r),' % (cs(n), r, mu))
            ncpu += 1
        out.append('            }),')
    out.append('        };')
    out.append('')
    mp = parse_machine_packages()
    out.append('        /// <summary>machine internal_name -> CPU_PKG_* the machine accepts (machine_table.c).</summary>')
    out.append('        public static readonly Dictionary<string, string[]> MachinePackages = new()')
    out.append('        {')
    for i, pk in sorted(mp.items()):
        out.append('            [%s] = new[] { %s },' % (cs(i), ', '.join(cs(x) for x in pk)))
    out.append('        };')
    out.append('')
    print('  MachinePackages: %d' % len(mp))
    out.append('        /// <summary>device class -> (internal_name, engine display name).</summary>')
    out.append('        public static readonly Dictionary<string, (string Internal, string Name)[]> Devices = new()')
    out.append('        {')
    total = 0
    for kind, dirs in KINDS.items():
        devs = parse_devices(dirs)
        out.append('            [%s] = new (string, string)[] {' % cs(kind))
        for i, n in sorted(devs.items()):
            out.append('                (%s, %s),' % (cs(i), cs(n)))
            total += 1
        out.append('            },')
    # Devices enumerated by an engine table rather than a directory.
    char = resolve_symbols(symbol_table('src/char/char.c', 'char_devices'))
    lpt_end = [i for i, x in enumerate(char) if x[2] == 'char_loopback_lpt_device']
    split = (lpt_end[0] + 1) if lpt_end else len(char)
    tabled = {
        'char_lpt': [x for x in char[:split]],
        'char_com': [x for x in char[split:]],
        'isartc': resolve_symbols(symbol_table('src/device/isartc.c', 'boards')),
        'isamem': resolve_symbols(symbol_table('src/device/isamem.c', 'boards')),
        'mcamem': resolve_symbols(symbol_table('src/device/mcamem.c', 'mcamem_cards')),
        'isarom': resolve_symbols(symbol_table('src/device/isarom.c', 'boards')),
    }
    # "none" is valid on either port type.
    for k in ('char_lpt', 'char_com'):
        if not any(x[0] == 'none' for x in tabled[k]):
            tabled[k].insert(0, ('none', 'None', 'char_none_device'))
    for kind, entries in tabled.items():
        out.append('            [%s] = new (string, string)[] {' % cs(kind))
        for i, n, _ in entries:
            out.append('                (%s, %s),' % (cs(i), cs(n)))
            total += 1
        out.append('            },')
        print('  %s: %d' % (kind, len(entries)))
    out.append('        };')
    out.append('')
    for name, rel, arr in (('MoDriveTypes', 'src/include/86box/mo.h', 'mo_drive_types'),
                           ('RdiskDriveTypes', 'src/include/86box/rdisk.h', 'rdisk_drive_types'),
                           ('TapeDriveTypes', 'src/include/86box/scsi_tape.h', 'tape_drive_types')):
        rows = parse_drive_table(rel, arr)
        out.append('        /// <summary>%s, in engine index order (%s); Bus is the tape medium bus, if fixed.</summary>' % (name, rel))
        out.append('        public static readonly (string Label, string Bus)[] %s = new (string, string)[] {' % name)
        for label, bus in rows:
            out.append('            (%s, %s),' % (cs(label), cs(bus)))
        out.append('        };')
        out.append('')
        print('  %s: %d' % (name, len(rows)))
    ftypes = parse_floppy_types()
    out.append('        /// <summary>fdd.c drive_types[]: (internal_name, engine display name).</summary>')
    out.append('        public static readonly (string Internal, string Name)[] FloppyTypes = new (string, string)[] {')
    for i, n in ftypes.items():
        out.append('            (%s, %s),' % (cs(i), cs(n)))
    out.append('        };')
    out.append('')
    print('  FloppyTypes: %d' % len(ftypes))
    for name, rel in NAMED_TABLES:
        named = parse_named(rel)
        out.append('        /// <summary>%s: (internal_name, engine display name) from %s.</summary>' % (name, rel))
        out.append('        public static readonly (string Internal, string Name)[] %s = new (string, string)[] {' % name)
        for i, n in named.items():
            out.append('            (%s, %s),' % (cs(i), cs(n)))
        out.append('        };')
        out.append('')
        print('  %s: %d' % (name, len(named)))
    out.append('    }')
    out.append('}')
    open(OUT, 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    print('%d CPU families, %d CPUs, %d device entries -> %s' % (len(fams), ncpu, total, OUT))


main()
