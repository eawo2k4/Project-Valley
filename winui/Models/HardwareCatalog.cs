using System;
using System.Collections.Generic;
using System.Linq;

namespace Box86_WinUI.Models
{
    public record MachinePreset(
        string Id,
        string Name,
        string Description,
        string Era,
        string MachineFamily,
        string MachineModel,
        string Cpu,
        int MemoryMb,
        string VideoCard,
        string SoundCard,
        string FloppyType,
        int HardDiskMb,
        string RecommendedOS
    );

    public record MachineInfo(
        string Name, 
        string InternalName, 
        int DefaultRamMb, 
        int MaxRamMb,
        string CpuPackage = "",
        int MinRamKb = 0,
        int MaxRamKb = 0,
        int StepRamKb = 0
    );

    public record CpuInfo(
        string Name, 
        string InternalName, 
        List<string> CompatibleSockets, 
        List<string> Frequencies, 
        List<string> SupportedFpus
    );

    public record OpticalDriveDevice(
        string InternalName,
        string DisplayName,
        string BusType,
        int Speed,
        bool IsDvd
    )
    {
        public override string ToString() => DisplayName;
    }

    public static class HardwareCatalog
    {
        // 27 authentic machine types from 86Box machine_table.c
        public static readonly List<string> MachineTypes = new()
        {
            "[1979] 8088",
            "[1978] 8086",
            "[1982] 80286",
            "[1988] i386SX",
            "[1997] ALi M6117",
            "[1992] 486SLC",
            "[1985] i386DX",
            "[1989] i386DX/i486",
            "[1989] Socket 168/1",
            "[1992] Socket 2",
            "[1993] Socket 3",
            "[1994] Socket 3 (PCI)",
            "[1993] Socket 3/4",
            "[1999] STMicroelectronics STPC",
            "[1993] Socket 4",
            "[1994] Socket 4/5",
            "[1994] Socket 5",
            "[1995] Socket 7 (Single Voltage)",
            "[1996] Socket 7 (Dual Voltage)",
            "[1998] Super Socket 7",
            "[1995] Socket 8",
            "[1997] Slot 1",
            "[1998] Slot 1/2",
            "[1998] Slot 1/Socket 370",
            "[1998] Slot 2",
            "[1998] Socket 370",
            "Miscellaneous",
        };

        // Machines grouped by Machine Type (520 total authentic boards from 86Box machine_table.c)
        public static readonly Dictionary<string, List<MachineInfo>> MachinesByType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["[1979] 8088"] = new()
            {
                new("[8088] IBM PC (1981)", "ibmpc", 1, 1, "CPU_PKG_8088", 16, 64, 16),
                new("[8088] IBM PC (1982)", "ibmpc82", 1, 1, "CPU_PKG_8088", 64, 256, 64),
                new("[8088] IBM PCjr", "ibmpcjr", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] IBM PC JX", "ibmpcjx", 1, 1, "CPU_PKG_8088", 128, 0, 0),
                new("[8088] IBM PC Convertible", "ibm5140", 1, 1, "CPU_PKG_80C88", 256, 0, 0),
                new("[8088] IBM XT (1982)", "ibmxt", 1, 1, "CPU_PKG_8088", 64, 256, 64),
                new("[8088] IBM XT (1986)", "ibmxt86", 1, 1, "CPU_PKG_8088", 256, 640, 64),
                new("[8088] IBM 3270 PC", "ibm3270pc", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] American XT Computer", "americxt", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] AMI XT clone", "amixt", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Atari PC 3", "ataripc3", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Bondwell BW230", "bw230", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] CDP MPC-1600", "mpc1600", 1, 1, "CPU_PKG_8088", 128, 512, 64),
                new("[8088] Compaq Portable", "portable", 1, 1, "CPU_PKG_8088", 128, 640, 128),
                new("[8088] DTK PIM-TB10-Z", "dtk", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Eagle PC Spirit", "pcspirit", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Generic XT clone", "genxt", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] GLaBIOS", "glabios", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Hyosung Topstar 88T", "top88", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Hyundai SUPER-16T", "super16t", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Hyundai SUPER-16TE", "super16te", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Juko ST", "jukopc", 1, 1, "CPU_PKG_8088", 512, 1024, 128),
                new("[8088] Kaypro PC", "kaypropc", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Multitech PC-500", "pc500", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Multitech PC-500+", "pc500plus", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Multitech PC-700", "pc700", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] NCR PC4i", "pc4i", 1, 1, "CPU_PKG_8088", 256, 640, 128),
                new("[8088] Olivetti M19", "m19", 1, 1, "CPU_PKG_8088", 256, 640, 128),
                new("[8088] OpenXT", "openxt", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Philips P3105/NMS9100", "p3105", 1, 1, "CPU_PKG_8088", 256, 768, 256),
                new("[8088] Phoenix XT clone", "pxxt", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Pravetz 16", "pravetz16", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Pravetz-16ES", "mxl7t", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Pravetz 16S", "pravetz16s", 1, 1, "CPU_PKG_8088", 512, 1024, 128),
                new("[8088] Samsung SPC-3000V", "pb8810", 1, 1, "CPU_PKG_8088", 256, 640, 128),
                new("[8088] Sanyo SX-16", "sansx16", 1, 1, "CPU_PKG_8088", 256, 640, 128),
                new("[8088] Schneider EuroPC", "europc", 1, 1, "CPU_PKG_8088_EUROPC", 512, 640, 128),
                new("[8088] Super PC", "pcxt", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Tandy 1000 SX", "tandy1000sx", 1, 1, "CPU_PKG_8088", 384, 640, 128),
                new("[8088] Tandy 1000 HX", "tandy1000hx", 1, 1, "CPU_PKG_8088", 256, 640, 128),
                new("[8088] Thomson TO16", "to16", 1, 1, "CPU_PKG_8088", 64, 768, 64),
                new("[8088] Toshiba T1000", "t1000", 1, 1, "CPU_PKG_8088", 512, 1280, 768),
                new("[8088] Vendex HeadStart Turbo 888-XT", "vendex", 1, 1, "CPU_PKG_8088", 256, 768, 256),
                new("[8088] VTech Laser Turbo XT", "ltxt", 1, 1, "CPU_PKG_8088_VTECH", 256, 640, 64),
                new("[8088] Xi8088", "xi8088", 1, 1, "CPU_PKG_8088", 64, 1024, 64),
                new("[8088] Z-NIX PC-1600", "znic", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[8088] Zenith Data Systems Z-151", "zdsz151", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Zenith Data Systems Z-159", "zdsz159", 1, 1, "CPU_PKG_8088", 128, 640, 64),
                new("[8088] Zenith Data Systems Z-184", "zdsupers", 1, 1, "CPU_PKG_8088", 128, 640, 128),
                new("[GC100A] Philips P3120", "p3120", 1, 1, "CPU_PKG_8088", 256, 768, 256),
                new("[V20] PC-XT", "v20xt", 1, 1, "CPU_PKG_8088", 64, 640, 64),
                new("[V20] Tulip PC Compact 2", "tuliptc8", 1, 1, "CPU_PKG_8088", 640, 640, 640),
            },
            ["[1978] 8086"] = new()
            {
                new("[8086] Amstrad PC1512", "pc1512", 1, 1, "CPU_PKG_8086", 512, 640, 128),
                new("[8086] Amstrad PC1640", "pc1640", 1, 1, "CPU_PKG_8086", 640, 640, 640),
                new("[8086] Amstrad PC2086", "pc2086", 1, 1, "CPU_PKG_8086", 640, 640, 640),
                new("[8086] Amstrad PC3086", "pc3086", 1, 1, "CPU_PKG_8086", 640, 640, 640),
                new("[8086] Amstrad PC5086", "pc5086", 1, 1, "CPU_PKG_8086", 512, 640, 128),
                new("[8086] Amstrad PC20(0)", "pc200", 1, 1, "CPU_PKG_8086", 512, 640, 128),
                new("[8086] Amstrad PPC512/640", "ppc512", 1, 1, "CPU_PKG_8086", 512, 640, 128),
                new("[8086] Compaq Deskpro", "deskpro", 1, 1, "CPU_PKG_8086", 128, 640, 128),
                new("[8086] Epson Equity LT", "elt", 1, 1, "CPU_PKG_8086", 640, 640, 640),
                new("[8086] IBM Multistation 5550", "ibm5550", 1, 1, "CPU_PKG_8086", 256, 640, 128),
                new("[8086] IBM PS/2 model 25 (color)", "ibmps2_m25", 1, 1, "CPU_PKG_8086", 512, 640, 128),
                new("[8086] IBM PS/2 model 30", "ibmps2_m30", 1, 1, "CPU_PKG_8086", 640, 640, 640),
                new("[8086] Mazovia 1016", "maz1016", 1, 1, "CPU_PKG_8086_MAZOVIA", 256, 640, 384),
                new("[8086] Olivetti M24", "m24", 1, 1, "CPU_PKG_8086", 128, 640, 128),
                new("[8086] Olivetti M240", "m240", 1, 1, "CPU_PKG_8086", 128, 640, 128),
                new("[8086] Schetmash Iskra-3104", "iskra3104", 1, 1, "CPU_PKG_8086", 128, 640, 128),
                new("[8086] Tandy 1000 SL/2", "tandy1000sl2", 1, 1, "CPU_PKG_8086", 512, 768, 128),
                new("[8086] Toshiba T1200", "t1200", 2, 2, "CPU_PKG_8086", 1024, 2048, 1024),
                new("[8086] Victor V86P", "v86p", 1, 1, "CPU_PKG_8086", 512, 1024, 128),
                new("[8086] VTech Laser XT3", "lxt3", 1, 1, "CPU_PKG_8086_VTECH", 512, 640, 64),
            },
            ["[1982] 80286"] = new()
            {
                new("[ISA] IBM AT", "ibmat", 1, 1, "CPU_PKG_286", 256, 512, 256),
                new("[ISA] IBM PS/1 model 2011", "ibmps1es", 2, 2, "CPU_PKG_286", 256, 2560, 128),
                new("[ISA] IBM PS/2 model 30-286", "ibmps2_m30_286", 4, 4, "CPU_PKG_286 | CPU_PKG_386SLC_IBM | CPU_PKG_486SLC_IBM", 512, 4096, 512),
                new("[ISA] IBM XT model 286", "ibmxt286", 1, 1, "CPU_PKG_286", 256, 640, 128),
                new("[ISA] Commodore PC-30 III", "cmdpc30", 1, 1, "CPU_PKG_286", 384, 1024, 128),
                new("[ISA] Compaq Portable II", "portableii", 1, 1, "CPU_PKG_286", 256, 640, 128),
                new("[ISA] Compaq Portable III", "portableiii", 7, 7, "CPU_PKG_286", 256, 6784, 128),
                new("[ISA] Flying Triumph FT-286", "ft286", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[ISA] GRiD GRiDCASE 1520", "grid1520", 8, 8, "CPU_PKG_286", 256, 8192, 128),
                new("[ISA] Multitech PC-900", "pc900", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[ISA] MR BIOS 286 clone", "mr286", 16, 16, "CPU_PKG_286", 256, 16384, 128),
                new("[ISA] NCR PC8", "pc8", 1, 1, "CPU_PKG_286", 256, 640, 128),
                new("[ISA] Olivetti M290 (1984)", "m290", 2, 2, "CPU_PKG_286", 256, 2048, 128),
                new("[ISA] Packard Bell PB286", "pb286", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[ISA] Phoenix AT clone", "ibmatpx", 16, 16, "CPU_PKG_286", 256, 16384, 128),
                new("[ISA] Quadtel AT clone", "ibmatquadtel", 16, 16, "CPU_PKG_286", 256, 16384, 128),
                new("[ISA] Sanyo MBC-17PLUS", "mbc17", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[ISA] Sharp AX286D", "ax286", 16, 16, "CPU_PKG_286", 256, 16384, 128),
                new("[ISA] Siemens PCD-2L", "siemens", 16, 16, "CPU_PKG_286", 256, 16384, 128),
                new("[ISA] Toshiba T3100e", "t3100e", 5, 5, "CPU_PKG_286", 1024, 5120, 256),
                new("[ISA] Trangg Bow Unknown 286", "ibmatami", 16, 16, "CPU_PKG_286", 256, 16384, 128),
                new("[C&T PC/AT] Dell System 200", "dells200", 14, 14, "CPU_PKG_286", 640, 14336, 128),
                new("[C&T PC/AT] Flying Triumph BABY-286", "ftbaby286", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[C&T PC/AT] Hyundai Super-286C", "super286c", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[C&T PC/AT] PC's Limited 28608L", "at122", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[C&T PC/AT] Tulip AT Compact", "tuliptc7", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[C&T PC/AT] Wells American A*Star", "wellamerastar", 1, 1, "CPU_PKG_286", 256, 1024, 128),
                new("[GC103] Quadtel 286 clone", "quadt286", 8, 8, "CPU_PKG_286", 256, 8192, 256),
                new("[GC103] TriGem VULCAN-II", "tg286m", 8, 8, "CPU_PKG_286", 256, 8192, 256),
                new("[NEAT] Arche AMA-2010", "px286", 8, 8, "CPU_PKG_286", 256, 8192, 128),
                new("[NEAT] Atari PC4", "ataripc4", 8, 8, "CPU_PKG_286", 256, 8192, 128),
                new("[NEAT] DataExpert 286", "ami286", 8, 8, "CPU_PKG_286", 256, 8192, 128),
                new("[NEAT] NCR 3302", "3302", 8, 8, "CPU_PKG_286", 256, 8192, 128),
                new("[NEAT] Nixdorf 8810 M30", "n8810m30", 1, 1, "CPU_PKG_286", 640, 640, 640),
                new("[SCAMP] Amstrad PC7286", "pc7286", 16, 16, "CPU_PKG_286", 1024, 16384, 512),
                new("[SCAT] Amstrad PC5286", "pc5286", 4, 4, "CPU_PKG_286", 1024, 4096, 128),
                new("[SCAT] GW-286CT GEAR", "gw286ct", 16, 16, "CPU_PKG_286", 512, 16384, 128),
                new("[SCAT] GoldStar GDC-212M", "gdc212m", 4, 4, "CPU_PKG_286", 512, 4096, 128),
                new("[SCAT] Hyundai Solomon 286KP", "award286", 8, 8, "CPU_PKG_286", 640, 8192, 128),
                new("[SCAT] Hyundai Super-286TR", "super286tr", 8, 8, "CPU_PKG_286", 640, 8192, 128),
                new("[SCAT] ICL DRS M35/286", "drsm35286", 5, 5, "CPU_PKG_286", 512, 5120, 128),
                new("[SCAT] Samsung Deskmaster 286/12", "deskmaster286", 8, 8, "CPU_PKG_286", 512, 8192, 128),
                new("[SCAT] Samsung SPC-4200P", "spc4200p", 8, 8, "CPU_PKG_286", 512, 8192, 128),
                new("[SCAT] Samsung SPC-4216P", "spc4216p", 5, 5, "CPU_PKG_286", 512, 5120, 128),
                new("[SCAT] Samsung SPC-4620P", "spc4620p", 8, 8, "CPU_PKG_286", 512, 8192, 128),
                new("[SCAT] Senor Science Co. SCAT-286-003", "senorscat286", 8, 8, "CPU_PKG_286", 512, 8192, 128),
                new("[MCA] IBM PS/2 model 50", "ibmps2_m50", 2, 2, "CPU_PKG_286 | CPU_PKG_386SLC_IBM | CPU_PKG_486SLC_IBM", 1024, 2048, 1024),
                new("[MCA] IBM PS/2 model 60", "ibmps2_m60", 4, 4, "CPU_PKG_286 | CPU_PKG_386SLC_IBM | CPU_PKG_486SLC_IBM", 1024, 4096, 1024),
            },
            ["[1988] i386SX"] = new()
            {
                new("[ISA] IBM PS/1 model 2121", "ibmps1_2121", 6, 6, "CPU_PKG_386SX", 2048, 6144, 1024),
                new("[ISA] NCR PC916SX", "pc916sx", 1, 1, "CPU_PKG_386SX", 512, 1024, 128),
                new("[ISA] QTC-SXM KT X20T02/HI", "quadt386sx", 16, 16, "CPU_PKG_386SX", 512, 16384, 512),
                new("[ACC 2036] Packard Bell PB300", "pbl300sx", 16, 16, "CPU_PKG_386SX", 1024, 16384, 128),
                new("[ALi M1217] AAEON SBC-350A", "sbc350a", 16, 16, "CPU_PKG_386SX", 1024, 16384, 1024),
                new("[ALi M1217] Acrosser AR-B1374", "arb1374", 16, 16, "CPU_PKG_386SX", 1024, 16384, 1024),
                new("[ALi M1217] Chaintech 3xxAX/AXB", "325ax", 16, 16, "CPU_PKG_386SX", 1024, 16384, 1024),
                new("[ALi M1217] Flytech A36", "flytech386", 16, 16, "CPU_PKG_386SX", 1024, 16384, 1024),
                new("[ALi M1409] Acer 100T", "acer100t", 16, 16, "CPU_PKG_386SX", 2048, 16256, 128),
                new("[HT18] Arche AMA-932J", "ama932j", 8, 8, "CPU_PKG_386SX", 512, 8192, 128),
                new("[HT18] Tandy 1000 RSX", "tandy1000rsx", 9, 9, "CPU_PKG_386SX", 1024, 9216, 512),
                new("[Intel 82335] ADI 386SX", "adi386sx", 8, 8, "CPU_PKG_386SX", 512, 8192, 512),
                new("[Intel 82335] Morse Shuttle 386SX", "shuttle386sx", 8, 8, "CPU_PKG_386SX", 512, 8192, 512),
                new("[NEAT] Commodore SL386SX-16", "cmdsl386sx16", 8, 8, "CPU_PKG_386SX", 512, 8192, 512),
                new("[NEAT] DTK PM-1630C", "dtk386", 8, 8, "CPU_PKG_386SX", 512, 8192, 128),
                new("[NEAT] Micro Express Inc. 066-3.1-0889", "me386sx", 12, 12, "CPU_PKG_386SX", 512, 12288, 128),
                new("[NEAT] Philips P3345", "p3345", 8, 8, "CPU_PKG_386SX", 512, 8192, 128),
                new("[NEATsx] OKI if386AX30L", "if386sx", 4, 4, "CPU_PKG_386SX", 1024, 4096, 1024),
                new("[OPTi 283] Silicon Valley Computer SVC386SX/P1", "svc386sxp1", 16, 16, "CPU_PKG_386SX", 3072, 16384, 512),
                new("[OPTi 291] DTK PPM-3333P", "awardsx", 16, 16, "CPU_PKG_386SX", 1024, 16384, 512),
                new("[SCAMP] Commodore SL386SX-25", "cmdsl386sx25", 16, 16, "CPU_PKG_386SX", 1024, 16384, 512),
                new("[SCAMP] DataExpert VLSI 311 386SX", "dataexpert386sx", 16, 16, "CPU_PKG_386SX", 512, 16384, 512),
                new("[SCAMP] Dell System 333s/L", "dells333sl", 16, 16, "CPU_PKG_386SX", 1024, 16384, 1024),
                new("[SCAMP] Samsung SPC-60xxP", "spc6033p", 16, 16, "CPU_PKG_386SX", 1024, 16384, 512),
                new("[SCATsx] Kaimei KMX-C-02", "kmxc02", 16, 16, "CPU_PKG_386SX", 512, 16384, 512),
                new("[WD76C10] Amstrad MegaPC", "megapc", 16, 16, "CPU_PKG_386SX", 512, 16384, 512),
                new("[MCA] IBM PS/2 model 55SX", "ibmps2_m55sx", 8, 8, "CPU_PKG_386SX", 1024, 8192, 1024),
                new("[MCA] IBM PS/2 model 65SX", "ibmps2_m65sx", 8, 8, "CPU_PKG_386SX", 1024, 8192, 1024),
            },
            ["[1997] ALi M6117"] = new()
            {
                new("[ALi M6117] Acrosser PJ-A511M", "pja511m", 16, 32, "CPU_PKG_M6117", 1024, 32768, 1024),
                new("[ALi M6117] ICOP-6021", "icop6021", 16, 20, "CPU_PKG_M6117", 4096, 20480, 4096),
                new("[ALi M6117] JUMPtec MOPS/386A", "mops386a", 8, 8, "CPU_PKG_M6117", 2048, 8192, 2048),
                new("[ALi M6117] Protech ProX-1330", "prox1332", 16, 64, "CPU_PKG_M6117", 1024, 65536, 1024),
            },
            ["[1992] 486SLC"] = new()
            {
                new("[OPTi 283] RYC Leopard LX", "rycleopardlx", 16, 16, "CPU_PKG_486SLC_IBM", 1024, 16384, 1024),
            },
            ["[1985] i386DX"] = new()
            {
                new("[386DX] IBM XT (Inboard 386/PC)", "ibmxt_inboard386", 5, 5, "CPU_PKG_386DX | CPU_PKG_486BL | CPU_PKG_486DLC", 1024, 5120, 2048),
                new("[ISA] Compaq Deskpro 386", "deskpro386", 16, 16, "CPU_PKG_386DX_DESKPRO386", 1024, 16384, 1024),
                new("[ISA] Compaq Portable III (386)", "portableiii386", 14, 14, "CPU_PKG_386DX", 1024, 14336, 1024),
                new("[ISA] Micronics 386 I-CACHE (Phoenix BIOS)", "micronics386px", 8, 8, "CPU_PKG_386DX", 512, 8192, 128),
                new("[ISA] Micronics 386 I-CACHE (Tandon BIOS)", "micronics386", 8, 8, "CPU_PKG_386DX", 512, 8192, 128),
                new("[ACC 2168] Juko AT046DX3", "acc386", 16, 16, "CPU_PKG_386DX", 1024, 16384, 1024),
                new("[ALi M1429] ECS Panda 386V", "ecs386v", 16, 64, "CPU_PKG_386DX", 1024, 65536, 1024),
                new("[C&T 386/AT] ECS 386/32", "ecs386", 16, 16, "CPU_PKG_386DX", 1024, 16384, 1024),
                new("[C&T 386/AT] Samsung SPC-6000A", "spc6000a", 16, 16, "CPU_PKG_386DX", 1024, 16384, 1024),
                new("[C&T 386/AT] Tandy 4000", "tandy4000", 16, 16, "CPU_PKG_386DX", 1024, 16384, 1024),
                new("[OPTi 391] DataExpert 386C", "dataexpert386wb", 16, 32, "CPU_PKG_386DX", 1024, 32768, 1024),
                new("[OPTi 495SLC] U-Board Unknown", "award495", 16, 32, "CPU_PKG_386DX", 1024, 32768, 1024),
                new("[SiS 310] ASUS 386/33-64K", "asus3863364k", 16, 16, "CPU_PKG_386DX", 1024, 16384, 1024),
                new("[SiS 310] ASUS ISA-386C", "asus386", 16, 64, "CPU_PKG_386DX", 1024, 65536, 1024),
                new("[SiS 460] ASUS ISA-386SIQ", "asus386siq", 16, 32, "CPU_PKG_386DX", 1024, 32768, 1024),
                new("[MCA] IBM PS/2 model 80 (type 2)", "ibmps2_m80", 16, 64, "CPU_PKG_386DX | CPU_PKG_486BL", 1024, 65536, 1024),
                new("[MCA] IBM PS/55 model 5550-T Stage II", "ibmps55_m50t", 16, 64, "CPU_PKG_386DX | CPU_PKG_486BL", 2048, 65536, 2048),
            },
            ["[1989] i386DX/i486"] = new()
            {
                new("[ALi M1429G] DataExpert EXP4349", "exp4349", 16, 64, "CPU_PKG_386DX | CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[OPTi 495SX] CAF Technology C747", "c747", 16, 32, "CPU_PKG_386DX | CPU_PKG_SOCKET1", 1024, 32768, 1024),
                new("[OPTi 495SX] DataExpert OPTI-495SX", "ami495", 16, 32, "CPU_PKG_386DX | CPU_PKG_SOCKET1", 1024, 32768, 1024),
                new("[Symphony SL82C460] APC Predator I Plus", "pred1plus", 16, 64, "CPU_PKG_386DX | CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[VIA VT82C495] FIC 4386-VC-V", "fic4386vcv", 16, 64, "CPU_PKG_386DX | CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[MCA] IBM PS/2 model 70 (type 3)", "ibmps2_m70_type3", 16, 64, "CPU_PKG_386DX | CPU_PKG_486BL | CPU_PKG_SOCKET1", 2048, 65536, 2048),
                new("[MCA] IBM PS/2 model 80 (type 3)", "ibmps2_m80_type3", 16, 64, "CPU_PKG_386DX | CPU_PKG_486BL | CPU_PKG_SOCKET1", 4096, 65536, 4096),
                new("[MCA] IBM PS/55 model 5550-V0/V1", "ibmps55_m50v", 16, 64, "CPU_PKG_386DX | CPU_PKG_486BL | CPU_PKG_SOCKET1", 4096, 65536, 4096),
            },
            ["[1989] Socket 168/1"] = new()
            {
                new("[CS4031] AMI 486 CS4031", "cs4031", 16, 64, "CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[OPTi 481] Gigabyte GA-486L", "ga486l", 16, 16, "CPU_PKG_SOCKET1", 1024, 16384, 1024),
                new("[OPTi 481] Pioneer Vantage 4865C-25/33", "vantage4865c", 16, 32, "CPU_PKG_SOCKET1", 1024, 32768, 1024),
                new("[OPTi 493] Dell Precision V486/xx", "precisionv486", 16, 64, "CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[OPTi 493] Leading Edge CPC-2000", "cpc2000le", 16, 64, "CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[OPTi 493] Silicon Valley Computer 486WB", "svc486wb", 16, 32, "CPU_PKG_SOCKET1", 1024, 32768, 1024),
                new("[OPTi 495SX] Packard Bell PB400", "pb400", 16, 20, "CPU_PKG_SOCKET1", 4096, 20480, 1024),
                new("[OPTi 498] Mylex MVI486", "mvi486", 16, 64, "CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[SiS 401] ASUS ISA-486", "isa486", 16, 64, "CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[SiS 401] Chaintech 4xxSX/SC", "sis401", 16, 64, "CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[SiS 460] ABIT AB-AV4", "av4", 16, 32, "CPU_PKG_SOCKET1", 1024, 32768, 1024),
                new("[SiS 461] DEC DECpc LPV+", "decpclpv", 16, 64, "CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[SiS 471] AST Advantage! 40xxd", "advantage40xxd", 16, 36, "CPU_PKG_SOCKET1", 4096, 36864, 4096),
                new("[Symphony SL82C460] DTK PKM-0031Y", "dtk461", 16, 32, "CPU_PKG_SOCKET1", 1024, 32768, 1024),
                new("[VIA VT82C495] FIC 486-VC-HD", "486vchd", 16, 64, "CPU_PKG_SOCKET1", 1024, 65536, 1024),
                new("[VLSI 82C480] HP Vectra 486VL", "vect486vl", 16, 32, "CPU_PKG_SOCKET1", 2048, 32768, 2048),
                new("[VLSI 82C481] Siemens-Nixdorf D824", "d824", 16, 32, "CPU_PKG_SOCKET1", 2048, 32768, 2048),
                new("[VLSI 82C486] Olivetti PCS 44/C", "pcs44c", 16, 20, "CPU_PKG_SOCKET1", 1024, 20480, 1024),
                new("[VLSI 82C486] Tandy Sensation! (25-1650)", "sensation1", 16, 32, "CPU_PKG_SOCKET1", 2048, 32768, 2048),
                new("[VLSI 82C486] Tulip 486 DC/DT", "tuliptc38", 16, 32, "CPU_PKG_SOCKET1", 2048, 32768, 2048),
                new("[ZyMOS Poach] ASUS ISA-486C", "isa486c", 16, 16, "CPU_PKG_SOCKET1", 1024, 16384, 1024),
                new("[ZyMOS Poach] Genoa Unknown 486", "genoa486", 16, 16, "CPU_PKG_SOCKET1", 1024, 16384, 1024),
                new("[MCA] IBM PS/2 model 70 (type 4)", "ibmps2_m70_type4", 16, 64, "CPU_PKG_SOCKET1", 2048, 65536, 2048),
            },
            ["[1992] Socket 2"] = new()
            {
                new("[ACC 2168] Packard Bell PB410A", "pb410a", 16, 36, "CPU_PKG_SOCKET3", 4096, 36864, 1024),
                new("[ALi M1429] Olystar LIL1429", "ali1429", 16, 32, "CPU_PKG_SOCKET3", 1024, 32768, 1024),
                new("[ALi M1429G] Acer A1G", "acera1g", 16, 36, "CPU_PKG_SOCKET3", 4096, 36864, 1024),
                new("[ALi M1429G] Kaimei SA-486 VL-BUS M.B.", "win486", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420TX] J-Bond PCI400C-A", "pci400ca", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[IMS 8848] Tekram G486IP", "g486ip", 16, 128, "CPU_PKG_SOCKET3", 2048, 131072, 2048),
                new("[OPTi 499] Alaris COUGAR 486BL", "cougar", 16, 64, "CPU_PKG_SOCKET3 | CPU_PKG_486BL", 1024, 65536, 1024),
                new("[SiS 460] Samsung SPC-7500P", "spc7500p", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[SiS 461] Auva Compter CAM/SG0", "auvacam", 16, 32, "CPU_PKG_SOCKET3", 1024, 32768, 1024),
                new("[SiS 461] Dell System 4xx/NP", "dell466np", 16, 32, "CPU_PKG_SOCKET3", 1024, 32768, 1024),
                new("[SiS 461] IBM PS/ValuePoint 433DX/Si", "valuepoint433", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[VLSI 82C480] Intel Classic R", "monsoon", 16, 32, "CPU_PKG_SOCKET3", 2048, 32768, 2048),
                new("[VLSI 82C480] ZEOS Martin", "martin", 16, 64, "CPU_PKG_SOCKET3", 2048, 65536, 2048),
                new("[VLSI 82C486] Tandy Sensation! II (25-1651)", "sensation2", 16, 64, "CPU_PKG_SOCKET3", 2048, 65536, 2048),
            },
            ["[1993] Socket 3"] = new()
            {
                new("[ACC 2168] Packard Bell PB430", "pb430", 16, 64, "CPU_PKG_SOCKET3", 4096, 65536, 1024),
                new("[ALi M1429G] A-Trend ATC-1762", "atc1762", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[ALi M1429G] ECS AL486", "ecsal486", 16, 96, "CPU_PKG_SOCKET3", 1024, 98304, 1024),
                new("[ALi M1429G] Lanner Electronics AP-4100AA", "ap4100aa", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[Contaq 82C596A] A-Trend 4GPV5", "4gpv5", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[Contaq 82C597] Visionex Green-B", "greenb", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[OPTi 499] ADD-X Normerel Xenon", "xenon", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[OPTi 499] Alaris Cobalt LPX", "cobalt", 16, 64, "CPU_PKG_SOCKET3 | CPU_PKG_486BL", 1024, 65536, 1024),
                new("[OPTi 895] Jetway J-403TG", "403tg", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[SiS 461] Acer V10", "acerv10", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[SiS 471] ABIT AB-AH4", "win471", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[SiS 471] ABIT AB-AH4T", "win471t", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[SiS 471] Acer Vi15G", "vi15g", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[SiS 471] ASUS VL/I-486SV2GX4", "vli486sv2g", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[SiS 471] DEC Venturis 4xx", "dvent4xx", 16, 68, "CPU_PKG_SOCKET3", 4096, 69632, 4096),
                new("[SiS 471] DTK PKM-0038S E-2", "dtk486", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[SiS 471] Epox GXA486SG", "ami471", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[SiS 471] TriGem 486G", "tg486g", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[SiS 471] Unknown VL-BUS 471 REV. A1", "px471", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[Symphony SL82C460] Y.M.S. VEGA VS486F-3VL", "vs486f3vl", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
            },
            ["[1994] Socket 3 (PCI)"] = new()
            {
                new("[ALi M1429G] MSI MS-4134", "ms4134", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[ALi M1429G] TriGem 486GP", "tg486gp", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[ALi M1489] AAEON SBC-490", "sbc490", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[ALi M1489] ABIT AB-PB4", "abpb4", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[ALi M1489] Acrosser AR-B1476", "arb1476", 16, 72, "CPU_PKG_SOCKET3", 8192, 73728, 1024),
                new("[ALi M1489] ESA TF-486", "tf486", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[ALi M1489] MSI MS-4145", "ms4145", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[ALi M1489] Unknown PCI486 V1-HJ3", "win486pci", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[i420EX] AIR 486PI", "486pi", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420EX] Anigma BAT4IP3e", "bat4ip3e", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420EX] ASUS PVI-486AP4", "486ap4", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420EX] ICS SB486P", "sb486p", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420EX] Intel Classic/PCI ED", "ninja", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420TX] AMI Super Voyager PCI", "amis76", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420TX] ASUS PCI/I-486SP3", "486sp3", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420TX] Intel Classic/PCI", "alfredo", 16, 128, "CPU_PKG_SOCKET3", 2048, 131072, 2048),
                new("[i420ZX] ASUS PCI/I-486SP3G", "486sp3g", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[i420ZX] ICS SB486PV", "sb486pv", 16, 64, "CPU_PKG_SOCKET3", 2048, 65536, 2048),
                new("[IMS 8848] J-Bond PCI400C-B", "pci400cb", 16, 128, "CPU_PKG_SOCKET3", 2048, 131072, 2048),
                new("[OPTi 802G] IBM PC 330 (type 6573)", "pc330_6573", 16, 64, "CPU_PKG_SOCKET3_PC330", 1024, 65536, 1024),
                new("[OPTi 895] Packard Bell PB450", "pb450", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[SiS 496] Acer P3", "acerp3", 16, 128, "CPU_PKG_SOCKET3", 2048, 131072, 1024),
                new("[SiS 496] ASUS PVI-486SP3", "486sp3c", 16, 255, "CPU_PKG_SOCKET3", 1024, 261120, 1024),
                new("[SiS 496] Lucky Star LS-486E", "ls486e", 16, 128, "CPU_PKG_SOCKET3", 2048, 131072, 1024),
                new("[SiS 496] Micronics M4Li", "m4li", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[SiS 496] MSI MS-4144", "ms4144", 16, 128, "CPU_PKG_SOCKET3", 5120, 131072, 1024),
                new("[SiS 496] Rise Computer R418", "r418", 16, 255, "CPU_PKG_SOCKET3", 1024, 261120, 1024),
                new("[SiS 496] Soyo 4SAW2", "4saw2", 16, 255, "CPU_PKG_SOCKET3", 2048, 261120, 1024),
                new("[SiS 496] Zida Tomato 4DPS", "4dps", 16, 255, "CPU_PKG_SOCKET3", 2048, 261120, 1024),
                new("[UMC 8881] A-Trend ATC-1415", "atc1415", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[UMC 8881] Biostar MB-84xxUUD-A", "84xxuuda", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[UMC 8881] Compaq Presario 7100 Series 486", "pl4600c", 16, 64, "CPU_PKG_SOCKET3", 1024, 65536, 1024),
                new("[UMC 8881] ECS Elite UM8810P-AIO", "ecs486", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[UMC 8881] Epson ActionPC 2600", "actionpc2600", 16, 256, "CPU_PKG_SOCKET3", 1024, 262144, 1024),
                new("[UMC 8881] Epson ActionTower 7x00", "actiontower8400", 16, 256, "CPU_PKG_SOCKET3", 1024, 262144, 1024),
                new("[UMC 8881] PC Chips M919", "m919", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[UMC 8881] Samsung SPC7700P-LW", "spc7700plw", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[UMC 8881] Shuttle HOT-433A", "hot433a", 16, 256, "CPU_PKG_SOCKET3", 1024, 262144, 1024),
                new("[VIA VT82C496G] DFI G486VPA", "g486vpa", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
                new("[VIA VT82C496G] FIC 486-VIP-IO2", "486vipio2", 16, 128, "CPU_PKG_SOCKET3", 1024, 131072, 1024),
            },
            ["[1993] Socket 3/4"] = new()
            {
                new("[OPTi 571] TMC PAT45PV", "pat45pv", 16, 64, "CPU_PKG_SOCKET3 | CPU_PKG_SOCKET4", 2048, 65536, 2048),
            },
            ["[1999] STMicroelectronics STPC"] = new()
            {
                new("[STPC Client] ITOX STAR", "itoxstar", 16, 128, "CPU_PKG_STPC", 8192, 131072, 8192),
                new("[STPC Consumer-II] Acrosser AR-B1423C", "arb1423c", 16, 128, "CPU_PKG_STPC", 8192, 131072, 8192),
                new("[STPC Consumer-II] Acrosser AR-B1479", "arb1479", 32, 32, "CPU_PKG_STPC", 32768, 32768, 32768),
                new("[STPC Consumer-II] Lanner Electronics IAC-H488", "iach488", 16, 128, "CPU_PKG_STPC", 8192, 131072, 8192),
                new("[STPC Elite] Advantech PCM-9340", "pcm9340", 32, 96, "CPU_PKG_STPC", 32768, 98304, 8192),
                new("[STPC Atlas] AAEON PCM-5330", "pcm5330", 32, 128, "CPU_PKG_STPC", 32768, 131072, 32768),
            },
            ["[1993] Socket 4"] = new()
            {
                new("[i430LX] Acer V12P", "v12p", 16, 192, "CPU_PKG_SOCKET4", 2048, 196608, 2048),
                new("[i430LX] AMI Excalibur PCI Pentium", "excaliburpci", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[i430LX] ASUS P/I-P5MP3", "p5mp3", 16, 192, "CPU_PKG_SOCKET4", 2048, 196608, 2048),
                new("[i430LX] Dell OptiPlex 560/L", "opti560l", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[i430LX] Gigabyte GA-586IS", "586is", 16, 192, "CPU_PKG_SOCKET4", 2048, 196608, 2048),
                new("[i430LX] IBM PS/ValuePoint P60", "valuepointp60", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[i430LX] Intel Premiere/PCI", "batman", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[i430LX] Intel Premiere/PCI ED", "revenge", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[i430LX] Micronics M5Pi", "m5pi", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[i430LX] Packard Bell PB520R", "pb520r", 16, 136, "CPU_PKG_SOCKET4", 8192, 139264, 2048),
                new("[OPTi 571] TMC PCI58PL", "pci58pl", 16, 64, "CPU_PKG_SOCKET4", 2048, 65536, 2048),
                new("[OPTi 597] AMI Excalibur VLB", "excalibur", 16, 64, "CPU_PKG_SOCKET4", 2048, 65536, 2048),
                new("[OPTi 597] AT&T Globalyst 330 (Pentium)", "globalyst330_p5", 16, 64, "CPU_PKG_SOCKET4", 2048, 65536, 2048),
                new("[OPTi 597] Supermicro P5VL-PCI", "p5vl", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[OPTi 597] TMC PAT58PV", "pat58pv", 16, 64, "CPU_PKG_SOCKET4", 2048, 65536, 2048),
                new("[SiS 501] AMI Excalibur PCI-II Pentium ISA", "excaliburpci2", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[SiS 501] ASUS PCI/I-P5SP4", "p5sp4", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
                new("[SiS 501] ECS SI5PI AIO", "ecs50x", 16, 128, "CPU_PKG_SOCKET4", 2048, 131072, 2048),
            },
            ["[1994] Socket 4/5"] = new()
            {
                new("[OPTi 571] Taken PCI560-01", "pci56001", 16, 128, "CPU_PKG_SOCKET4 | CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[VLSI SuperCore] DEC Celebris 5xx", "celebris5xx", 16, 256, "CPU_PKG_SOCKET4 | CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
            },
            ["[1994] Socket 5"] = new()
            {
                new("[i430NX] ASUS PCI/I-P54NP4", "p54np4", 16, 512, "CPU_PKG_SOCKET5_7", 2048, 524288, 2048),
                new("[i430NX] Gigabyte GA-586IP", "586ip", 16, 256, "CPU_PKG_SOCKET5_7", 2048, 262144, 2048),
                new("[i430NX] Intel Premiere/PCI II", "plato", 16, 512, "CPU_PKG_SOCKET5_7", 2048, 524288, 2048),
                new("[i430NX] Siemens-Nixdorf D842", "d842", 16, 512, "CPU_PKG_SOCKET5_7", 2048, 524288, 2048),
                new("[i430NX] Teknor TEK-932", "tek932", 16, 512, "CPU_PKG_SOCKET5_7", 2048, 524288, 2048),
                new("[i430FX] Acer V30", "acerv30", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] AMI Apollo", "apollo", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Dell OptiPlex GXL/GXM", "optiplexgxl", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] FIC PT-2000", "pt2000", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] HP Pavilion 50x0/70xx", "morrison32", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] IBM PC 3x0 (type 65x6)", "pc330_65x6", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Intel Advanced/ZP", "zappa", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] NEC PowerMate Vxxx", "powermatev", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Packard Bell PB570", "pb570", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Suk Jung SJ-P54CSR-100", "sjp54csr", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] TriGem Hawk", "hawk", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[OPTi 547] NCS Elegance Pentium 90", "ncselp90", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[OPTi 571] TMC PCI54PL", "pci54pl", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[OPTi 597] Shuttle HOT-543", "hot543", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[OPTi 597] TMC PAT54PV", "pat54pv", 16, 64, "CPU_PKG_SOCKET5_7", 2048, 65536, 2048),
                new("[OPTi Viper] Acer M1", "acerm1", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[OPTi Viper] DFI G586OPA", "g586opa", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[OPTi Viper] TriGem Bristol", "bristol", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[SiS 501] ASUS PCI/I-P54SP4", "p54sp4", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[SiS 501] BCM SQ-588", "sq588", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[SiS 501] Gemlight GMB-P54SPS", "p54sps", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[SiS 501] MSI MS-5109", "ms5109", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 1024),
                new("[SiS 5501] Olivetti Torino", "torino", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[UMC 889x] Compaq Presario 7100 Series 586", "pl5600d", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[UMC 889x] Shuttle HOT-539", "hot539", 16, 256, "CPU_PKG_SOCKET5_7", 2048, 262144, 1024),
                new("[VLSI SuperCore] AST Bravo MS", "bravoms586", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[VLSI SuperCore] Micronics M54Si", "m54si", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[VLSI SuperCore] Packard Bell PB600", "pb600", 16, 264, "CPU_PKG_SOCKET5_7", 8192, 270336, 4096),
                new("[VLSI Wildcat] AT&T Globalyst 620", "globalyst620", 16, 384, "CPU_PKG_SOCKET5_7", 4096, 393216, 4096),
                new("[VLSI Wildcat] DFI G586VPM (rev. C)", "g586vpmc", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
            },
            ["[1995] Socket 7 (Single Voltage)"] = new()
            {
                new("[i430FX] ASUS P/I-P55TP4XE", "p54tp4xe", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] DataExpert EXP8551", "exp8551", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] HP Pavilion 51xx", "holly", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] HP Vectra 500 Series xxx/MT", "vectra500mt", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] HP Vectra VE 5/xxx Series 2", "vectra52", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] HP Vectra VL 5/xxx Series 4", "vectra54", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] IBM PC 7x0 (type 68x7)", "ibm_pc700_6877_6887", 16, 128, "CPU_PKG_SOCKET5_7", 16384, 131072, 8192),
                new("[i430FX] Intel Advanced/AS", "atlantis", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Intel Advanced/ATX", "thor", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Intel Advanced/EV", "endeavor", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Intel Advanced/MA", "monaco", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] MSI MS-5119", "ms5119", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Packard Bell PB640", "pb640", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] PC Partner MB500N", "mb500n", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] QDI FMB", "fmb", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430FX] Suk Jung SJ-PTM FX", "sjptm", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 8192),
                new("[i430HX] Acer V35N", "acerv35n", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430HX] AOpen AP53", "ap53", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430HX] Biostar MB-8500TUC", "8500tuc", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430HX] Siemens-Nixdorf D943", "d943", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[i430HX] Siemens Simatic OP47", "op47", 16, 64, "CPU_PKG_SOCKET5_7", 8192, 65536, 4096),
                new("[i430VX] Gateway 2000 Mailman", "gw2kma", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[OPTi Viper] Octek Rhino 8", "rhino8", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[SiS 5501] Chaintech 5SBM2", "5sbm2", 16, 128, "CPU_PKG_SOCKET5_7", 2048, 131072, 2048),
                new("[SiS 5511] AMI Atlas PCI-II", "amis727", 16, 512, "CPU_PKG_SOCKET5_7", 2048, 524288, 2048),
                new("[SiS 5511] AOpen AP5S", "ap5s", 16, 512, "CPU_PKG_SOCKET5_7", 2048, 524288, 2048),
                new("[SiS 5511] HP Pavilion 52xx", "fm562", 16, 512, "CPU_PKG_SOCKET5_7", 8192, 524288, 4096),
                new("[SiS 5511] IBM PC 140 (type 6260)", "pc140_6260", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[SiS 5511] MSI MS-5124", "ms5124", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[VLSI Wildcat] ZEOS Boa 2", "zeoswildcat", 16, 384, "CPU_PKG_SOCKET5_7", 4096, 393216, 4096),
            },
            ["[1996] Socket 7 (Dual Voltage)"] = new()
            {
                new("[ALi ALADDiN IV+] Biostar M5ATA", "m5ata", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[ALi ALADDiN IV+] MSI MS-5164", "ms5164", 16, 1024, "CPU_PKG_SOCKET5_7", 8192, 1048576, 8192),
                new("[ALi ALADDiN IV+] PC Chips M560", "m560", 16, 768, "CPU_PKG_SOCKET5_7", 8192, 786432, 8192),
                new("[i430HX] Acer AcerPower Ultima", "acerm3a", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430HX] ASUS P/I-P55T2P4", "p55t2p4", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430HX] ASUS P/I-P65UP5 (C-P55T2D)", "p65up5_cp55t2d", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430HX] HP Pavilion 73xx", "rubyusb", 16, 512, "CPU_PKG_SOCKET5_7", 8192, 524288, 4096),
                new("[i430HX] Intel CU430HX", "cu430hx", 16, 512, "CPU_PKG_SOCKET5_7", 8192, 524288, 4096),
                new("[i430HX] Intel TC430HX", "tc430hx", 16, 512, "CPU_PKG_SOCKET5_7", 8192, 524288, 4096),
                new("[i430HX] Micronics M7S-Hi", "m7shi", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430HX] RadiSys EPC-2102", "epc2102", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430HX] Sony Vaio PCV-90", "pcv90", 16, 512, "CPU_PKG_SOCKET5_7", 8192, 524288, 4096),
                new("[i430HX] Supermicro P55T2S", "p55t2s", 16, 512, "CPU_PKG_SOCKET5_7", 4096, 524288, 4096),
                new("[i430VX] AOpen AP5VM", "ap5vm", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430VX] ASUS P/I-P55TVP4", "p55tvp4", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430VX] Azza PT-5IV", "5ivg", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430VX] BCM FM530", "pb810", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430VX] Biostar MB-8500TVX-A", "8500tvxa", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430VX] Compaq Presario 224x", "presario2240", 16, 48, "CPU_PKG_SOCKET5_7", 16384, 49152, 4096),
                new("[i430VX] Compaq Presario 45xx", "presario4500", 16, 48, "CPU_PKG_SOCKET5_7", 16384, 49152, 4096),
                new("[i430VX] Dell Dimension XPS Pxxxa/Mxxxa", "dellhannibalp", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[i430VX] ECS P5VX-B", "p5vxb", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430VX] Epox P55-VA", "p55va", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430VX] Gateway 2000 Hitman", "gw2kte", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[i430VX] HP Brio 80xx", "brio80xx", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[i430VX] LG IBM Multinet x52", "lgibmx52", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[i430VX] Packard Bell PB68x", "pb680", 16, 128, "CPU_PKG_SOCKET5_7", 8192, 131072, 4096),
                new("[i430VX] PC Partner MB520N", "mb520n", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430VX] Shuttle HOT-557", "430vx", 16, 128, "CPU_PKG_SOCKET5_7", 4096, 131072, 4096),
                new("[i430TX] ADLink NuPRO-59x", "nupro592", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[i430TX] ASUS TX97", "tx97", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[i430TX] ASUS TXP4-X", "txp4x", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[i430TX] AwardBIOS v6.00PG Beta Evaluation Board", "awardeval", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[i430TX] Dell OptiPlex GN+", "optiplexgn", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[i430TX] Gateway E-1000", "tomahawk", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[i430TX] HP Pavilion 81xx", "tx97xv", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[i430TX] Intel AN430TX", "an430tx", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[i430TX] Intel YM430TX", "ym430tx", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[i430TX] Micronics Thunderbolt", "thunderbolt", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[i430TX] MSI MS-5156", "ms5156", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[i430TX] NEC Mate NX MA23C", "ma23c", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[i430TX] PC Partner MB540N", "mb540n", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[i430TX] Soltek SL-56A5", "56a5", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[i430TX] Supermicro P5MMS98", "p5mms98", 16, 256, "CPU_PKG_SOCKET5_7", 4096, 262144, 4096),
                new("[i430TX] TriGem RD535", "richmond", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[SiS 5571] Daewoo CD520", "cb52xsi", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[SiS 5571] MSI MS-5146", "ms5146", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[SiS 5571] Rise R534F", "r534f", 16, 384, "CPU_PKG_SOCKET5_7", 8192, 393216, 8192),
                new("[SiS 5581] ASUS SP97-XV", "sp97xv", 16, 1536, "CPU_PKG_SOCKET5_7", 8192, 1572864, 8192),
                new("[SiS 5581] BCM SQ-578", "sq578", 16, 1536, "CPU_PKG_SOCKET5_7", 8192, 1572864, 8192),
                new("[SiS 5591] MSI MS-5172", "ms5172", 16, 768, "CPU_PKG_SOCKET5_7", 8192, 786432, 8192),
                new("[Utron UT85C50x] PC Chips M558", "m558", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[VIA VPX] FIC VA-502", "ficva502", 16, 512, "CPU_PKG_SOCKET5_7", 8192, 524288, 8192),
                new("[VIA VP3] FIC PA-2012", "ficpa2012", 16, 1024, "CPU_PKG_SOCKET5_7", 8192, 1048576, 8192),
                new("[VIA VP3] Lucky Star 5AVP3", "5avp3", 16, 1024, "CPU_PKG_SOCKET5_7", 8192, 1048576, 8192),
                new("[VIA VP3] PC Partner VIA809DS", "via809ds", 16, 1024, "CPU_PKG_SOCKET5_7", 8192, 1048576, 8192),
            },
            ["[1998] Super Socket 7"] = new()
            {
                new("[ALi ALADDiN V] ASUS P5A", "p5a", 16, 1536, "CPU_PKG_SOCKET5_7", 8192, 1572864, 8192),
                new("[ALi ALADDiN V] Cobalt Qube 3", "cobalt3k_carmel", 32, 1024, "CPU_PKG_SOCKET5_7", 32768, 1048576, 8192),
                new("[ALi ALADDiN V] Cobalt RaQ 3/4", "cobalt3k_pacifica", 32, 1024, "CPU_PKG_SOCKET5_7", 32768, 1048576, 8192),
                new("[ALi ALADDiN V] Gateway Lucas", "gwlucas", 16, 256, "CPU_PKG_SOCKET5_7", 8192, 262144, 8192),
                new("[ALi ALADDiN V] Gigabyte GA-5AA", "5aa", 16, 1536, "CPU_PKG_SOCKET5_7", 8192, 1572864, 8192),
                new("[ALi ALADDiN V] Gigabyte GA-5AX", "5ax", 16, 1536, "CPU_PKG_SOCKET5_7", 8192, 1572864, 8192),
                new("[ALi ALADDiN V] MSI MS-5169", "ms5169", 16, 1536, "CPU_PKG_SOCKET5_7", 8192, 1572864, 8192),
                new("[ALi ALADDiN V] PC Chips M579", "m579", 16, 1536, "CPU_PKG_SOCKET5_7", 8192, 1572864, 8192),
                new("[SiS 530] BCM IN530", "in530", 16, 1024, "CPU_PKG_SOCKET5_7", 8192, 1048576, 8192),
                new("[SiS 530] IBM Aptiva 2187", "aptiva2187", 16, 1024, "CPU_PKG_SOCKET5_7", 8192, 1048576, 8192),
                new("[SiS 5591] Gigabyte GA-5SG100", "5sg100", 16, 768, "CPU_PKG_SOCKET5_7", 8192, 786432, 8192),
                new("[VIA MVP3] AOpen AX59 Pro", "ax59pro", 16, 1024, "CPU_PKG_SOCKET5_7", 8192, 1048576, 8192),
                new("[VIA MVP3] DFI K6BV3+ (rev. A+)", "k6bv3p_a", 16, 768, "CPU_PKG_SOCKET5_7", 16384, 786432, 16384),
                new("[VIA MVP3] FIC VA-503+", "ficva503p", 16, 1024, "CPU_PKG_SOCKET5_7", 8192, 1048576, 8192),
                new("[VIA MVP3] FIC VA-503A", "ficva503a", 16, 768, "CPU_PKG_SOCKET5_7", 8192, 786432, 8192),
                new("[VIA MVP3] Soyo 5EMA PRO", "5emapro", 16, 768, "CPU_PKG_SOCKET5_7", 8192, 786432, 8192),
                new("[VIA MVP3] TriGem Delhi-III", "delhi3", 16, 512, "CPU_PKG_SOCKET5_7", 8192, 524288, 8192),
            },
            ["[1995] Socket 8"] = new()
            {
                new("[i450KX] AOpen AP61", "ap61", 16, 256, "CPU_PKG_SOCKET8", 8192, 262144, 8192),
                new("[i450KX] Intel Performance/AU", "aurora", 16, 512, "CPU_PKG_SOCKET8", 8192, 524288, 8192),
                new("[i450GX] ASUS P/I-P6RP4", "p6rp4", 16, 512, "CPU_PKG_SOCKET8", 8192, 524288, 8192),
                new("[i450GX] FIC PO-6000", "ficpo6000", 16, 512, "CPU_PKG_SOCKET8", 8192, 524288, 8192),
                new("[i440FX] Acer V60N", "acerv60n", 16, 384, "CPU_PKG_SOCKET8", 8192, 393216, 8192),
                new("[i440FX] ASUS P/I-P65UP5 (C-P6ND)", "p65up5_cp6nd", 16, 1024, "CPU_PKG_SOCKET8", 8192, 1048576, 8192),
                new("[i440FX] Biostar MB-8600TTC", "8600ttc", 16, 512, "CPU_PKG_SOCKET8", 8192, 524288, 8192),
                new("[i440FX] Gigabyte GA-686NX", "686nx", 16, 512, "CPU_PKG_SOCKET8", 8192, 524288, 8192),
                new("[i440FX] Intel AP440FX", "ap440fx", 16, 512, "CPU_PKG_SOCKET8", 8192, 524288, 8192),
                new("[i440FX] Intel VS440FX", "vs440fx", 16, 512, "CPU_PKG_SOCKET8", 8192, 524288, 8192),
                new("[i440FX] LG IBM Multinet x61", "lgibmx61", 40, 512, "CPU_PKG_SOCKET8", 40960, 524288, 8192),
                new("[i440FX] Micronics M6Mi", "m6mi", 16, 768, "CPU_PKG_SOCKET8", 8192, 786432, 8192),
                new("[i440FX] PC Partner MB600N", "mb600n", 16, 512, "CPU_PKG_SOCKET8", 8192, 524288, 8192),
                new("[i440FX] Zida Tomato 6DXP", "6dxp", 16, 1024, "CPU_PKG_SOCKET8", 8192, 1048576, 8192),
            },
            ["[1997] Slot 1"] = new()
            {
                new("[ALi ALADDiN-PRO II] PC Chips M729", "m729", 16, 1536, "CPU_PKG_SLOT1", 8192, 1572864, 8192),
                new("[i440FX] Acer V62X", "acerv62x", 16, 384, "CPU_PKG_SLOT1", 8192, 393216, 8192),
                new("[i440FX] AIR P6KDI", "p6kdi", 16, 1024, "CPU_PKG_SLOT1", 8192, 1048576, 8192),
                new("[i440FX] ASUS P/I-P65UP5 (C-PKND)", "p65up5_cpknd", 16, 1024, "CPU_PKG_SLOT1", 8192, 1048576, 8192),
                new("[i440FX] ASUS KN97", "kn97", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440FX] FIC KN-6000", "fickn6000", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440FX] Micronics Dual Fortress", "dualfortress", 40, 1024, "CPU_PKG_SLOT1", 40960, 1048576, 8192),
                new("[i440LX] ABIT AB-LX6", "lx6", 16, 1024, "CPU_PKG_SLOT1", 8192, 1048576, 8192),
                new("[i440LX] Dell OptiPlex GXa", "optiplexgxa", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440LX] Intel AL440LX", "al440lx", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440LX] Micronics Spitfire", "spitfire", 16, 1024, "CPU_PKG_SLOT1", 8192, 1048576, 8192),
                new("[i440LX] MSI MS-6117", "ms6117", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440LX] NEC Mate NX MA30D", "ma30d", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440EX] BCM IN440EX", "in440ex", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[i440EX] BCM IN440EX-D", "in440exd", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[i440EX] Dell OptiPlex E1", "optiplexe1", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[i440EX] HP Brio 83xx", "brio83xx", 16, 256, "CPU_PKG_SLOT1", 8192, 262144, 8192),
                new("[i440EX] QDI EXCELLENT II", "p6i440e2", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[i440EX] TriGem Como", "como", 16, 256, "CPU_PKG_SLOT1", 8192, 262144, 8192),
                new("[i440BX] ABIT AB-BE6-II", "be6ii", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440BX] ABIT AB-BF6", "bf6", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440BX] ABIT AB-BX6", "bx6", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[i440BX] AOpen AX6BC", "ax6bc", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440BX] ASUS P2B-LS", "p2bls", 16, 1024, "CPU_PKG_SLOT1", 8192, 1048576, 8192),
                new("[i440BX] ASUS P3B-F", "p3bf", 16, 1024, "CPU_PKG_SLOT1", 8192, 1048576, 8192),
                new("[i440BX] Dell OptiPlex GX1", "optiplexgx1", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440BX] Fujitsu ErgoPro x365", "ergox365", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440BX] Gigabyte GA-686BX", "686bx", 16, 1024, "CPU_PKG_SLOT1", 8192, 1048576, 8192),
                new("[i440BX] MSI MS-6119", "ms6119", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440BX] MSI MS-6147", "ms6147", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[i440BX] Supermicro P6SBA", "p6sba", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440BX] Tyan Tsunami ATX", "s1846", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[i440ZX] MiTAC/Trigon 6110Zu", "vei8", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[i440ZX] MSI MS-6168", "ms6168", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[i440ZX] Packard Bell Bora Pro", "borapro", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[SiS 5600] Freetech/Flexus P6F99", "p6f99", 16, 1536, "CPU_PKG_SLOT1", 8192, 1572864, 8192),
                new("[SiS 5600] PC Chips M747", "m747", 16, 1536, "CPU_PKG_SLOT1", 8192, 1572864, 8192),
                new("[SMSC VictoryBX-66] A-Trend ATC6310BXII", "atc6310bxii", 16, 768, "CPU_PKG_SLOT1", 8192, 786432, 8192),
                new("[VIA Apollo Pro] FIC KA-6130", "ficka6130", 16, 512, "CPU_PKG_SLOT1", 8192, 524288, 8192),
                new("[VIA Apollo Pro 133] ASUS P3V133", "p3v133", 16, 1536, "CPU_PKG_SLOT1", 8192, 1572864, 8192),
                new("[VIA Apollo Pro 133] MSI MS-6199VA", "ms6199va", 16, 1536, "CPU_PKG_SLOT1", 8192, 1572864, 8192),
                new("[VIA Apollo Pro 133A] ASUS P3V4X", "p3v4x", 16, 2048, "CPU_PKG_SLOT1", 8192, 2097152, 8192),
                new("[VIA Apollo Pro 133A] BCM GT694VA", "gt694va", 16, 3072, "CPU_PKG_SLOT1", 8192, 3145728, 8192),
            },
            ["[1998] Slot 1/2"] = new()
            {
                new("[i440GX] Freeway FW-6400GX", "fw6400gx", 16, 1792, "CPU_PKG_SLOT1 | CPU_PKG_SLOT2", 16384, 1835008, 16384),
            },
            ["[1998] Slot 1/Socket 370"] = new()
            {
                new("[ALi ALADDiN-PRO II] PC Chips M726MRT", "m726mrt", 16, 1536, "CPU_PKG_SLOT1 | CPU_PKG_SOCKET370", 8192, 1572864, 8192),
                new("[i440BX] ECS P6BXT-A+", "prosignias31x_bx", 16, 768, "CPU_PKG_SLOT1 | CPU_PKG_SOCKET370", 8192, 786432, 8192),
                new("[i440BX] Tyan Trinity 371", "s1857", 16, 768, "CPU_PKG_SLOT1 | CPU_PKG_SOCKET370", 8192, 786432, 8192),
                new("[VIA Apollo Pro 133] ECS P6BAT-A+", "p6bat", 16, 1536, "CPU_PKG_SLOT1 | CPU_PKG_SOCKET370", 8192, 1572864, 8192),
            },
            ["[1998] Slot 2"] = new()
            {
                new("[i440GX] Gigabyte GA-6GXU", "6gxu", 16, 2048, "CPU_PKG_SLOT2", 16384, 2097152, 16384),
                new("[i440GX] Supermicro S2DGE", "s2dge", 16, 2048, "CPU_PKG_SLOT2", 16384, 2097152, 16384),
            },
            ["[1998] Socket 370"] = new()
            {
                new("[i440LX] Supermicro 370SLM", "s370slm", 16, 768, "CPU_PKG_SOCKET370", 8192, 786432, 8192),
                new("[i440BX] AEWIN AW-O671R", "awo671r", 16, 512, "CPU_PKG_SOCKET370", 8192, 524288, 8192),
                new("[i440BX] AmazePC AM-BX133", "ambx133", 16, 768, "CPU_PKG_SOCKET370", 8192, 786432, 8192),
                new("[i440BX] ASUS CUBX", "cubx", 16, 1024, "CPU_PKG_SOCKET370", 8192, 1048576, 8192),
                new("[i440BX] RadiSys Endura EM440", "em440", 32, 512, "CPU_PKG_SOCKET370", 32768, 524288, 8192),
                new("[i440ZX] Soltek SL-63A1", "63a1", 16, 512, "CPU_PKG_SOCKET370", 8192, 524288, 8192),
                new("[SiS 600] Soyo SY-7SBB", "7sbb", 16, 1536, "CPU_PKG_SOCKET370", 8192, 1572864, 8192),
                new("[SMSC VictoryBX-66] A-Trend ATC7020BXII", "atc7020bxii", 16, 1024, "CPU_PKG_SOCKET370", 8192, 1048576, 8192),
                new("[SMSC VictoryBX-66] PC Chips M773", "m773", 16, 512, "CPU_PKG_SOCKET370", 8192, 524288, 8192),
                new("[VIA Apollo Pro] PC Partner VIM863S", "vim863s", 16, 768, "CPU_PKG_SOCKET370", 8192, 786432, 8192),
                new("[VIA Apollo Pro 133] ECS P6BAP-A+", "p6bap", 16, 1536, "CPU_PKG_SOCKET370", 8192, 1572864, 8192),
                new("[VIA Apollo Pro 133A] Acorp 6VIA90AP", "6via90ap", 16, 3072, "CPU_PKG_SOCKET370", 16384, 3145728, 8192),
                new("[VIA Apollo Pro 133A] ASUS CUV4X-LS", "cuv4xls", 16, 4096, "CPU_PKG_SOCKET370", 16384, 4194304, 8192),
                new("[VIA Apollo Pro 133A] MSI MS-6318", "ms6318", 16, 2048, "CPU_PKG_SOCKET370", 16384, 2097152, 8192),
                new("[VIA Apollo Pro 133A] Samsung CAIRO-5", "cairo5", 16, 3072, "CPU_PKG_SOCKET370", 16384, 3145728, 8192),
            },
            ["Miscellaneous"] = new()
            {
                new("[i440BX] Microsoft Virtual PC 2007", "vpc2007", 16, 1024, "CPU_PKG_SLOT1", 8192, 1048576, 8192),
            },
        };

        // All 82 authentic CPU Families with exact speed & FPU configurations from 86Box cpu_table.c
        public static readonly List<CpuInfo> CpuList = new()
        {
            new("Intel 8088", "8088", new() { "CPU_PKG_8088" },
                new() { "4.77", "7.16", "8", "9.54", "10", "12", "16" },
                new() { "None", "8087" }),
            new("Intel 80C88", "80c88", new() { "CPU_PKG_80C88" },
                new() { "4.77" },
                new() { "None", "8087" }),
            new("Intel 8088", "8088_europc", new() { "CPU_PKG_8088_EUROPC" },
                new() { "4.77", "7.16", "9.54" },
                new() { "None", "8087" }),
            new("Intel 8088", "8088_vtech", new() { "CPU_PKG_8088_VTECH" },
                new() { "4.77", "10" },
                new() { "None", "8087" }),
            new("Intel 8086", "8086", new() { "CPU_PKG_8086" },
                new() { "7.16", "8", "9.54", "10", "12", "16" },
                new() { "None", "8087" }),
            new("KRIM K1810VM86", "8086_mazovia", new() { "CPU_PKG_8086_MAZOVIA" },
                new() { "4.77" },
                new() { "None", "8087" }),
            new("Intel 8086", "8086_vtech", new() { "CPU_PKG_8086_VTECH" },
                new() { "4.77", "10" },
                new() { "None", "8087" }),
            new("Intel 80188", "80188", new() { "CPU_PKG_188" },
                new() { "6", "7.16", "8", "9.54", "10", "12", "16", "20", "25" },
                new() { "None", "8087" }),
            new("NEC V20", "necv20", new() { "CPU_PKG_8088" },
                new() { "4.77", "7.16", "8", "10", "12", "16" },
                new() { "None", "8087" }),
            new("Intel 80186", "80186", new() { "CPU_PKG_186" },
                new() { "6", "7.16", "8", "9.54", "10", "12", "16", "20", "25" },
                new() { "None", "8087", "80187" }),
            new("NEC V30", "necv30", new() { "CPU_PKG_8086" },
                new() { "7.16", "8", "9.54", "10", "12", "16" },
                new() { "None", "8087", "80187" }),
            new("Intel 80286", "286", new() { "CPU_PKG_286" },
                new() { "6", "8", "10", "12", "16", "20", "25" },
                new() { "None", "287", "287XL" }),
            new("Intel i386SX", "i386sx", new() { "CPU_PKG_386SX" },
                new() { "16", "20", "25", "33", "40" },
                new() { "None", "387" }),
            new("AMD Am386SX", "am386sx", new() { "CPU_PKG_386SX" },
                new() { "16", "20", "25", "33", "40" },
                new() { "None", "387" }),
            new("Intel i386DX", "i386dx", new() { "CPU_PKG_386DX" },
                new() { "16", "20", "25", "33", "40" },
                new() { "None", "387" }),
            new("Intel i386DX", "i386dx_deskpro386", new() { "CPU_PKG_386DX_DESKPRO386" },
                new() { "16", "20", "25" },
                new() { "None", "287", "287XL", "387" }),
            new("Cyrix Cx486DLC", "cx486dlc_deskpro386", new() { "CPU_PKG_386DX_DESKPRO386" },
                new() { "25" },
                new() { "None", "387" }),
            new("Cyrix Cx486DRx2", "cx486drx2_deskpro386", new() { "CPU_PKG_386DX_DESKPRO386" },
                new() { "32", "40", "50" },
                new() { "None", "387" }),
            new("Intel RapidCAD", "rapidcad", new() { "CPU_PKG_386DX" },
                new() { "25", "33", "40" },
                new() { "Internal" }),
            new("AMD Am386DX", "am386dx", new() { "CPU_PKG_386DX" },
                new() { "25", "33", "40" },
                new() { "None", "387" }),
            new("ALi M6117", "m6117", new() { "CPU_PKG_M6117" },
                new() { "33", "40" },
                new() { "None" }),
            new("IBM 386SLC", "ibm386slc", new() { "CPU_PKG_386SLC_IBM" },
                new() { "16", "20", "25" },
                new() { "None", "387" }),
            new("Cyrix Cx486SLC", "cx486slc", new() { "CPU_PKG_386SX" },
                new() { "20", "25", "33" },
                new() { "None", "387" }),
            new("Cyrix Cx486SRx2", "cx486srx2", new() { "CPU_PKG_386SX" },
                new() { "32", "40", "50" },
                new() { "None", "387" }),
            new("IBM 486SLC", "ibm486slc", new() { "CPU_PKG_486SLC_IBM" },
                new() { "33" },
                new() { "None", "387" }),
            new("IBM 486SLC2", "ibm486slc2", new() { "CPU_PKG_486SLC_IBM" },
                new() { "40", "50", "66" },
                new() { "None", "387" }),
            new("IBM 486SLC3", "ibm486slc3", new() { "CPU_PKG_486SLC_IBM" },
                new() { "60", "75", "100" },
                new() { "None", "387" }),
            new("IBM 486BL2", "ibm486bl2", new() { "CPU_PKG_486BL" },
                new() { "50", "66" },
                new() { "None", "387" }),
            new("IBM 486BL3", "ibm486bl3", new() { "CPU_PKG_486BL" },
                new() { "75", "83.5", "100" },
                new() { "None", "387" }),
            new("Cyrix Cx486DLC", "cx486dlc", new() { "CPU_PKG_386DX" },
                new() { "25", "33", "40" },
                new() { "None", "387" }),
            new("Cyrix Cx486DRx2", "cx486drx2", new() { "CPU_PKG_386DX" },
                new() { "32", "40", "50", "66" },
                new() { "None", "387" }),
            new("Intel i486SX", "i486sx", new() { "CPU_PKG_SOCKET1" },
                new() { "16", "20", "25", "33" },
                new() { "None", "487SX" }),
            new("Intel i486SX-S", "i486sx_slenh", new() { "CPU_PKG_SOCKET1" },
                new() { "25", "33" },
                new() { "None", "487SX" }),
            new("Intel i486SX2", "i486sx2", new() { "CPU_PKG_SOCKET1" },
                new() { "50", "66 (Q0569)" },
                new() { "None", "487SX" }),
            new("Intel i486DX", "i486dx", new() { "CPU_PKG_SOCKET1" },
                new() { "25", "33", "50" },
                new() { "Internal" }),
            new("Intel i486DX-S", "i486dx_slenh", new() { "CPU_PKG_SOCKET1" },
                new() { "33", "50" },
                new() { "Internal" }),
            new("Intel i486DX2", "i486dx2", new() { "CPU_PKG_SOCKET1" },
                new() { "40", "50", "66" },
                new() { "Internal" }),
            new("Intel i486DX2-S", "i486dx2_slenh", new() { "CPU_PKG_SOCKET1" },
                new() { "40", "50", "66" },
                new() { "Internal" }),
            new("Intel i486DX2 WB", "i486dx2_pc330", new() { "CPU_PKG_SOCKET1", "CPU_PKG_SOCKET3_PC330" },
                new() { "50", "66" },
                new() { "Internal" }),
            new("Intel Pentium OverDrive (P24T)", "pentium_p24t", new() { "CPU_PKG_SOCKET3", "CPU_PKG_SOCKET3_PC330" },
                new() { "63", "83" },
                new() { "Internal" }),
            new("AMD Am486SX", "am486sx", new() { "CPU_PKG_SOCKET1" },
                new() { "33", "40" },
                new() { "None", "487SX" }),
            new("AMD Am486SX2", "am486sx2", new() { "CPU_PKG_SOCKET1" },
                new() { "50", "66" },
                new() { "None", "487SX" }),
            new("AMD Am486DX", "am486dx", new() { "CPU_PKG_SOCKET1" },
                new() { "33", "40" },
                new() { "Internal" }),
            new("AMD Am486DX2", "am486dx2", new() { "CPU_PKG_SOCKET1" },
                new() { "50", "66", "80" },
                new() { "Internal" }),
            new("AMD Am486DXL", "am486dxl", new() { "CPU_PKG_SOCKET1" },
                new() { "33", "40" },
                new() { "Internal" }),
            new("AMD Am486DXL2", "am486dxl2", new() { "CPU_PKG_SOCKET1" },
                new() { "50", "66", "80" },
                new() { "Internal" }),
            new("AMD Am486DX4", "am486dx4", new() { "CPU_PKG_SOCKET3" },
                new() { "75", "90", "100", "120" },
                new() { "Internal" }),
            new("AMD Am486DX2 (Enhanced)", "am486dx2_slenh", new() { "CPU_PKG_SOCKET3" },
                new() { "66", "80" },
                new() { "Internal" }),
            new("AMD Am486DX4 (Enhanced)", "am486dx4_slenh", new() { "CPU_PKG_SOCKET3" },
                new() { "75", "100", "120" },
                new() { "Internal" }),
            new("AMD Am5x86", "am5x86", new() { "CPU_PKG_SOCKET3" },
                new() { "133 (P75)", "150 (P75+)", "160 (P90)" },
                new() { "Internal" }),
            new("Cyrix Cx486S", "cx486s", new() { "CPU_PKG_SOCKET1" },
                new() { "25", "33", "40" },
                new() { "None", "487SX" }),
            new("Cyrix Cx486DX", "cx486dx", new() { "CPU_PKG_SOCKET1" },
                new() { "33", "40" },
                new() { "Internal" }),
            new("Cyrix Cx486DX2", "cx486dx2", new() { "CPU_PKG_SOCKET1" },
                new() { "50", "66", "80" },
                new() { "Internal" }),
            new("Cyrix Cx486DX4", "cx486dx4", new() { "CPU_PKG_SOCKET3" },
                new() { "75", "100" },
                new() { "Internal" }),
            new("Cyrix Cx5x86", "cx5x86", new() { "CPU_PKG_SOCKET3" },
                new() { "80", "100", "120", "133" },
                new() { "Internal" }),
            new("ST STPC-DX", "stpc_dx", new() { "CPU_PKG_STPC" },
                new() { "66", "75" },
                new() { "Internal" }),
            new("ST STPC-DX2", "stpc_dx2", new() { "CPU_PKG_STPC" },
                new() { "133" },
                new() { "Internal" }),
            new("Intel Pentium (P5)", "pentium_p5", new() { "CPU_PKG_SOCKET4" },
                new() { "50 (Q0399)", "60", "66" },
                new() { "Internal" }),
            new("Intel Pentium OverDrive (P54C 5V)", "pentium_p54c_od5v", new() { "CPU_PKG_SOCKET4" },
                new() { "100", "120", "133" },
                new() { "Internal" }),
            new("Intel Pentium (P54C)", "pentium_p54c", new() { "CPU_PKG_SOCKET5_7" },
                new() { "75", "90", "100/50", "100/66", "120", "133", "150", "166", "200" },
                new() { "Internal" }),
            new("Intel Pentium MMX", "pentium_p55c", new() { "CPU_PKG_SOCKET5_7" },
                new() { "166", "200", "233" },
                new() { "Internal" }),
            new("Intel Mobile Pentium MMX", "pentium_tillamook", new() { "CPU_PKG_SOCKET5_7" },
                new() { "120", "133", "150", "166", "200", "233", "266", "300" },
                new() { "Internal" }),
            new("Intel Pentium OverDrive (P54C 3.3V)", "pentium_p54c_od3v", new() { "CPU_PKG_SOCKET5_7" },
                new() { "125", "150", "166" },
                new() { "Internal" }),
            new("Intel Pentium OverDrive MMX", "pentium_p55c_od", new() { "CPU_PKG_SOCKET5_7" },
                new() { "75", "125", "150/60", "166", "180", "200" },
                new() { "Internal" }),
            new("IDT WinChip", "winchip", new() { "CPU_PKG_SOCKET5_7" },
                new() { "75", "90", "100", "120", "133", "150", "166", "180", "200", "225", "240" },
                new() { "Internal" }),
            new("IDT WinChip 2", "winchip2", new() { "CPU_PKG_SOCKET5_7" },
                new() { "200", "225", "240", "250" },
                new() { "Internal" }),
            new("IDT WinChip 2A", "winchip2a", new() { "CPU_PKG_SOCKET5_7" },
                new() { "200", "233", "266", "300" },
                new() { "Internal" }),
            new("AMD K5 (Model 0)", "k5_ssa5", new() { "CPU_PKG_SOCKET5_7" },
                new() { "75 (PR75)", "90 (PR90)", "100 (PR100)" },
                new() { "Internal" }),
            new("AMD K5 (Model 1/2/3)", "k5_5k86", new() { "CPU_PKG_SOCKET5_7" },
                new() { "90 (PR120)", "100 (PR133)", "105 (PR150)", "116.7 (PR166)", "133 (PR200)" },
                new() { "Internal" }),
            new("AMD K6 (Model 6)", "k6_m6", new() { "CPU_PKG_SOCKET5_7" },
                new() { "66", "100", "133", "166", "200", "233", "K6 (Model 7)", "266", "300" },
                new() { "Internal" }),
            new("AMD K6-2", "k6_2", new() { "CPU_PKG_SOCKET5_7" },
                new() { "100", "133", "166", "200", "233", "266", "300", "333", "350", "366", "380", "400/66", "400/100", "450", "475", "500", "533", "550", "570", "600 (570 OC)", "K6-2+", "K6-III", "400" },
                new() { "Internal" }),
            new("AMD K6-III+", "k6_3p", new() { "CPU_PKG_SOCKET5_7" },
                new() { "100", "133", "166", "200", "233", "266", "300", "333", "350", "366", "380", "400", "450", "475", "500" },
                new() { "Internal" }),
            new("Cyrix Cx6x86", "cx6x86", new() { "CPU_PKG_SOCKET5_7" },
                new() { "80 (PR90+)", "100 (PR120+)", "110 (PR133+)", "120 (PR150+)", "133 (PR166+)", "150 (PR200+)" },
                new() { "Internal" }),
            new("Cyrix Cx6x86L", "cx6x86l", new() { "CPU_PKG_SOCKET5_7" },
                new() { "110 (PR133+)", "120 (PR150+)", "133 (PR166+)", "150 (PR200+)" },
                new() { "Internal" }),
            new("Cyrix Cx6x86MX", "cx6x86mx", new() { "CPU_PKG_SOCKET5_7" },
                new() { "133 (PR166)", "166 (PR200)", "187.5 (PR233)", "208.3 (PR266)" },
                new() { "Internal" }),
            new("Cyrix MII", "mii", new() { "CPU_PKG_SOCKET5_7" },
                new() { "IBM 133 (PR166)", "166 (PR200)", "187.5 (PR233)", "208.3 (PR266)", "233 (PR300)", "250/83 (PR333)", "250/100 (PR366)", "270 (PR350)", "285 (PR400)", "300 (PR433)" },
                new() { "Internal" }),
            new("Intel Pentium Pro", "pentiumpro", new() { "CPU_PKG_SOCKET8" },
                new() { "60", "66", "90", "100", "120", "133", "150", "166", "180", "200" },
                new() { "Internal" }),
            new("Intel Pentium II OverDrive", "pentium2_od", new() { "CPU_PKG_SOCKET8" },
                new() { "66", "100", "133", "166", "200", "233", "266", "300", "333" },
                new() { "Internal" }),
            new("Intel Pentium II (Klamath)", "pentium2_klamath", new() { "CPU_PKG_SLOT1" },
                new() { "66", "100", "133", "166", "200", "233", "266", "300", "Pentium II (Deschutes)", "333", "350", "400", "450" },
                new() { "Internal" }),
            new("Intel Celeron (Covington)", "celeron_covington", new() { "CPU_PKG_SLOT1" },
                new() { "66", "100", "133", "166", "200", "233", "266", "300", "Pentium II Xeon", "150", "250", "350", "400", "450" },
                new() { "Internal" }),
            new("Intel Celeron (Mendocino)", "celeron_mendocino", new() { "CPU_PKG_SOCKET370" },
                new() { "66", "100", "133", "166", "200", "233", "266", "300A", "333", "366", "400", "433", "466", "500", "533" },
                new() { "Internal" }),
            new("VIA Cyrix III (Samuel)", "c3_samuel", new() { "CPU_PKG_SOCKET370" },
                new() { "66", "100", "133", "166", "200", "233", "266", "300", "333", "366", "400", "433", "450", "466", "500", "533", "550", "600/100", "600/133", "650", "667", "700", "733" },
                new() { "Internal" }),
        };



        public static List<CpuInfo> GetCompatibleCpus(string? machineType, string? machineModel = null)
        {
            if (!string.IsNullOrWhiteSpace(machineType) && MachinesByType.TryGetValue(machineType, out var machines))
            {
                var machine = machines.FirstOrDefault(m => m.Name.Equals(machineModel, StringComparison.OrdinalIgnoreCase) || m.InternalName.Equals(machineModel, StringComparison.OrdinalIgnoreCase));
                if (machine != null && !string.IsNullOrWhiteSpace(machine.CpuPackage))
                {
                    var filtered = CpuList.Where(cpu => cpu.CompatibleSockets.Contains(machine.CpuPackage, StringComparer.OrdinalIgnoreCase)).ToList();
                    if (filtered.Count > 0)
                        return filtered;
                }
            }

            if (!string.IsNullOrWhiteSpace(machineType))
            {
                var compatibleSockets = GetMachineSockets(machineType);
                var filtered = CpuList.Where(cpu => cpu.CompatibleSockets.Any(s => compatibleSockets.Contains(s))).ToList();
                if (filtered.Count > 0)
                    return filtered;
            }

            return CpuList;
        }

        private static HashSet<string> GetMachineSockets(string machineType)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (machineType.Contains("8088"))
            {
                set.Add("CPU_PKG_8088");
                set.Add("CPU_PKG_80C88");
                set.Add("CPU_PKG_8088_EUROPC");
                set.Add("CPU_PKG_8088_VTECH");
                set.Add("CPU_PKG_188");
            }
            else if (machineType.Contains("8086"))
            {
                set.Add("CPU_PKG_8086");
                set.Add("CPU_PKG_8086_MAZOVIA");
                set.Add("CPU_PKG_8086_VTECH");
                set.Add("CPU_PKG_186");
            }
            else if (machineType.Contains("80286"))
            {
                set.Add("CPU_PKG_286");
            }
            else if (machineType.Contains("i386SX"))
            {
                set.Add("CPU_PKG_386SX");
                set.Add("CPU_PKG_386SLC_IBM");
            }
            else if (machineType.Contains("ALi M6117"))
            {
                set.Add("CPU_PKG_M6117");
                set.Add("CPU_PKG_386SX");
            }
            else if (machineType.Contains("486SLC"))
            {
                set.Add("CPU_PKG_486SLC");
                set.Add("CPU_PKG_486SLC_IBM");
            }
            else if (machineType.Contains("i386DX/i486"))
            {
                set.Add("CPU_PKG_386DX");
                set.Add("CPU_PKG_SOCKET1");
            }
            else if (machineType.Contains("i386DX"))
            {
                set.Add("CPU_PKG_386DX");
                set.Add("CPU_PKG_386DX_DESKPRO386");
                set.Add("CPU_PKG_486BL");
                set.Add("CPU_PKG_486DLC");
            }
            else if (machineType.Contains("Socket 168") || machineType.Contains("Socket 1"))
            {
                set.Add("CPU_PKG_SOCKET1");
            }
            else if (machineType.Contains("Socket 2"))
            {
                set.Add("CPU_PKG_SOCKET1");
                set.Add("CPU_PKG_SOCKET3");
            }
            else if (machineType.Contains("Socket 3 (PCI)") || machineType.Contains("Socket 3"))
            {
                set.Add("CPU_PKG_SOCKET3");
                set.Add("CPU_PKG_SOCKET1");
            }
            else if (machineType.Contains("Socket 3/4"))
            {
                set.Add("CPU_PKG_SOCKET3");
                set.Add("CPU_PKG_SOCKET4");
            }
            else if (machineType.Contains("STPC"))
            {
                set.Add("CPU_PKG_STPC");
            }
            else if (machineType.Contains("Socket 4/5"))
            {
                set.Add("CPU_PKG_SOCKET4");
                set.Add("CPU_PKG_SOCKET5_7");
            }
            else if (machineType.Contains("Socket 4"))
            {
                set.Add("CPU_PKG_SOCKET4");
            }
            else if (machineType.Contains("Socket 5"))
            {
                set.Add("CPU_PKG_SOCKET5_7");
            }
            else if (machineType.Contains("Socket 7"))
            {
                set.Add("CPU_PKG_SOCKET5_7");
            }
            else if (machineType.Contains("Super Socket 7"))
            {
                set.Add("CPU_PKG_SOCKET5_7");
            }
            else if (machineType.Contains("Socket 8"))
            {
                set.Add("CPU_PKG_SOCKET8");
            }
            else if (machineType.Contains("Slot 1/2"))
            {
                set.Add("CPU_PKG_SLOT1");
                set.Add("CPU_PKG_SLOT2");
            }
            else if (machineType.Contains("Slot 1/Socket 370"))
            {
                set.Add("CPU_PKG_SLOT1");
                set.Add("CPU_PKG_SOCKET370");
            }
            else if (machineType.Contains("Slot 1"))
            {
                set.Add("CPU_PKG_SLOT1");
            }
            else if (machineType.Contains("Slot 2"))
            {
                set.Add("CPU_PKG_SLOT2");
            }
            else if (machineType.Contains("Socket 370"))
            {
                set.Add("CPU_PKG_SOCKET370");
            }
            else
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "CPU_PKG_8088", "CPU_PKG_8086", "CPU_PKG_286", "CPU_PKG_386SX", "CPU_PKG_386DX",
                    "CPU_PKG_SOCKET1", "CPU_PKG_SOCKET3", "CPU_PKG_SOCKET4", "CPU_PKG_SOCKET5_7",
                    "CPU_PKG_SOCKET8", "CPU_PKG_SLOT1", "CPU_PKG_SLOT2", "CPU_PKG_SOCKET370"
                };
            }
            return set;
        }

        /// <summary>"Default", then "0 wait states" ... "7 wait states": the original's list. Index = the cpu_waitstates value.</summary>
        public static readonly List<string> WaitStatesOptions = new[] { "Default" }
            .Concat(Enumerable.Range(0, 8).Select(i => i == 1 ? "1 wait state" : $"{i} wait states")).ToList();
        public static readonly List<string> PitModes = new() { "Auto", "Slow", "Fast" };

        public static readonly List<string> VideoCards = new()
        {
            "None",
            "Internal / Motherboard Video",
            "IBM Monochrome Display Adapter (MDA)",
            "IBM Color Graphics Adapter (CGA)",
            "IBM Enhanced Graphics Adapter (EGA)",
            "IBM Video Graphics Array (VGA)",
            "IBM Professional Graphics Controller (PGC)",
            "Hercules Graphics Card",
            "Hercules Graphics Card Plus",
            "Hercules InColor Card",
            "Plantronics Colorplus",
            "Compaq CGA",
            "Compaq CGA 2",
            "Compaq Plasma",
            "NCR NGA",
            "Olivetti OGC",
            "Quadram Quadcolor I",
            "Sigma Designs Color 400",
            "Wyse WY-700",
            "Yamaha V6355D",
            "Image Manager 1024",
            "Iskra EGA",
            "JEGA (Japanese EGA)",
            "Oak OTI-037C",
            "Oak OTI-067",
            "Oak OTI-077",
            "Paradise PVGA1A",
            "Paradise WD90C11",
            "Paradise WD90C30",
            "Realtek RTG3105",
            "Realtek RTG3106",
            "Video 7 VGA 1024i",
            "Tseng Labs ET2000",
            "Tseng Labs ET3000AX (ISA)",
            "Tseng Labs ET4000AX (ISA)",
            "Tseng Labs ET4000AX (MCA)",
            "Tseng Labs ET4000/W32 (ISA)",
            "Tseng Labs ET4000/W32 (VLB)",
            "Tseng Labs ET4000/W32i (ISA)",
            "Tseng Labs ET4000/W32i (VLB)",
            "Tseng Labs ET4000/W32p (VLB)",
            "Tseng Labs ET4000/W32p (PCI)",
            "Trident TVGA 8900B/C/D",
            "Trident TVGA 9000B",
            "Trident TGUI 9400CXi (VLB)",
            "Trident TGUI 9440 (VLB)",
            "Trident TGUI 9440 (PCI)",
            "Trident TGUI 9660 (PCI)",
            "Trident TGUI 9680 (PCI)",
            "Cirrus Logic GD5401 (ISA)",
            "Cirrus Logic GD5402 (ISA)",
            "Cirrus Logic GD5420 (ISA)",
            "Cirrus Logic GD5422 (ISA)",
            "Cirrus Logic GD5424 (VLB)",
            "Cirrus Logic GD5426 (ISA)",
            "Cirrus Logic GD5426 (VLB)",
            "Cirrus Logic GD5426 (MCA)",
            "Cirrus Logic GD5428 (ISA)",
            "Cirrus Logic GD5428 (VLB)",
            "Cirrus Logic GD5428 (MCA)",
            "Cirrus Logic GD5429 (ISA)",
            "Cirrus Logic GD5429 (VLB)",
            "Cirrus Logic GD5430 (VLB)",
            "Cirrus Logic GD5430 (PCI)",
            "Cirrus Logic GD5434 (ISA)",
            "Cirrus Logic GD5434 (VLB)",
            "Cirrus Logic GD5434 (PCI)",
            "Cirrus Logic GD5436 (PCI)",
            "Cirrus Logic GD5440 (PCI)",
            "Cirrus Logic GD5446 (PCI)",
            "Cirrus Logic GD5480 (PCI)",
            "S3 86C911 (ISA)",
            "S3 86C924 (ISA)",
            "S3 86C801 (ISA)",
            "S3 86C805 (ISA)",
            "S3 86C805 (VLB)",
            "S3 86C928 (ISA)",
            "S3 86C928 (VLB)",
            "S3 86C928 (PCI)",
            "S3 Trio32 (VLB)",
            "S3 Trio32 (PCI)",
            "S3 Trio64 (VLB)",
            "S3 Trio64 (PCI)",
            "S3 Trio64V+ (VLB)",
            "S3 Trio64V+ (PCI)",
            "S3 Trio64V2/DX (PCI)",
            "S3 Vision864 (VLB)",
            "S3 Vision864 (PCI)",
            "S3 Vision964 (VLB)",
            "S3 Vision964 (PCI)",
            "S3 Vision868 (PCI)",
            "S3 Vision968 (VLB)",
            "S3 Vision968 (PCI)",
            "S3 ViRGE (325, PCI)",
            "S3 ViRGE/DX (375, PCI)",
            "S3 ViRGE/GX (385, PCI)",
            "S3 ViRGE/GX2 (357, PCI)",
            "S3 ViRGE/GX2 (357, AGP)",
            "S3 ViRGE/VX (988, PCI)",
            "S3 Trio3D/2X (PCI)",
            "S3 Trio3D/2X (AGP)",
            "ATI EGA Wonder 800+",
            "ATI VGA Wonder (18800)",
            "ATI VGA Edge-8 (18800)",
            "ATI VGA Wonder XL (28800-5)",
            "ATI VGA Wonder XL+ (28800-6)",
            "ATI Mach8 (ISA)",
            "ATI Mach32 (ISA)",
            "ATI Mach32 (VLB)",
            "ATI Mach32 (MCA)",
            "ATI Mach32 (PCI)",
            "ATI Mach64 GX (ISA)",
            "ATI Mach64 GX (VLB)",
            "ATI Mach64 GX (PCI)",
            "ATI Mach64 CT (PCI)",
            "ATI Mach64 VT (PCI)",
            "ATI Mach64 VT2 (PCI)",
            "Matrox Millennium (PCI)",
            "Matrox Millennium II (PCI)",
            "Matrox Millennium II (AGP)",
            "Matrox Mystique (PCI)",
            "Matrox Mystique 220 (PCI)",
            "Matrox Productiva G100 (AGP)",
            "3dfx Voodoo Banshee (PCI)",
            "3dfx Voodoo Banshee (AGP)",
            "3dfx Velocity 100 (AGP)",
            "3dfx Velocity 200 (AGP)",
            "3dfx Voodoo3 1000 (PCI)",
            "3dfx Voodoo3 1000 (AGP)",
            "3dfx Voodoo3 2000 (PCI)",
            "3dfx Voodoo3 2000 (AGP)",
            "3dfx Voodoo3 3000 (PCI)",
            "3dfx Voodoo3 3000 (AGP)",
            "3dfx Voodoo3 3500 (AGP)",
            "Chips & Technologies B69000 (PCI)",
            "Bochs SVGA (PCI)"
        };

        public static readonly List<string> SecondaryVideoCards = new()
        {
            "None",
            "IBM Monochrome Display Adapter (MDA)",
            "Hercules Graphics Card",
            "Hercules Graphics Card Plus",
            "Hercules InColor Card",
            "IBM Color Graphics Adapter (CGA)",
            "Plantronics Colorplus",
            "Compaq CGA",
            "Cirrus Logic GD5436 (PCI)",
            "Cirrus Logic GD5446 (PCI)",
            "Matrox Millennium (PCI)",
            "Matrox Millennium II (PCI)",
            "Matrox Millennium II (AGP)",
            "Matrox Mystique (PCI)",
            "Matrox Mystique 220 (PCI)"
        };

        public static readonly List<string> ScreenTypes = new()
        {
            "RGB Color",
            "RGB Grayscale",
            "Amber monitor",
            "Green monitor",
            "White monitor"
        };

        public static readonly List<string> GrayscaleConversions = new()
        {
            "BT601 (NTSC/PAL)",
            "BT709 (HDTV)",
            "Average"
        };

        public static readonly List<string> SoundCards = new()
        {
            "None",
            "Internal / Motherboard Sound",
            "PC Speaker",
            "Tandy 1000 Sound",
            "Creative Music System (CMS) / Game Blaster",
            "Innovation SSI-2001 (SID)",
            "AdLib Music Synthesizer Card",
            "AdLib Gold 1000",
            "Sound Blaster 1.0 (CT1300)",
            "Sound Blaster 1.5 (CT1320C)",
            "Sound Blaster 2.0 (CT1350)",
            "Sound Blaster Pro v1 (CT1330A)",
            "Sound Blaster Pro v2 (CT1600)",
            "Sound Blaster 16 (CT1740/CT2230)",
            "Sound Blaster 16 PnP (CT2940/CT2950)",
            "Sound Blaster 16 PnP IDE (CT2980)",
            "Sound Blaster 32 PnP (CT3600)",
            "Sound Blaster AWE32 (CT2760/CT3900)",
            "Sound Blaster AWE32 PnP (CT3980/CT3990)",
            "Sound Blaster AWE64 Value (CT4500)",
            "Sound Blaster AWE64 (CT4520)",
            "Sound Blaster AWE64 Gold (CT4390)",
            "Sound Blaster Vibra 16C (CT2920)",
            "Sound Blaster Vibra 16CL (CT2960)",
            "Sound Blaster Vibra 16S (CT2800)",
            "Sound Blaster Vibra 16XV (CT4170)",
            "Sound Blaster MCV (CT5320, MCA)",
            "Sound Blaster Pro MCV (CT5330, MCA)",
            "Sound Blaster 16 Reply (MCA)",
            "Sound Blaster PCI 128 (ES1371)",
            "Sound Blaster AudioPCI 64V (ES1370)",
            "Sound Blaster AudioPCI (CT5880)",
            "Ensoniq AudioPCI (ES1373)",
            "Ensoniq Soundscape",
            "Gravis UltraSound (GUS) Classic",
            "Gravis UltraSound v3.4 / v3.7",
            "Gravis UltraSound MAX",
            "Gravis UltraSound ACE",
            "Gravis UltraSound Extreme",
            "Gravis UltraSound PnP",
            "Media Vision Pro Audio Spectrum (PAS)",
            "Media Vision Pro Audio Spectrum Plus",
            "Media Vision Pro Audio Spectrum 16",
            "Media Vision Pro Audio Spectrum 16D",
            "ESS AudioDrive ES488",
            "ESS AudioDrive ES688",
            "ESS AudioDrive ES1488",
            "ESS AudioDrive ES1688",
            "ESS AudioDrive ES1788",
            "ESS AudioDrive ES1868",
            "ESS AudioDrive ES1869",
            "ESS Solo-1 (ES1938/ES1946, PCI)",
            "Aztech Sound Galaxy Pro 16",
            "Aztech Sound Galaxy Nova 16",
            "Aztech Sound Galaxy Washington 16",
            "Aztech Sound Galaxy Waverider 32+",
            "Crystal CS4232",
            "Crystal CS4235",
            "Crystal CS4236B",
            "Crystal CS4297A (AC'97)",
            "OPTi 82C930",
            "OPTi 82C931",
            "Windows Sound System (WSS)",
            "Yamaha OPL3-SA (YMF701)",
            "Yamaha OPL3-SA2 (YMF718)",
            "Yamaha OPL3-SA3 (YMF719)",
            "C-Media CMI8338 (PCI)",
            "C-Media CMI8738 (PCI)"
        };

        public static readonly List<string> FmDrivers = new()
        {
            "Nuked (more accurate)",
            "YMFM (faster)"
        };

        public static readonly List<string> AudioSampleRates = new()
        {
            "22050 Hz",
            "44100 Hz",
            "48000 Hz",
            "96000 Hz"
        };

        public static readonly List<string> AudioOutputDevices = new()
        {
            "Default Audio Endpoint",
            "WASAPI: Speakers / Headphones",
            "DirectSound: Primary Sound Driver",
            "Null Audio Output"
        };

        public static readonly List<string> MidiDevices = new()
        {
            "None",
            "Roland MPU-401 (Intelligent)",
            "Roland MPU-401 (UART)",
            "Sound Blaster MIDI",
            "FluidSynth SoundFont Synth",
            "Windows MIDI Mapper",
            "Host MIDI In/Out"
        };

        public static readonly List<string> NetworkAdapters = new()
        {
            "None",
            "Novell NE1000 (8-bit ISA)",
            "Novell NE2000 (16-bit ISA)",
            "3Com EtherLink II (3C503)",
            "3Com Fast EtherLink III (3C590/3C905)",
            "Realtek RTL8029AS (PCI 10Mbps)",
            "Realtek RTL8139C+ (PCI 100Mbps)",
            "AMD PCnet-ISA (AM79C960)",
            "AMD PCnet-PCI II (AM79C970A)",
            "Intel EtherExpress Pro/100B (82557)"
        };

        public static readonly List<string> NetworkModes = new() { "Null Driver", "SLiRP", "Local Switch" };

        public static readonly List<string> HdControllers = new()
        {
            "None",
            "Internal controller",
            "[ISA] Acculogic sIDE-1/16",
            "[ISA] Standard IDE",
            "[ISA] Standard 2-channel IDE",
            "[ISA] XT-IDE",
            "[ISA] DTC 5150X MFM",
            "[ISA] Western Digital WD1002A-WX1 MFM",
            "[ISA16] Standard AT IDE",
            "[ISA16] Western Digital WD1003 MFM",
            "[ISA16] Western Digital WD1007V-SE1 ESDI",
            "[VLB] Standard VLB IDE",
            "[PCI] CMD PCI-640 IDE",
            "[PCI] Intel PIIX IDE (Internal)"
        };

        public static readonly List<string> ScsiControllers = new()
        {
            "None",
            "[ISA] Rancho RT1000B",
            "[ISA] Trantor T128",
            "[ISA] Trantor T130B",
            "[ISA16] Adaptec AHA-1540A",
            "[ISA16] Adaptec AHA-1542B",
            "[ISA16] Adaptec AHA-1542C",
            "[ISA16] Adaptec AHA-1542CF",
            "[ISA16] Adaptec AHA-1542CP",
            "[ISA16] BusLogic BT-542B",
            "[ISA16] BusLogic BT-545S",
            "[MCA] Adaptec AHA-1640",
            "[VLB] BusLogic BT-445S",
            "[PCI] Adaptec AHA-2940UW",
            "[PCI] AMD PCscsi (AM53C974)",
            "[PCI] BusLogic BT-958",
            "[PCI] NCR 53C810",
            "[PCI] NCR 53C815",
            "[PCI] NCR 53C875",
            "[PCI] QLogic QLA1040B",
            "[PCI] QLogic QLA1280",
            "[PCI] QLogic QLA12160A"
        };

        public static readonly List<string> CdromControllers = new()
        {
            "None",
            "Sound Blaster 16 CD-ROM (Panasonic/Mitsumi/Sony)",
            "Sony CDU-31A/33A",
            "Mitsumi FX001",
            "Aztech (Galaxy)",
            "Creative OmniCD",
            "GoldStar Prime 2C"
        };

        public static readonly List<string> HddBuses = new()
        {
            "IDE",
            "SCSI",
            "ESDI",
            "MFM"
        };

        public static readonly List<string> HddChannels = new()
        {
            "0:0",
            "0:1",
            "1:0",
            "1:1",
            "2:0",
            "2:1",
            "3:0",
            "3:1"
        };

        /// <summary>Every drive model of the engine's hdd_speed_presets table (src/disk/hdd.c), in the engine's order.</summary>
        public static readonly List<string> HddModels = AuthenticCatalog.HddPresets.Select(p => p.Name).ToList();

        /// <summary>
        /// The preset config.c uses when a disk has no hdd_NN_speed key: a 1997 5400 RPM drive for IDE / ESDI / ATAPI / SCSI,
        /// the RAM disk otherwise.
        /// </summary>
        public static string DefaultHddModel(string? bus)
        {
            string internalName = (bus ?? "IDE").ToUpperInvariant() is "IDE" or "ESDI" or "ATAPI" or "SCSI" ? "1997_5400rpm" : "ramdisk";
            foreach (var p in AuthenticCatalog.HddPresets)
                if (p.Internal == internalName) return p.Name;
            return HddModels.FirstOrDefault() ?? string.Empty;
        }

        public static readonly List<string> HddAudioOptions = new()
        {
            "None",
            "Standard IDE Audio"
        };

        public static readonly List<string> FloppyControllers = new()
        {
            "None",
            "Internal / Motherboard FDC",
            "Standard IBM PC/XT FDC",
            "Standard IBM AT 1.2M/1.44M FDC",
            "CompatiCard IV (4 Drives)",
            "DTC 7287 Enhanced FDC"
        };

        public static readonly List<string> MouseTypes = new()
        {
            "None",
            "Standard PS/2 Mouse",
            "IntelliMouse (PS/2 Wheel)",
            "Microsoft Serial Mouse",
            "Logitech Serial Mouse",
            "Mouse Systems Serial Mouse",
            "Bus Mouse (InPort)"
        };

        public static List<string> MachineFamilies => MachineTypes;

                public static readonly List<OpticalDriveDevice> OpticalDrives = new()
        {
// Auto-generated from cdrom.h
            new OpticalDriveDevice("none", "None", "BUS_TYPE_BOTH", 0, false),
            new OpticalDriveDevice("86dvd100", "86Box 86B_DVD 1.00", "BUS_TYPE_BOTH", -1, true),
            new OpticalDriveDevice("86cd", "86Box 86B_CD 3.50", "BUS_TYPE_BOTH", -1, false),
            new OpticalDriveDevice("86cd100", "86Box 86B_CD 1.00", "BUS_TYPE_BOTH", -1, false),
            new OpticalDriveDevice("86dvd", "86Box 86B_DVD 5.00", "BUS_TYPE_BOTH", -1, true),
            new OpticalDriveDevice("acer_656a", "ACER 656A 8.4D (8x)", "BUS_TYPE_IDE", 8, false),
            new OpticalDriveDevice("acer_8432ia", "ACER 8432IA 5.AX (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("aopen_924e", "AOpen CD-924E A205 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("aopen_948e", "AOpen CD-948E 4.02 (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("aopen_952e", "AOpen CD-952E 2.01 (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("aopen_956e", "AOpen CD-956E 2480 (56x)", "BUS_TYPE_IDE", 56, false),
            new OpticalDriveDevice("aopen_9632", "AOpen DVD-9632 1.15 (32x)", "BUS_TYPE_IDE", 32, true),
            new OpticalDriveDevice("asus_500", "ASUS CD-S500/A 1.41 (50x)", "BUS_TYPE_IDE", 50, false),
            new OpticalDriveDevice("asus_520", "ASUS CD-S520/A4 1.6K (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("asus_e616", "ASUS DVD-E616P2 1.08 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("azt_cda", "AZT CDA46802I 1.15 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("benq_656a", "BENQ CD-656A 56AI (56x)", "BUS_TYPE_IDE", 56, false),
            new OpticalDriveDevice("btc_16xa", "BTC CD-ROM BCD16XA U2.2 (16x)", "BUS_TYPE_IDE", 16, false),
            new OpticalDriveDevice("btc_24x", "BTC CD-ROM BCD24X U2.0 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("btc_24xhm", "BTC CD-ROM BCD24XHM V1.0 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("btc_36xh", "BTC CD-ROM BCD36XH U1.0 (36x)", "BUS_TYPE_IDE", 36, false),
            new OpticalDriveDevice("creative_2422", "CREATIVE CD2422E MC10 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("creative_3621", "CREATIVE CD3621E ZC10 (36x)", "BUS_TYPE_IDE", 36, false),
            new OpticalDriveDevice("creative_5220", "CREATIVE CD5220E 2.02 (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("creative_d2240", "CREATIVEDVD-ROM DVD2240E 1.7A (20x)", "BUS_TYPE_IDE", 20, true),
            new OpticalDriveDevice("ecs_300", "ECS 300ESD V200 (3x)", "BUS_TYPE_IDE", 3, false),
            new OpticalDriveDevice("ecs_600", "ECS 600ESD V300 (6x)", "BUS_TYPE_IDE", 6, false),
            new OpticalDriveDevice("goldstar", "GOLDSTAR CRD-8160B 3.14 (16x)", "BUS_TYPE_IDE", 16, false),
            new OpticalDriveDevice("goldstar_8240b", "GOLDSTAR CRD-8240B 1.11 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("goldstar_8320b", "GOLDSTAR CRD-8320B 1.10 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("gs_8400b_102", "GOLDSTAR CRD-8400B 1.02 (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("gs_8400b_103", "GOLDSTAR CRD-8400B 1.03 (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("goldstar_8400b", "GOLDSTAR CRD-8400B 1.12 (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("goldstar_8484b", "GOLDSTAR CRD-8484B 1.03 (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("goldstar_r542b", "GOLDSTAR GCD-R542B 1.20 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("goldstar_r560b", "GOLDSTAR GCD-R560B 1.00 (6x)", "BUS_TYPE_IDE", 6, false),
            new OpticalDriveDevice("goldstar_r580b", "GOLDSTAR GCD-R580B 1.04 (8x)", "BUS_TYPE_IDE", 8, false),
            new OpticalDriveDevice("hitachi_r7930", "HITACHI CDR-7930 A5 (8x)", "BUS_TYPE_IDE", 8, false),
            new OpticalDriveDevice("hitachi_r8130", "HITACHI CDR-8130 0020 (16x)", "BUS_TYPE_IDE", 16, false),
            new OpticalDriveDevice("hitachi_r8330", "HITACHI CDR-8330 0007 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("hitachi_r8435", "HITACHI CDR-8435 0010 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("hitachi_2000", "HITACHI DVD-ROM GD-2000 A012 (20x)", "BUS_TYPE_IDE", 20, true),
            new OpticalDriveDevice("hitachi_2500", "HITACHI DVD-ROM GD-2500 0101 (24x)", "BUS_TYPE_IDE", 24, true),
            new OpticalDriveDevice("hitachi_7500", "HITACHI GD-7500 A1 (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("hldtst_8526b", "HL-DT-ST CD-ROM GCR-8526B 1.01 (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("hldtst_8082", "HL-DT-ST DVDROM GDR-8082N 0002 (24x)", "BUS_TYPE_IDE", 24, true),
            new OpticalDriveDevice("hldtst_8163", "HL-DT-ST DVDROM GDR-8163B 0L23 (52x)", "BUS_TYPE_IDE", 52, true),
            new OpticalDriveDevice("hldtst_4160", "HL-DT-ST DVDRAM GSA-4160B A302 (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("hldtst_h42l", "HL-DT-ST DVDRAM GSA-H42L SL01 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("hp_7200", "HP 7200e 1.34 (6x)", "BUS_TYPE_IDE", 6, false),
            new OpticalDriveDevice("hp_1040i", "HP DVD1040i 4H13 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("kenwood_421", "KENWOOD CD-ROM UCR-421 208E (72x)", "BUS_TYPE_IDE", 72, false),
            new OpticalDriveDevice("leoptics_24x", "LEOPTICS CD-ROM 24X 4.6C (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("lg_8160b", "LG CD-ROM CRD-8160B 1.15 (16x)", "BUS_TYPE_IDE", 16, false),
            new OpticalDriveDevice("lg_8240b", "LG CD-ROM CRD-8240B 1.19 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("lg_8245b", "LG CD-ROM CRN-8245B 1.30 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("lg_8322b", "LG CD-ROM CRD-8322B 1.24 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("lg_8400c", "LG CD-ROM CRD-8400C 1.02 (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("lg_8482b", "LG CD-ROM CRD-8482B 1.00 (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("lg_8522b", "LG CD-ROM CRD-8522B 2.03 (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("lg_d820b", "LG DVDROM DRD-820B 1.04 (24x)", "BUS_TYPE_IDE", 24, true),
            new OpticalDriveDevice("lg_d8160b", "LG DVDROM DRD-8160B 1.01 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("liteon_204", "LITEON CD-ROM LTN204 1017 (20x)", "BUS_TYPE_IDE", 20, false),
            new OpticalDriveDevice("liteon_242", "LITEON CD-ROM LTN242 2048 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("liteon_ltn242", "LITEON CD-ROM LTN242 HP12 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("liteon_301", "LITEON CD-ROM LTN301 1027 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("liteon_382", "LITEON CD-ROM LTN382 2019 (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("liteon_403", "LITEON CD-ROM LTN403L DQ19 (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("liteon_48125s", "LITEON CD-ROM LTR48125S 1S07 (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("liteon_526d", "LITEON CD-ROM LTN526D YSR5 (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("liteon_166d", "LITEON CD-ROM LTD166 9S14 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("liteon_401s", "LITE-ON DVD+RW LDW-401S ES0G (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("liteon_812s", "LITE-ON DVDRW SOHW-812S US0A (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("liteon_812s", "LITE-ON DVDRW SOHW-1633S BS0G (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("liteon_167t", "LITE-ON DVD SOHD-167T 9S13 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("maddog_56x", "MAD DOG LIGHTNING 56X 1.0 (56x)", "BUS_TYPE_IDE", 56, false),
            new OpticalDriveDevice("maddog_16x", "MAD DOG ENTERTAINER 16X 1.0 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("matshita_571", "MATSHITA CR-571 1.0e (2x)", "BUS_TYPE_IDE", 2, false),
            new OpticalDriveDevice("matshita_572", "MATSHITA CR-572 1.0j (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("matshita_574", "MATSHITA CR-574 P.11 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("matshita_581", "MATSHITA CD-ROM CR-581-B 1.05 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("matshita_583", "MATSHITA CD-ROM CR-583 1.07 (8x)", "BUS_TYPE_IDE", 8, false),
            new OpticalDriveDevice("matshita_585", "MATSHITA CD-ROM CR-585 Z18P (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("matshita_587", "MATSHITA CD-ROM CR-587 7S13 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("matshita_588", "MATSHITA CD-ROM CR-588 LS15 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("matshita_594", "MATSHITA CD-ROM CR-594-C PA05 (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("matshita_8587", "MATSHITA DVD-ROM SR-8587 CA5B (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("matshita_8584", "MATSHITA DVD-ROM SR-8584 e15C (52x)", "BUS_TYPE_IDE", 52, true),
            new OpticalDriveDevice("mitsumi_400e", "MITSUMI FX400E K02 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("mitsumi_400c", "MITSUMI CRMC-FX400G l07 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("mitsumi_600s", "MITSUMI CRMC-FX600S p07 (6x)", "BUS_TYPE_IDE", 6, false),
            new OpticalDriveDevice("mitsumi_810t4", "MITSUMI CRMC-FX810T4 a03 (8x)", "BUS_TYPE_IDE", 8, false),
            new OpticalDriveDevice("mitsumi_120t", "MITSUMI CRMC-FX120T w02 (12x)", "BUS_TYPE_IDE", 12, false),
            new OpticalDriveDevice("mitsumi_240s", "MITSUMI CRMC-FX240S g05 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("mitsumi_322m", "MITSUMI CRMC-FX322M p01 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("mitsumi_480ate", "MITSUMI CR-480ATE 1.0E (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("mitsumi_4820t", "MITSUMI CRMC-FX4820T D02A (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("nec_260_early", "NEC CD-ROM DRIVE:260 1.00 (2x)", "BUS_TYPE_IDE", 2, false),
            new OpticalDriveDevice("nec_260", "NEC CD-ROM DRIVE:260 1.01 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("nec_272", "NEC CD-ROM DRIVE:272 3.02 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("nec_273_early", "NEC CD-ROM DRIVE:273 4.20 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("nec_273", "NEC CD-ROM DRIVE:273 4.25 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("nec_280_early", "NEC CD-ROM DRIVE:280 1.05 (6x)", "BUS_TYPE_IDE", 6, false),
            new OpticalDriveDevice("nec_280", "NEC CD-ROM DRIVE:280 3.08 (8x)", "BUS_TYPE_IDE", 8, false),
            new OpticalDriveDevice("nec_289", "NEC CD-ROM DRIVE:289 1.00 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("nec_1300a", "NEC CDR-1300A 1.05 (6x)", "BUS_TYPE_IDE", 6, false),
            new OpticalDriveDevice("nec_1801a", "NEC CDR-1801A J111 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("nec_1900a", "NEC CDR-1900A 1.00 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("nec_3002a", "NEC CDR-3002A C000 (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("nec_d1300a", "NEC ND-1300A 1.0B (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("nec_d3500a", "NEC ND-3500A 2.1A (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("newmax_7120", "NEWMAX CCD-7120 4.00 (16x)", "BUS_TYPE_IDE", 16, false),
            new OpticalDriveDevice("octek_810", "OCTEK CDR-810 1020 (10x)", "BUS_TYPE_IDE", 10, false),
            new OpticalDriveDevice("philips_323", "PHILIPS CD-ROM PCA323CD 2.5 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("philips_4401", "PHILIPS CDD4401/31 C1.7 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("philips_403", "PHILIPS CD-ROM PCA403CD U31P (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("philips_4801", "PHILIPS CDD4801/71 C1.3 (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("pioneer_a12x", "PIONEER CD-ROM DR-A12X 1.00 (12x)", "BUS_TYPE_IDE", 12, false),
            new OpticalDriveDevice("pioneer_u24x", "PIONEER CD-ROM DR-U24X 1.00 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("pioneer_115lh", "PIONEER DVD-115LH 1.24 (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("pioneer_121", "PIONEER DVD-121 196L (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("pioneer_mcc", "PIONEER DVD-RAM DVR-MCC 1.00 (24x)", "BUS_TYPE_IDE", 24, true),
            new OpticalDriveDevice("pioneer_106d", "PIONEER DVD-RAM DVR-106D 1.08 (32x)", "BUS_TYPE_IDE", 32, true),
            new OpticalDriveDevice("pioneer_110d", "PIONEER DVD-RAM DVR-110D 1.41 (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("pioneer_800a", "PLEXTOR DVD-ROM PX-800A 81HA (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("ricoh_7040", "RICOH MP7040A 1.60 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("samsung_3231", "SAMSUNG CD-ROM SCR-3231 S101 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("samsung_140", "SAMSUNG CD-ROM SC-140 BS14 (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("samsung_148f", "SAMSUNG CD-ROM SC-148F PS07 (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("samsung_152", "SAMSUNG CD-ROM SC-152 C400 (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("samsung_616e", "SAMSUNG DVD-ROM SD-616E F503 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("samsung_162c", "SAMSUNG DVD-ROM SH-D162C TS05 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("sanyo_crd254p", "SANYO CRD-254P 1.05 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("sanyo_crd820p", "SANYO CRD-820P 1.04 (20x)", "BUS_TYPE_IDE", 20, false),
            new OpticalDriveDevice("sony_76", "SONY CD-ROM CDU76 1.0i (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("sony_311", "SONY CD-ROM CDU311 3.0h (8x)", "BUS_TYPE_IDE", 8, false),
            new OpticalDriveDevice("sony_611", "SONY CD-ROM CDU611 2.2c (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("sony_4011", "SONY CD-ROM CDU4011 B2BA (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("sony_5225", "SONY CD-ROM CDU5225 NYS4 (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("sony_530a", "SONY DVD-ROM DRU-530A U305 (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("sony_710a", "SONY DVD-ROM DRU-710A 11BA (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("sony_810a", "SONY DVD-ROM DRU-810A 71BD (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("sony_1612", "SONY DVD-ROM DDU1612 BA01 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("sony_1615", "SONY DVD-ROM DDU1615 B2EE (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("teac_55a", "TEAC CD 55A 2.10 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("teac_520", "TEAC CD-SN250 N.0A (10x)", "BUS_TYPE_IDE", 10, false),
            new OpticalDriveDevice("teac_516e", "TEAC CD-516E 1.0G (16x)", "BUS_TYPE_IDE", 16, false),
            new OpticalDriveDevice("teac_224e", "TEAC CD-224E 4.0D (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("teac_524ea", "TEAC CD-524EA 3.0D (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("teac_532e", "TEAC CD-532E 2.0A (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("teac_532ea", "TEAC CD-532EA 3.0A (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("teac_540e", "TEAC CD-540E 2.0U (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("teac_520e", "TEAC CD-P520E 2.0R (52x)", "BUS_TYPE_IDE", 52, false),
            new OpticalDriveDevice("teac_516d", "TEAC DV-516D 1.0A (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("toshiba_5302ta", "TOSHIBA CD-ROM XM-5302TA 0305 (4x)", "BUS_TYPE_IDE", 4, false),
            new OpticalDriveDevice("toshiba_1502b", "TOSHIBA CD-ROM XM-1502B RA70 (10x)", "BUS_TYPE_IDE", 10, false),
            new OpticalDriveDevice("toshiba_5702b", "TOSHIBA CD-ROM XM-5702B TA70 (12x)", "BUS_TYPE_IDE", 12, false),
            new OpticalDriveDevice("toshiba_6002b", "TOSHIBA CD-ROM XM-6002B VE70 (16x)", "BUS_TYPE_IDE", 16, false),
            new OpticalDriveDevice("toshiba_6102b", "TOSHIBA CD-ROM XM-6102B WA70 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("toshiba_c2612", "TOSHIBA CD-ROM SD-C2612 1D21 (24x)", "BUS_TYPE_IDE", 24, false),
            new OpticalDriveDevice("toshiba_6202b", "TOSHIBA CD-ROM XM-6202B 1512 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("toshiba_6402b", "TOSHIBA CD-ROM XM-6402B 1008 (32x)", "BUS_TYPE_IDE", 32, false),
            new OpticalDriveDevice("toshiba_6502b", "TOSHIBA CD-ROM XM-6502B 1013 (40x)", "BUS_TYPE_IDE", 40, false),
            new OpticalDriveDevice("toshiba_6702b", "TOSHIBA CD-ROM XM-6702B 1007 (48x)", "BUS_TYPE_IDE", 48, false),
            new OpticalDriveDevice("toshiba_m1202", "TOSHIBA DVD-ROM SD-M1202 1020 (32x)", "BUS_TYPE_IDE", 32, true),
            new OpticalDriveDevice("toshiba_m1402", "TOSHIBA DVD-ROM SD-M1402 1010 (40x)", "BUS_TYPE_IDE", 40, true),
            new OpticalDriveDevice("toshiba_m1802", "TOSHIBA DVD-ROM SD-M1802 1B08 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("toshiba_m1912", "TOSHIBA DVD-ROM SD-M1912 TM01 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("toshiba_m2012", "TOSHIBA DVD-ROM SD-M2012 TU01 (48x)", "BUS_TYPE_IDE", 48, true),
            new OpticalDriveDevice("wearnes_110", "WEARNES CDD-110 1.02 (2x)", "BUS_TYPE_IDE", 2, false),
            new OpticalDriveDevice("chinon_431", "[SCSI-1] CHINON CD-ROM CDS-431 H42 (1x)", "BUS_TYPE_SCSI", 1, false),
            new OpticalDriveDevice("chinon_435", "[SCSI-1] CHINON CD-ROM CDX-435 M62 (2x)", "BUS_TYPE_SCSI", 2, false),
            new OpticalDriveDevice("dec_42", "[SCSI-1] DEC RRD42 (C) DEC 4.5d (1x)", "BUS_TYPE_SCSI", 1, false),
            new OpticalDriveDevice("dec_45", "[SCSI-1] DEC RRD45 (C) DEC 0436 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("grundig_100", "[SCSI-2] GRUNDIG CDR100 1.20 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("matshita_501", "[SCSI-1] MATSHITA CD-ROM CR-501 1.0b (1x)", "BUS_TYPE_SCSI", 1, false),
            new OpticalDriveDevice("matshita_504", "[SCSI-1] MATSHITA CD-ROM CR-504 4.0i (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("matshita_506", "[SCSI-1] MATSHITA CD-ROM CR-506 8.0h (8x)", "BUS_TYPE_SCSI", 8, false),
            new OpticalDriveDevice("matshita_508", "[SCSI-2] MATSHITA CD-ROM CR-508 XS03 (24x)", "BUS_TYPE_SCSI", 24, false),
            new OpticalDriveDevice("nec_25", "[SCSI-1] NEC CD-ROM DRIVE:25 1.0a (2x)", "BUS_TYPE_SCSI", 2, false),
            new OpticalDriveDevice("nec_38", "[SCSI-2] NEC CD-ROM DRIVE:38 1.00 (1x)", "BUS_TYPE_SCSI", 1, false),
            new OpticalDriveDevice("nec_75", "[SCSI-1] NEC CD-ROM DRIVE:75 1.03 (1x)", "BUS_TYPE_SCSI", 1, false),
            new OpticalDriveDevice("nec_77", "[SCSI-1] NEC CD-ROM DRIVE:77 1.06 (1x)", "BUS_TYPE_SCSI", 1, false),
            new OpticalDriveDevice("nec_211", "[SCSI-2] NEC CD-ROM DRIVE:211 1.00 (3x)", "BUS_TYPE_SCSI", 3, false),
            new OpticalDriveDevice("nec_464", "[SCSI-2] NEC CD-ROM DRIVE:464 1.05 (3x)", "BUS_TYPE_SCSI", 3, false),
            new OpticalDriveDevice("nec_900", "[SCSI-2] NEC CD-ROM DRIVE:900 2.5a (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("philips_2000", "[SCSI-2] PHILIPS CDD2000 1.26 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("philips_2600", "[SCSI-2] PHILIPS CDD2600 1.07 (6x)", "BUS_TYPE_SCSI", 6, false),
            new OpticalDriveDevice("pioneer_604x", "[SCSI-2] PIONEER CD-ROM DRM-604X 2403 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("pioneer_u124x", "[SCSI-2] PIONEER CD-ROM DR-U124X 4021 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("pioneer_u16s", "[SCSI-2] PIONEER CD-ROM DR-U16S 0066 (36x)", "BUS_TYPE_SCSI", 36, false),
            new OpticalDriveDevice("pioneer_305s", "[SCSI-2] PIONEER DVD-ROM DVD-305S 1.00 (40x)", "BUS_TYPE_SCSI", 40, true),
            new OpticalDriveDevice("plextor_43ch", "[SCSI-2] PLEXTOR CD-ROM PX-43CH 0204 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("plextor_83cs", "[SCSI-2] PLEXTOR CD-ROM PX-83CS 1.01 (8x)", "BUS_TYPE_SCSI", 8, false),
            new OpticalDriveDevice("plextor_12cs", "[SCSI-2] PLEXTOR CD-ROM PX-12CS 1.04 (12x)", "BUS_TYPE_SCSI", 12, false),
            new OpticalDriveDevice("plextor_12ts", "[SCSI-2] PLEXTOR CD-ROM PX-12TS 1.01 (12x)", "BUS_TYPE_SCSI", 12, false),
            new OpticalDriveDevice("plextor_20ts", "[SCSI-1] PLEXTOR CD-ROM PX-20TSi 0101 (20x)", "BUS_TYPE_SCSI", 20, false),
            new OpticalDriveDevice("plextor_32ts", "[SCSI-2] PLEXTOR CD-ROM PX-32TS 1.03 (32x)", "BUS_TYPE_SCSI", 32, false),
            new OpticalDriveDevice("plextor_40ts", "[SCSI-2] PLEXTOR CD-ROM PX-40TS 1.14 (40x)", "BUS_TYPE_SCSI", 40, false),
            new OpticalDriveDevice("sanyo_crd254sh", "[SCSI-2] SANYO CRD-254SH 1.05 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("shinaken_3x1s", "[SCSI-1] ShinaKen CD-ROM DM-3x1S 1.04 (3x)", "BUS_TYPE_SCSI", 3, false),
            new OpticalDriveDevice("sony_541", "[SCSI-1] SONY CD-ROM CDU-541 1.0i (1x)", "BUS_TYPE_SCSI", 1, false),
            new OpticalDriveDevice("sony_561", "[SCSI-2] SONY CD-ROM CDU-561 1.9a (2x)", "BUS_TYPE_SCSI", 2, false),
            new OpticalDriveDevice("sony_76s", "[SCSI-2] SONY CD-ROM CDU-76S 1.00 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("teac_50", "[SCSI-2] TEAC CD 50 1.00 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("teac_55s", "[SCSI-2] TEAC CD-ROM R55S 1.0R (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("teac_516s", "[SCSI-1] TEAC CD-516S 2.0H (16x)", "BUS_TYPE_SCSI", 16, false),
            new OpticalDriveDevice("teac_56s", "[SCSI-2] TEAC CD-ROM R56S 1.0R (24x)", "BUS_TYPE_SCSI", 24, false),
            new OpticalDriveDevice("teac_532s", "[SCSI-1] TEAC CD-532S 3.0A (32x)", "BUS_TYPE_SCSI", 32, false),
            new OpticalDriveDevice("texel_3024", "[SCSI-2] TEXEL CD-ROM DM-3024 1.00 (2x)", "BUS_TYPE_SCSI", 2, false),
            new OpticalDriveDevice("texel_3028", "[SCSI-2] TEXEL CD-ROM DM-3028 1.06 (3x)", "BUS_TYPE_SCSI", 3, false),
            new OpticalDriveDevice("toshiba_xm", "[SCSI-2] TOSHIBA CD-ROM DRIVE:XM 3433 (2x)", "BUS_TYPE_SCSI", 2, false),
            new OpticalDriveDevice("toshiba_3201b", "[SCSI-1] TOSHIBA CD-ROM XM-3201B 3232 (1x)", "BUS_TYPE_SCSI", 1, false),
            new OpticalDriveDevice("toshiba_3301ta", "[SCSI-2] TOSHIBA CD-ROM XM-3301TA 0272 (2x)", "BUS_TYPE_SCSI", 2, false),
            new OpticalDriveDevice("toshiba_3701b", "[SCSI-2] TOSHIBA CD-ROM XM-3701B 0236 (6x)", "BUS_TYPE_SCSI", 6, false),
            new OpticalDriveDevice("toshiba_4101b", "[SCSI-1] TOSHIBA CD-ROM XM-4101B EA40 (2x)", "BUS_TYPE_SCSI", 2, false),
            new OpticalDriveDevice("toshiba_5401b", "[SCSI-2] TOSHIBA CD-ROM XM-5401B 1036 (4x)", "BUS_TYPE_SCSI", 4, false),
            new OpticalDriveDevice("toshiba_5701a", "[SCSI-2] TOSHIBA CD-ROM XM-5701TA 3136 (12x)", "BUS_TYPE_SCSI", 12, false),
            new OpticalDriveDevice("toshiba_6401a", "[SCSI-2] TOSHIBA CD-ROM XM-6401TA 1404 (32x)", "BUS_TYPE_SCSI", 32, false),
            new OpticalDriveDevice("toshiba_m1401", "[SCSI-2] TOSHIBA DVD-ROM SD-M1401 1008 (40x)", "BUS_TYPE_SCSI", 40, true),


        };

        public static string GetCdromDisplayName(string? internalOrDisplay)
        {
            if (string.IsNullOrWhiteSpace(internalOrDisplay) || internalOrDisplay.Equals("none", StringComparison.OrdinalIgnoreCase))
                return "None";

            var match = OpticalDrives.FirstOrDefault(d =>
                d.InternalName.Equals(internalOrDisplay, StringComparison.OrdinalIgnoreCase) ||
                d.DisplayName.Equals(internalOrDisplay, StringComparison.OrdinalIgnoreCase));

            return match?.DisplayName ?? internalOrDisplay;
        }

        public static string GetCdromInternalName(string? internalOrDisplay)
        {
            if (string.IsNullOrWhiteSpace(internalOrDisplay) || internalOrDisplay.Equals("none", StringComparison.OrdinalIgnoreCase))
                return "none";

            var match = OpticalDrives.FirstOrDefault(d =>
                d.DisplayName.Equals(internalOrDisplay, StringComparison.OrdinalIgnoreCase) ||
                d.InternalName.Equals(internalOrDisplay, StringComparison.OrdinalIgnoreCase));

            return match?.InternalName ?? internalOrDisplay;
        }

        public static bool IsCdromDvd(string? internalOrDisplay)
        {
            if (string.IsNullOrWhiteSpace(internalOrDisplay)) return false;
            var match = OpticalDrives.FirstOrDefault(d =>
                d.InternalName.Equals(internalOrDisplay, StringComparison.OrdinalIgnoreCase) ||
                d.DisplayName.Equals(internalOrDisplay, StringComparison.OrdinalIgnoreCase));
            return match?.IsDvd ?? false;
        }

        public static readonly List<MachinePreset> Presets = new()
        {
            new(
                "ibm_5160_xt",
                "IBM PC/XT 5160 (1982)",
                "The IBM XT: an 8088 with a floppy drive and VGA graphics. (The 1981 PC 5150 cannot start a VGA card - its BIOS shows nothing - so this XT is the 8088 default.)",
                "1982 - 1986 (XT Era)",
                "[1979] 8088",
                "[8088] IBM XT (1982)",
                "Intel 8088",
                256,
                "IBM VGA",
                "PC Speaker",
                "360k_525",
                0,
                "PC-DOS 1.10 / 2.10"
            ),
            new(
                "ibm_5170_at",
                "IBM PC/AT 5170 (1984)",
                "286 AT architecture with 16-bit ISA slots, EGA graphics, and 20MB HDD.",
                "1984 - 1987 (286 Era)",
                "[1982] 80286",
                "[ISA] IBM AT",
                "Intel 80286",
                512,
                "IBM EGA",
                "PC Speaker",
                "12m_525",
                20,
                "IBM DOS 3.30"
            ),
            new(
                "award_486_gaming",
                "Super 486 DX2/66 Gaming Rig (1993)",
                "The definitive classic gaming PC with VLB SVGA, Sound Blaster 16, and CD-ROM.",
                "1992 - 1994 (486 Era)",
                "[1994] i486 (Socket 3)",
                "[SiS 471] ASUS VL/I-486SV2GX4",
                "AMD Am486DX2 (Enhanced)",
                16384,
                "Cirrus Logic GD5429 (VLB)",
                "Sound Blaster 16 (CT2230)",
                "144m_35",
                520,
                "MS-DOS 6.22 + Windows 3.11"
            ),
            new(
                "pentium_win95",
                "Socket 7 Pentium 133 Multimedia PC (1995)",
                "The classic Windows 95 workhorse with PCI bus, S3 Trio64, AWE32, and 4x CD-ROM.",
                "1995 - 1997 (Early Pentium)",
                "[1996] Socket 7 (Dual Voltage)",
                "[i430HX] ASUS P/I-P55T2P4",
                "Intel Pentium (P54C)",
                32768,
                "S3 Trio64V+ (PCI)",
                "Sound Blaster AWE32 (CT3900)",
                "144m_35",
                2100,
                "Windows 95 OSR2"
            ),
            new(
                "voodoo2_pentium2",
                "Slot 1 Pentium II 400 3D Gaming PC (1998)",
                "3dfx Voodoo 2 3D accelerator, Sound Blaster Live!, 128MB RAM for Quake II and Half-Life.",
                "1998 - 2000 (3Dfx Era)",
                "[1997] Slot 1",
                "[i440BX] ASUS P2B-LS",
                "Intel Pentium II (Deschutes)",
                131072,
                "3dfx Voodoo Banshee (PCI/AGP)",
                "Sound Blaster AWE64 Gold (CT4390)",
                "144m_35",
                10000,
                "Windows 98 SE"
            )
        };

        // Video Cards Internal Name -> Display Name Mapping
        public static readonly Dictionary<string, string> VideoCardsMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["none"] = "None",
            ["internal"] = "Internal / Motherboard Video",
            ["cga"] = "IBM Color Graphics Adapter (CGA)",
            ["mda"] = "IBM Monochrome Display Adapter (MDA)",
            ["ega"] = "IBM Enhanced Graphics Adapter (EGA)",
            ["vga"] = "IBM Video Graphics Array (VGA)",
            ["agpgart"] = "AGP Graphics Address Remapping Table",
            ["ibm3270pc_vid"] = "IBM 3270 PC Display Adapter",
            ["8514_isa"] = "IBM 8514/A clone (ISA)",
            ["8514_mca"] = "IBM 8514/A (MCA)",
            ["ati18800w"] = "ATI-18800",
            ["ati18800v"] = "ATI 18800-1",
            ["ati18800"] = "ATI VGA Edge 16",
            ["ati28800_5"] = "ATI 28800-5",
            ["ati28800k"] = "ATI Korean VGA",
            ["ati28800k_spc4620p"] = "ATI Korean VGA On-Board (Samsung SPC-4620P)",
            ["ati28800k_spc6033p"] = "ATI Korean VGA On-Board (Samsung SPC-6033P)",
            ["ati28800_wonder1024d_xl_plus"] = "ATI 28800-6",
            ["ati28800w"] = "ATI-28800 (VGA Wonder XL24)",
            ["mach64gx_isa"] = "ATI Mach64GX ISA",
            ["mach64gx_vlb"] = "ATI Mach64GX VLB",
            ["mach64gx_pci"] = "ATI Mach64GX PCI",
            ["mach64ct"] = "ATI Mach64CT",
            ["mach64ct_onboard"] = "ATI Mach64CT (On-Board)",
            ["mach64vt"] = "ATI Mach64VT",
            ["mach64vt2"] = "ATI Mach64VT2",
            ["mach64vt3_onboard"] = "ATI Mach64VT3 (On-Board)",
            ["mach8_vga_isa"] = "ATI Mach8 (ATI Graphics Ultra) (ISA)",
            ["mach32_isa"] = "ATI Mach32 (ISA)",
            ["mach32_vlb"] = "ATI Mach32 (VLB)",
            ["mach32_mca"] = "ATI Mach32 (MCA)",
            ["mach32_pci"] = "ATI Mach32 (PCI)",
            ["mach32_pci_onboard"] = "ATI Mach32 (PCI) On-Board",
            ["bochs_svga"] = "Bochs SVGA",
            ["cga_pravetz"] = "Pravetz VDC-2",
            ["cga_lightpen"] = "CGA lightpen",
            ["plantronics"] = "Plantronics Colorplus",
            ["compaq_cga"] = "Compaq CGA",
            ["compaq_cga_2"] = "Compaq CGA 2",
            ["compaq_plasma"] = "Compaq Plasma",
            ["nga"] = "NCR NGA",
            ["ogc_m24"] = "Olivetti M21/M24/M28 (GO317/318/380/709)",
            ["ogc"] = "Olivetti OGC (GO708)",
            ["quadcolor"] = "Quadram Quadcolor I / I+II",
            ["t1000_video"] = "Toshiba T1000 Video",
            ["t1200_video"] = "Toshiba T1200 Video",
            ["t3100e"] = "Toshiba T3100e",
            ["v6355d"] = "Yamaha V6355D",
            ["chips_69000"] = "Chips & Technologies B69000",
            ["chips_69000_onboard"] = "Chips & Technologies B69000 On-Board",
            ["cl_gd5401_isa"] = "Cirrus Logic GD5401 (ISA)",
            ["cl_gd5402_onboard"] = "Cirrus Logic GD5401 (ISA) (On-Board)",
            ["cl_gd5402_isa"] = "Cirrus Logic GD5402 (ISA)",
            ["cl_gd5402_onboard_commodore"] = "Cirrus Logic GD5402 (ISA) (On-Board) (Commodore)",
            ["cl_gd5420_isa"] = "Cirrus Logic GD5420 (ISA)",
            ["cl_gd5420_onboard"] = "Cirrus Logic GD5420 (ISA) (On-Board)",
            ["cl_gd5422_migrated_isa"] = "Cirrus Logic GD5422 (ISA)",
            ["cl_gd5424_vlb"] = "Cirrus Logic GD5424 (VLB)",
            ["cl_gd5424_onboard"] = "Cirrus Logic GD5424 (VLB) (On-Board)",
            ["cl_gd5426_migrated_isa"] = "Cirrus Logic GD5426 (ISA)",
            ["cl_gd5426_vlb"] = "Cirrus Logic GD5426 (VLB)",
            ["cl_gd5426_onboard"] = "Cirrus Logic GD5426 (ISA) (On-Board)",
            ["cl_gd5428_isa"] = "Cirrus Logic GD5428 (ISA)",
            ["cl_gd5428_migrated_vlb"] = "Cirrus Logic GD5428 (VLB)",
            ["ibm1mbsvga"] = "Cirrus Logic GD5428 (MCA)",
            ["replymcasvga"] = "Cirrus Logic GD5426 (MCA)",
            ["cl_gd5428_onboard"] = "Cirrus Logic GD5428 (ISA) (On-Board)",
            ["cl_gd5428_vlb_onboard"] = "Cirrus Logic GD5428 (VLB) (On-Board)",
            ["cl_gd5428_onboard_vlb"] = "Cirrus Logic GD5428 (VLB) (On-Board) (1MB)",
            ["cl_gd5428_vlb_onboard_pb450"] = "Cirrus Logic GD5428 (VLB) (On-Board) (PB450)",
            ["cl_gd5428_vlb_onboard_tandy"] = "Cirrus Logic GD5428 (VLB) (On-Board) (Tandy)",
            ["cl_gd5429_isa"] = "Cirrus Logic GD5429 (ISA)",
            ["cl_gd5429_vlb"] = "Cirrus Logic GD5429 (VLB)",
            ["cl_gd5430_migrated_vlb"] = "Cirrus Logic GD5430 (VLB)",
            ["cl_gd5430_onboard_vlb"] = "Cirrus Logic GD5430 (VLB) (On-Board)",
            ["cl_gd5430_pci"] = "Cirrus Logic GD5430 (PCI)",
            ["cl_gd5430_onboard_pci"] = "Cirrus Logic GD5430 (PCI) (On-Board)",
            ["cl_gd5434_migrated_isa"] = "Cirrus Logic GD5434 (ISA)",
            ["cl_gd5434_onboard_pci"] = "Cirrus Logic GD5434-4 (PCI) (On-Board)",
            ["cl_gd5434_vlb"] = "Cirrus Logic GD5434 (VLB) (Orchid KELVIN 64)",
            ["cl_gd5434_pci"] = "Cirrus Logic GD5434 (PCI)",
            ["cl_gd5436_onboard_pci"] = "Cirrus Logic GD5436 (PCI) (On-Board)",
            ["cl_gd5436_onboard_pci_ics"] = "Cirrus Logic GD5436 (PCI) (On-Board) (ICS)",
            ["cl_gd5436_pci"] = "Cirrus Logic GD5436 (PCI)",
            ["cl_gd5440_onboard_pci"] = "Cirrus Logic GD5440 (PCI) (On-Board)",
            ["cl_gd5440_pci"] = "Cirrus Logic GD5440 (PCI)",
            ["cl_gd5446_migrated_pci"] = "Cirrus Logic GD5446 (PCI)",
            ["cl_gd5480_pci"] = "Cirrus Logic GD5480 (PCI)",
            ["compaq_ega"] = "Compaq EGA",
            ["superega"] = "Chips & Technologies SuperEGA",
            ["egawonder800p"] = "ATI EGA Wonder 800+",
            ["iskra_ega"] = "Iskra EGA (Cyrillic ROM)",
            ["et2000"] = "Tseng Labs ET2000",
            ["et3000ax"] = "Tseng Labs ET3000AX (ISA)",
            ["et4000ax"] = "Tseng Labs ET4000AX (ISA)",
            ["et4000mca"] = "Tseng Labs ET4000AX (MCA)",
            ["et4000w32"] = "Tseng Labs ET4000/w32 ISA",
            ["et4000w32_vlb"] = "Tseng Labs ET4000/w32 VLB",
            ["et4000w32_onboard"] = "Tseng Labs ET4000/w32 ISA (On-Board)",
            ["et4000w32i"] = "Tseng Labs ET4000/w32i ISA",
            ["et4000w32i_vlb"] = "Tseng Labs ET4000/w32i VLB",
            ["et4000w32p_migrated_vlb"] = "Tseng Labs ET4000/w32p VLB",
            ["et4000w32p_migrated_pci"] = "Tseng Labs ET4000/w32p PCI",
            ["f82c425_video"] = "82C425 CGA LCD/CRT Controller",
            ["genius"] = "Genius VHR",
            ["hd44780"] = "Hitachi HD44780 LCD",
            ["hd44780_cobalt3k"] = "Cobalt 3000/4000 Series LCD Console",
            ["hercules"] = "Hercules",
            ["incolor"] = "Hercules InColor",
            ["hercules_plus"] = "Hercules Plus",
            ["g2_gc205"] = "G2 GC205",
            ["v7_vga_1024i"] = "Video 7 VGA 1024i (HT208)",
            ["ht216_32_pb410a"] = "Headland HT216-32 On-Board (Packard Bell PB410A)",
            ["ht216_32"] = "Headland HT216-32",
            ["radius_isa"] = "Radius SVGA Multiview ISA (HT209)",
            ["radius_mc"] = "Radius SVGA Multiview MCA (HT209)",
            ["ibm5140_lcd"] = "IBM PC Convertible LCD controller",
            ["im1024"] = "ImageManager 1024",
            ["jega"] = "JEGA",
            ["jvga"] = "OKI VGA/H-2 (JVGA/H)",
            ["if386jega"] = "JEGA On-Board (OKI if386AX30L)",
            ["ibm_ps2_m25_mcga"] = "IBM PS/2 Model 25 MCGA",
            ["millennium"] = "Matrox Millennium",
            ["mystique"] = "Matrox Mystique",
            ["mystique_220"] = "Matrox Mystique 220",
            ["millennium_ii"] = "Matrox Millennium II",
            ["millennium_ii_agp"] = "Matrox Millennium II AGP",
            ["productiva_g100"] = "Matrox Productiva G100",
            ["oti037c"] = "Oak OTI-037C",
            ["oti037_pbl300sx"] = "Oak OTI-037 On-Board (Packard Bell PB300/PB320)",
            ["oti067"] = "Oak OTI-067",
            ["oti067_ama932j"] = "Oak OTI-067 On-Board (Arche AMA-932J)",
            ["oti067_m300"] = "Oak OTI-067 On-Board (Olivetti M300-08/15)",
            ["oti077"] = "Oak OTI-077",
            ["oti077_acer100t"] = "Oak OTI-077 On-Board (Acer 100T)",
            ["oti077_pcs44c"] = "Oak OTI-077 On-Board (Olivetti PCS 44/C)",
            ["oti077_pb400"] = "Oak OTI-077 On-Board (Packard Bell PB400)",
            ["pvga1a_pc2086"] = "Paradise PVGA1A On-Board (Amstrad PC2086)",
            ["pvga1a_pc3086"] = "Paradise PVGA1A On-Board (Amstrad PC3086)",
            ["pvga1a_ncr3302"] = "Paradise PVGA1A On-Board (NCR 3302)",
            ["pvga1a"] = "Paradise PVGA1A",
            ["wd90c11_megapc"] = "Paradise WD90C11 On-Board (Amstrad MegaPC)",
            ["wd90c11"] = "Paradise WD90C11-LR",
            ["wd90c30"] = "Paradise WD90C30-LR",
            ["pcjx_video"] = "IBM PC JX (Video)",
            ["pgc"] = "IBM PGC",
            ["ps55da2"] = "IBM Display Adapter II (MCA)",
            ["rtg3105"] = "Realtek RTG3105 (ISA)",
            ["rtg3106"] = "Realtek RTG3106 (ISA)",
            ["px_s3_805_onboard_vlb"] = "S3 86c805 VLB On-Board",
            ["px_trio32_onboard_vlb"] = "S3 Trio32 VLB On-Board",
            ["px_trio32_onboard_pci"] = "S3 Trio32 PCI On-Board",
            ["trio64_onboard_pci"] = "S3 Trio64 PCI On-Board",
            ["trio64vplus_onboard_pci"] = "S3 Trio64V+ PCI On-Board",
            ["trio64v2dx_onboard_pci"] = "S3 Trio64V2/DX On-Board PCI",
            ["s3_86c911_isa"] = "S3 86c911 ISA",
            ["s3_86c924_isa"] = "S3 86c924 ISA",
            ["s3_86c928_isa"] = "S3 86c928 ISA",
            ["s3_86c928_vlb"] = "S3 86c928 VLB",
            ["s3_86c928_pci"] = "S3 86c928 PCI",
            ["s3_86c801_isa"] = "S3 86c801 ISA",
            ["s3_86c805_isa"] = "S3 86c805 ISA",
            ["s3_86c805_vlb"] = "S3 86c805 VLB",
            ["s3_vision864_vlb"] = "S3 Vision864 VLB",
            ["s3_vision864_pci"] = "S3 Vision864 PCI",
            ["s3_trio32_vlb"] = "S3 Trio32 VLB",
            ["s3_trio32_pci"] = "S3 Trio32 PCI",
            ["s3_vision964_vlb"] = "S3 Vision964 VLB",
            ["s3_vision964_pci"] = "S3 Vision964 PCI",
            ["s3_trio64_vlb"] = "S3 Trio64 VLB",
            ["s3_trio64_pci"] = "S3 Trio64 PCI",
            ["s3_vision868_pci"] = "S3 Vision868 PCI",
            ["s3_vision968_vlb"] = "S3 Vision968 VLB",
            ["s3_vision968_pci"] = "S3 Vision968 PCI",
            ["s3_trio64vplus_vlb"] = "S3 Trio64V+ VLB",
            ["s3_trio64vplus_pci"] = "S3 Trio64V+ PCI",
            ["s3_trio64v2dx_pci"] = "S3 Trio64V2/DX PCI",
            ["virge_pci"] = "S3 ViRGE PCI",
            ["virge325_onboard_pci"] = "S3 ViRGE (325) On-Board PCI",
            ["virge_vx_pci"] = "S3 ViRGE/VX PCI",
            ["virge_dx_pci"] = "S3 ViRGE/DX PCI",
            ["virge375_onboard_pci"] = "S3 ViRGE/DX (375) On-Board PCI",
            ["virge385_pci"] = "S3 ViRGE/GX PCI",
            ["virge357_pci"] = "S3 ViRGE/GX2 PCI",
            ["virge_gx2_agp"] = "S3 ViRGE/GX2 AGP",
            ["trio3d2x"] = "S3 Trio3D/2X (362) PCI",
            ["trio3d2x_agp"] = "S3 Trio3D/2X (362) AGP",
            ["sigma400"] = "Sigma Color 400",
            ["tandy1000_video"] = "Tandy 1000 SX (Video)",
            ["tandy1000_hx_video"] = "Tandy 1000 HX (Video)",
            ["tandy1000_sl_video"] = "Tandy 1000SL2 (Video)",
            ["tgui9400cxi_vlb"] = "Trident TGUI 9400CXi",
            ["tgui9440_vlb"] = "Trident TGUI 9440AGi VLB",
            ["tgui9440_pci"] = "Trident TGUI 9440AGi PCI",
            ["tgui9440_onboard_pci"] = "Trident TGUI 9440AGi On-Board PCI",
            ["tgui9660_pci"] = "Trident TGUI 9660XGi PCI",
            ["tgui9660_onboard_pci"] = "Trident TGUI 9660XGi On-Board PCI",
            ["tgui9680_pci"] = "Trident TGUI 9680XGi PCI",
            ["ibm_ps1_2121"] = "IBM PS/1 Model 2121 SVGA",
            ["tvga8900"] = "Trident TVGA 8900",
            ["tvga8900d_onboard"] = "Trident TVGA 8900D (On-Board)",
            ["tvga9000b_migrated"] = "Trident TVGA 9000B",
            ["ps1vga"] = "IBM PS/1 VGA",
            ["ps1vga_mca"] = "IBM PS/1 VGA",
            ["voodoo"] = "3Dfx Voodoo Graphics",
            ["voodoo_banshee_migrated_pci"] = "3Dfx Voodoo Banshee PCI",
            ["voodoo_banshee_agp"] = "3dfx Voodoo Banshee",
            ["voodoo3_1k_pci"] = "3dfx Voodoo3 1000",
            ["voodoo3_1k_agp"] = "3dfx Voodoo3 1000",
            ["voodoo3_2k_pci"] = "3dfx Voodoo3 2000",
            ["voodoo3_2k_agp"] = "3dfx Voodoo3 2000",
            ["voodoo3_2k_agp_onboard_8m"] = "3dfx Voodoo3 2000 (On-Board)",
            ["voodoo3_3k_pci"] = "3dfx Voodoo3 3000",
            ["voodoo3_3k_agp"] = "3dfx Voodoo3 3000",
            ["voodoo3_3500_agp"] = "3dfx Voodoo3 3500 AGP",
            ["velocity100_agp"] = "3dfx Velocity 100",
            ["velocity200_agp"] = "3dfx Velocity 200",
            ["wy700"] = "Wyse 700",
            ["xga_mca"] = "XGA (MCA)",
            ["inmos_xga_isa"] = "INMOS XGA (ISA)",
        };

        // Sound Cards Internal Name -> Display Name Mapping
        public static readonly Dictionary<string, string> SoundCardsMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["none"] = "None",
            ["internal"] = "Internal / Motherboard Audio",
            ["speaker"] = "PC Speaker",
            ["sb1"] = "Creative Sound Blaster 1.0",
            ["sb15"] = "Creative Sound Blaster 1.5",
            ["sb2"] = "Creative Sound Blaster 2.0",
            ["sbpro1"] = "Creative Sound Blaster Pro (CT1330)",
            ["sbpro2"] = "Creative Sound Blaster Pro v2 (CT1600)",
            ["sb16"] = "Creative Sound Blaster 16",
            ["sb16pnp"] = "Creative Sound Blaster 16 PnP",
            ["sbawe32"] = "Creative Sound Blaster AWE32",
            ["sbawe64"] = "Creative Sound Blaster AWE64",
            ["adlib"] = "AdLib Music Synthesizer Card",
            ["gus"] = "Gravis UltraSound (GUS) Classic",
            ["gusmax"] = "Gravis UltraSound MAX",
            ["fluidsynth"] = "FluidSynth",
            ["mt32"] = "Roland MT-32 Emulation",
            ["mt32_new"] = "Roland MT-32 (New) Emulation",
            ["cm32l"] = "Roland CM-32L Emulation",
            ["cm32ln"] = "Roland CM-32LN Emulation",
            ["opl4-ml"] = "OPL4-ML Daughterboard",
            ["soundcanvas"] = "Roland Sound Canvas (CLAP)",
            ["ad1881"] = "Analog Devices AD1881",
            ["ak4540"] = "Asahi Kasei AK4540",
            ["alc100"] = "Avance Logic ALC100",
            ["cs4297"] = "Crystal CS4297",
            ["cs4297a"] = "Crystal CS4297A",
            ["ice1232"] = "ICEnsemble ICE1232 / VIA VT1611A",
            ["stac9708"] = "SigmaTel STAC9708",
            ["stac9721"] = "SigmaTel STAC9721",
            ["tr28023"] = "TriTech TR28023 / Creative CT1297",
            ["w83971d"] = "Winbond W83971D",
            ["wm9701a"] = "Wolfson WM9701A",
            ["si3036"] = "Silicon Laboratories Si3036 Modem",
            ["ac97_via"] = "VIA VT82C686 Integrated AC97 Controller",
            ["ad1816"] = "Analog Devices AD1816",
            ["adlib_mca"] = "AdLib (MCA)",
            ["adlibgold"] = "AdLib Gold",
            ["es1370"] = "Ensoniq AudioPCI (ES1370)",
            ["es1371"] = "Ensoniq AudioPCI (ES1371)",
            ["es1371_onboard"] = "Ensoniq AudioPCI (ES1371) (On-Board)",
            ["es1373"] = "Creative Sound Blaster PCI 128 (ES1373)",
            ["es1373_onboard"] = "Creative Sound Blaster PCI 128 (ES1373) (On-Board)",
            ["ct5880"] = "Creative Sound Blaster PCI 4.1 (CT5880)",
            ["ct5880_onboard"] = "Creative Sound Blaster PCI 4.1 (CT5880) (On-Board)",
            ["azt2316r"] = "Aztech Sound Galaxy Pro 16 II",
            ["azt2316a"] = "Aztech Sound Galaxy Pro 16 AB",
            ["azt1605"] = "Aztech Sound Galaxy Nova 16 Extra",
            ["aztpr16"] = "Aztech Sound Galaxy Pro 16",
            ["azt2320"] = "HP Multimedia Pro 16V-A",
            ["cmi8338"] = "C-Media CMI8338",
            ["cmi8338_onboard"] = "C-Media CMI8338 (On-Board)",
            ["cmi8738"] = "C-Media CMI8738",
            ["cmi8738_onboard"] = "C-Media CMI8738 (On-Board)",
            ["cmi8738_6ch_onboard"] = "C-Media CMI8738-6CH (On-Board)",
            ["cms"] = "Creative Music System / Game Blaster",
            ["voicemasterkey"] = "Covox Voice Master Key",
            ["soundmasterplus"] = "Covox Sound Master Plus",
            ["isadacr0"] = "ISA DAC-r0",
            ["isadacr1"] = "ISA DAC-r1",
            ["soundman"] = "Rainbow Arts PC-Soundman",
            ["cqm_nuked"] = "NUKED CQM",
            ["cs4232"] = "Crystal CS4232",
            ["cs4232_onboard"] = "Crystal CS4232 (On-Board)",
            ["cs4235"] = "Crystal CS4235",
            ["cs4235_onboard"] = "Crystal CS4235 (On-Board)",
            ["cs4236_onboard"] = "Crystal CS4236 (On-Board)",
            ["cs4236b"] = "Crystal CS4236B",
            ["cs4237b"] = "Crystal CS4237B",
            ["cs4238b"] = "Crystal CS4238B",
            ["csm"] = "Covox Sound Master",
            ["gusv34"] = "Gravis UltraSound (rev 3.4)",
            ["gusv37"] = "Gravis UltraSound (rev 3.7)",
            ["gusace"] = "Gravis UltraSound ACE",
            ["gusextreme"] = "Gravis UltraSound Extreme",
            ["gusvipermax"] = "Synergy ViperMAX",
            ["guspnp"] = "Gravis UltraSound PnP (Old)",
            ["guspnp_new"] = "Gravis UltraSound PnP (New)",
            ["guspnp_nocd"] = "Gravis UltraSound PnP (No CD)",
            ["guspnp_compaq"] = "Compaq UltraSound 32",
            ["jazz16"] = "Media Vision Pro Sonic 16",
            ["lpt_adlipt"] = "AdLib-on-LPT (adlipt/OPL2LPT)",
            ["lpt_opl3"] = "AdLib-on-LPT (OPL3LPT)",
            ["lpt_cms"] = "Creative Music System-on-LPT (CMSLPT)",
            ["lpt_dac"] = "LPT DAC / Covox Speech Thing",
            ["lpt_dac_stereo"] = "Stereo LPT DAC",
            ["lpt_dac_ftl"] = "FTL Sound Adapter",
            ["lpt_dac_soundjr"] = "SiliconSoft SoundJr",
            ["dss"] = "Disney Sound Source",
            ["lpt_tndlpt"] = "Tandy-on-LPT (TNDLPT)",
            ["mmb"] = "Mindscape Music Board",
            ["mpu401"] = "Roland MPU-IPC-T",
            ["mpu401_mca"] = "Roland MPU-IMC",
            ["opl2board_device"] = "OPL2Board (External Device)",
            ["ym3812_nuked_opl2"] = "Yamaha YM3812 OPL2 (NUKED OPL2)",
            ["ym3812_nuked_opl3"] = "Yamaha YM3812 OPL2 (NUKED OPL3)",
            ["ymf262_nuked"] = "Yamaha YMF262 OPL3 (NUKED OPL3)",
            ["ymf289b_nuked"] = "Yamaha YMF262 OPL3 (NUKED OPL3)",
            ["esfm_esfmu"] = "ESS Technology ESFM (ESFMu)",
            ["acermagic_s20"] = "AcerMagic S20",
            ["mirosound_pcm10"] = "miroSOUND PCM10",
            ["opti_82c930"] = "OPTi 82C930",
            ["opti_82c931"] = "OPTi 82C931",
            ["pas"] = "Media Vision Pro Audio Spectrum",
            ["pasplus"] = "Media Vision Pro Audio Spectrum Plus",
            ["pas16"] = "Media Vision Pro Audio Spectrum 16",
            ["pas16d"] = "Media Vision Pro Audio Spectrum 16D",
            ["ps1snd"] = "IBM PS/1 Audio Card",
            ["ps1midi"] = "IBM PS/1 Audio Card (MIDI)",
            ["pssj"] = "Tandy PSSJ",
            ["pssj_1e0"] = "Tandy PSSJ (port 1e0h)",
            ["pssj_isa"] = "Tandy PSSJ Clone",
            ["saaym"] = "TexElec SAAYM",
            ["thunderboard"] = "Media Vision Thunder Board",
            ["sb"] = "Sound Blaster v1.0",
            ["sb1.5"] = "Sound Blaster v1.5",
            ["sbmcv"] = "Sound Blaster MCV",
            ["sb2.0"] = "Sound Blaster v2.0",
            ["sbprov1"] = "Sound Blaster Pro v1",
            ["sbprov2"] = "Sound Blaster Pro v2",
            ["sbpromcv"] = "Sound Blaster Pro MCV",
            ["sbpro_compat"] = "Sound Blaster Pro (Compatibility)",
            ["sb_vibra16c_onboard"] = "Sound Blaster ViBRA 16C (On-Board)",
            ["sb_vibra16c"] = "Sound Blaster ViBRA 16C",
            ["sb_vibra16cl_onboard"] = "Sound Blaster ViBRA 16CL (On-Board)",
            ["sb_vibra16cl"] = "Sound Blaster ViBRA 16CL",
            ["sb_vibra16s_onboard"] = "Sound Blaster ViBRA 16S (On-Board)",
            ["sb_vibra16s"] = "Sound Blaster ViBRA 16S",
            ["sb_vibra16xv_onboard"] = "Sound Blaster ViBRA 16XV (On-Board)",
            ["sb_vibra16xv"] = "Sound Blaster ViBRA 16XV",
            ["sb16_reply_mca"] = "Sound Blaster 16 Reply MCA",
            ["sb16_pnp"] = "Sound Blaster 16 PnP",
            ["sb16_pnp_ide"] = "Sound Blaster 16 PnP (IDE)",
            ["sb16_compat"] = "Sound Blaster 16 (Compatibility)",
            ["sb_goldfinch"] = "Creative EMU8000 PnP (Goldfinch)",
            ["sb32_pnp"] = "Sound Blaster 32 PnP",
            ["sbawe32_pnp"] = "Sound Blaster AWE32 PnP",
            ["sbawe32_ide_pnp"] = "Sound Blaster AWE32 IDE PnP",
            ["sbawe64_value"] = "Sound Blaster AWE64 Value",
            ["sbawe64_ide"] = "Sound Blaster AWE64 (IDE)",
            ["sbawe64_gold"] = "Sound Blaster AWE64 Gold",
            ["ess_es488"] = "ESS AudioDrive ES488",
            ["ess_es1488"] = "ESS AudioDrive ES1488",
            ["ess_es688"] = "ESS AudioDrive ES688",
            ["ess_ess0100_pnp"] = "ESS AudioDrive ES688 (ESS0100) PnP",
            ["ess_es1688"] = "ESS AudioDrive ES1688",
            ["ess_es1688_compaq"] = "ESS AudioDrive ES1688 (Compaq)",
            ["ess_ess0102_pnp"] = "ESS AudioDrive ES1688 (ESS0102) PnP",
            ["ess_ess0968_pnp_es688"] = "ESS AudioDrive ES688 (ESS0968) PnP",
            ["ess_ess0968_pnp"] = "ESS AudioDrive ES1688 (ESS0968) PnP",
            ["soundpiper_16_mca"] = "SoundPiper 16 (ESS AudioDrive ES688) MCA",
            ["soundpiper_32_mca"] = "SoundPiper 32 (ESS AudioDrive ES1688) MCA",
            ["chipchat_16_mca"] = "ChipChat 16 (ESS AudioDrive ES1688) MCA",
            ["ess_es1788"] = "ESS AudioDrive ES1788/ES1698",
            ["ess_es1888"] = "ESS AudioDrive ES1888",
            ["ess_es1888_compaq"] = "ESS AudioDrive ES1888 (Compaq)",
            ["ess_es1887"] = "ESS AudioDrive ES1887",
            ["ess_es1868"] = "ESS AudioDrive ES1868",
            ["ess_es1869"] = "ESS AudioDrive ES1869",
            ["sensationaud"] = "Tandy Sensation/VIS audio",
            ["sn76489"] = "TI SN76489 PSG",
            ["ncr8496"] = "NCR8496 PSG",
            ["tndy"] = "TNDY",
            ["ess_solo1"] = "ESS Solo-1 ES1938S",
            ["ess_solo1_onboard"] = "ESS Solo-1 ES1938S (On-Board)",
            ["sound_source"] = "Sound source",
            ["ssi2001"] = "Innovation SSI-2001",
            ["Entertainer"] = "The Entertainer",
            ["wss"] = "Windows Sound System",
            ["ncraudio"] = "NCR Business Audio",
            ["ymf701"] = "Yamaha YMF-701 (OPL3-SA)",
            ["ymf715_onboard"] = "Yamaha YMF715 (OPL3-SA3) (On-Board)",
            ["ymf718"] = "Yamaha YMF718 (OPL3-SA2)",
            ["ymf719"] = "Yamaha YMF719 (OPL3-SA3)",
        };



        public static (string Platform, MachineInfo? Info) FindMachine(string? internalOrDisplayName)
        {
            if (string.IsNullOrWhiteSpace(internalOrDisplayName))
                return (string.Empty, null);

            foreach (var (platform, list) in MachinesByType)
            {
                var match = list.FirstOrDefault(m =>
                    m.InternalName.Equals(internalOrDisplayName, StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Equals(internalOrDisplayName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return (platform, match);
            }

            return (string.Empty, null);
        }

        public static string ResolveMachinePlatform(string? internalOrDisplayName)
        {
            var (platform, _) = FindMachine(internalOrDisplayName);
            return !string.IsNullOrEmpty(platform) ? platform : "IBM PC Compatible";
        }

        public static string FormatSocketPackage(string? cpuPackage)
        {
            if (string.IsNullOrWhiteSpace(cpuPackage))
                return "Socket / Slot";

            var pkg = cpuPackage.Split('|')[0].Trim();
            return pkg switch
            {
                "CPU_PKG_8088" or "CPU_PKG_80C88" => "8088",
                "CPU_PKG_8086" => "8086",
                "CPU_PKG_286" => "80286",
                "CPU_PKG_386SX" => "386SX",
                "CPU_PKG_386DX" => "386DX",
                "CPU_PKG_486" or "CPU_PKG_SOCKET1" or "CPU_PKG_SOCKET2" or "CPU_PKG_SOCKET3" => "Socket 1/2/3",
                "CPU_PKG_SOCKET4" => "Socket 4",
                "CPU_PKG_SOCKET5" or "CPU_PKG_SOCKET5_7" => "Socket 5 / 7",
                "CPU_PKG_SOCKET7" => "Socket 7",
                "CPU_PKG_SOCKET8" => "Socket 8",
                "CPU_PKG_SLOT1" => "Slot 1",
                "CPU_PKG_SLOT2" => "Slot 2",
                "CPU_PKG_SOCKET370" => "Socket 370",
                _ => pkg.Replace("CPU_PKG_", "").Replace("_", " ")
            };
        }

        public static string ResolveMachineName(string? internalOrDisplayName)
        {
            if (string.IsNullOrWhiteSpace(internalOrDisplayName))
                return "Unknown Machine";

            foreach (var list in MachinesByType.Values)
            {
                var match = list.FirstOrDefault(m => 
                    m.InternalName.Equals(internalOrDisplayName, StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Equals(internalOrDisplayName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match.Name;
            }

            return internalOrDisplayName;
        }

        public static string ResolveCpuName(string? internalOrDisplayName, int cpuSpeedHz = 0, string? fpu = null)
        {
            if (string.IsNullOrWhiteSpace(internalOrDisplayName))
                return "Unknown CPU";

            string baseName = "";
            var match = CpuList.FirstOrDefault(c => 
                c.InternalName.Equals(internalOrDisplayName, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals(internalOrDisplayName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                baseName = match.Name;
            }
            else
            {
                // Fallback translations for known 86Box internal CPU tags
                baseName = internalOrDisplayName.ToLowerInvariant() switch
                {
                    "pentium_p55c" => "Intel Pentium MMX",
                    "pentium_tillamook" => "Intel Mobile Pentium MMX",
                    "pentium_p54c" => "Intel Pentium (P54C)",
                    "pentium_p5" => "Intel Pentium (P5)",
                    "pentiumpro" => "Intel Pentium Pro",
                    "pentium2_klamath" => "Intel Pentium II (Klamath)",
                    "pentium2_deschutes" => "Intel Pentium II (Deschutes)",
                    "pentium3_katmai" => "Intel Pentium III (Katmai)",
                    "pentium3_coppermine" => "Intel Pentium III (Coppermine)",
                    "celeron_covington" => "Intel Celeron (Covington)",
                    "celeron_mendocino" => "Intel Celeron (Mendocino)",
                    "k6_2" => "AMD K6-2",
                    "k6_3" => "AMD K6-III",
                    "am5x86" => "AMD Am5x86",
                    "am486dx4" => "AMD Am486DX4",
                    "cx6x86" => "Cyrix Cx6x86",
                    "cx6x86mx" => "Cyrix 6x86MX / MII",
                    "winchip" => "IDT WinChip",
                    "winchip2" => "IDT WinChip 2",
                    _ => internalOrDisplayName
                };
            }

            if (cpuSpeedHz > 0)
            {
                int speedMhz = (int)Math.Round(cpuSpeedHz / 1_000_000.0);
                if (speedMhz > 0)
                {
                    baseName += $" / {speedMhz} MHz";
                }
            }

            if (!string.IsNullOrWhiteSpace(fpu) && 
                !fpu.Equals("none", StringComparison.OrdinalIgnoreCase) && 
                !fpu.Equals("internal", StringComparison.OrdinalIgnoreCase))
            {
                baseName += $", {fpu.ToUpperInvariant()} FPU";
            }

            return baseName;
        }

        public static int ResolveMemoryMb(int memSizeKb)
        {
            if (memSizeKb >= 1024)
                return memSizeKb / 1024;
            return Math.Max(1, memSizeKb);
        }

        public static string FormatMemoryDisplay(int memSizeKb)
        {
            if (memSizeKb >= 1048576)
                return $"{memSizeKb / 1048576.0:0.#} GB";
            if (memSizeKb >= 1024)
                return $"{memSizeKb / 1024} MB";
            return $"{memSizeKb} KB";
        }

        public static string ResolveVideoName(string? internalOrDisplayName)
        {
            if (string.IsNullOrWhiteSpace(internalOrDisplayName))
                return "None";

            if (VideoCardsMap.TryGetValue(internalOrDisplayName, out var displayName))
                return displayName;

            var match = VideoCards.FirstOrDefault(v => v.Equals(internalOrDisplayName, StringComparison.OrdinalIgnoreCase));
            return match ?? internalOrDisplayName;
        }

        public static string ResolveSoundName(string? internalOrDisplayName)
        {
            if (string.IsNullOrWhiteSpace(internalOrDisplayName))
                return "None";

            if (SoundCardsMap.TryGetValue(internalOrDisplayName, out var displayName))
                return displayName;

            var match = SoundCards.FirstOrDefault(s => s.Equals(internalOrDisplayName, StringComparison.OrdinalIgnoreCase));
            return match ?? internalOrDisplayName;
        }
    }
}
