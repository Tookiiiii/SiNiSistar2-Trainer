using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("SiNiSistar 2 Sanctum Trainer")]
[assembly: AssemblyDescription("Universal IL2CPP Auto-Adapting Trainer for SiNiSistar 2")]
[assembly: AssemblyProduct("SiNiSistar 2 Sanctum Trainer")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace SiNiSistar2Trainer
{
    internal static class Native
    {
        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_VM_WRITE = 0x0020;
        public const uint PROCESS_VM_OPERATION = 0x0008;
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;

        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_NONE = 0x0000;
        public const int ERROR_ACCESS_DENIED = 5;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }

    internal sealed class DynamicOffsets
    {
        // Il2CppClass / FieldInfo internal layout (auto-probed from PE exports, with Unity 2022.3 defaults)
        public ulong ClassName = 0x10;
        public ulong ClassNamespace = 0x18;
        public ulong ClassParent = 0x58;
        public ulong ClassFields = 0x80;
        public ulong ClassStaticFields = 0xB8;
        public ulong ClassFieldCount = 0x124;
        public int FieldInfoSize = 0x20;
        public int FieldInfoOffsetField = 0x18;

        // ManagerList
        public ulong ManagerListInstance = 0x40;
        public ulong MgrDebugger = 0x30;
        public ulong MgrPlayerStatus = 0x88;

        // Debugger
        public ulong DbgCriticalPoise = 0x28;
        public ulong DbgCriticalDamage = 0x29;
        public ulong DbgIsOverwriteDamage = 0x2A;
        public ulong DbgOverwriteDamageValue = 0x2C;
        public ulong DbgIsDisableHold = 0x30;
        public ulong DbgIsPlayerSuperTough = 0x31;
        public ulong DbgIsNotIncrementalDefilement = 0x41;

        // PlayerStatusManager
        public ulong PsMaxHPField = 0x28;
        public ulong PsMaxMPField = 0x30;
        public ulong PsAbnormalList = 0x50;
        public ulong PsDurability = 0x58;
        public ulong PsHP = 0x60;
        public ulong PsMP = 0x68;
        public ulong PsAttackLvProp = 0x148;
        public ulong PsAttackLvRoProp = 0x150;
        public ulong PsHpLvProp = 0x158;
        public ulong PsHpLvRoProp = 0x160;
        public ulong PsMpLvProp = 0x168;
        public ulong PsMpLvRoProp = 0x170;
        public ulong PsRelicsProp = 0x178;
        public ulong PsRelicsRoProp = 0x180;
        public ulong PsEnhanceParam = 0x188;
        public ulong PsNextAdditiveRelics = 0x1A8;

        // BattleMainParameter
        public ulong BmpCurrentRp = 0x10;
        public ulong BmpMaxRp = 0x18;
        public ulong BmpCurrentRoRp = 0x20;
        public ulong BmpMaxRoRp = 0x28;
        public ulong BmpDontSub = 0x34;

        // UniRx ReactiveProperty / ReadOnlyReactiveProperty
        public ulong RpValue = 0x10;
        public ulong RoRpLatestValue = 0x14;

        // EnhanceParameter & EnhanceAttack
        public ulong EpMaxHPArray = 0x30;
        public ulong EpMaxMPArray = 0x38;
        public ulong EpEnhanceAttackArray = 0x40;
        public ulong EaPowerArray = 0x18;

        // AbnormalList & AbnormalData
        public ulong AbAddedArray = 0x48;
        public ulong AbDataType = 0x18;
        public ulong AbDataToLevel0 = 0x31;
        public ulong AbDataDeleteTimer = 0x34;
        public ulong AbDataLevelRp = 0x48;
        public ulong AbDataLevel = 0x58;

        // Constants (x64 IL2CPP array header & stable enum hash)
        public const ulong ArrayLength = 0x18;
        public const ulong ArrayData = 0x20;
        public const int AbnormalTypeDefilement = 960050227; // 0x39393433

        // Known ManagerList_TypeInfo RVA fast-path hints (validated before use; falls back to full .data scan)
        public static readonly ulong[] FastPathHints = new ulong[] { 0x2F4D868 };
    }

    internal sealed class GameState
    {
        public bool Connected;
        public bool AccessDenied;
        public bool Scanning;
        public bool Resolved;
        public bool InGame;
        public int Pid;
        public string GameVersion = "";
        public string ResolveSummary = "";

        public int HpCurrent;
        public int HpMax;
        public bool HpDontSub;

        public int MpCurrent;
        public int MpMax;
        public bool MpDontSub;

        public bool DurabilityDontSub;

        public int Relics;
        public int AttackLv;
        public int HpLv;
        public int MpLv;

        public int DefilementLevel;
        public bool DefilementLocked;

        public bool CriticalDamage;
        public bool CriticalPoise;
        public bool IsOverwriteDamage;
        public int OverwriteDamageValue;
        public bool IsDisableHold;
        public bool IsPlayerSuperTough;

        public int SampleBaseDamage;
        public int SampleCurrentDamage;
        public int AttackEntryCount;
    }

    internal sealed class MemoryBridge : IDisposable
    {
        private readonly object _sync = new object();
        private IntPtr _handle = IntPtr.Zero;
        private int _pid = 0;
        private ulong _gaBase = 0;
        private uint _gaImageSize = 0;
        private string _exePath = "";
        private string _gameVersion = "";
        private bool _accessDenied = false;

        // Auto-resolved runtime state
        private DynamicOffsets _off = new DynamicOffsets();
        private ulong _managerListClassPtr = 0;
        private ulong _managerListSlotRva = 0;
        private string _resolveMode = "";
        private int _resolvedFieldCount = 0;
        private bool _fieldsResolvedCore = false;
        private bool _fieldsResolvedSub = false;
        private bool _fieldsResolvedAbnormalData = false;
        private volatile bool _isScanning = false;
        private DateTime _lastScanFailTime = DateTime.MinValue;
        private readonly List<string> _diagLines = new List<string>();

        // Cached original EnhanceParameter tables for lossless scaling & restore
        private ulong _cachedEnhanceParamPtr = 0;
        private int[] _origMaxHpArray = null;
        private int[] _origMaxMpArray = null;
        private readonly Dictionary<ulong, int[]> _origPowerArrays = new Dictionary<ulong, int[]>();
        private ulong _samplePowerArrayPtr = 0;
        private ulong _lastPurifiedDefPtr = 0;

        public bool IsAttached { get { return _handle != IntPtr.Zero; } }
        public string ExePath { get { return _exePath; } }

        public void Dispose()
        {
            Detach();
        }

        public void Detach()
        {
            lock (_sync)
            {
                if (_handle != IntPtr.Zero)
                {
                    Native.CloseHandle(_handle);
                    _handle = IntPtr.Zero;
                }
                _pid = 0;
                _gaBase = 0;
                _gaImageSize = 0;
                _exePath = "";
                _gameVersion = "";
                _accessDenied = false;
                _off = new DynamicOffsets();
                _managerListClassPtr = 0;
                _managerListSlotRva = 0;
                _resolveMode = "";
                _resolvedFieldCount = 0;
                _fieldsResolvedCore = false;
                _fieldsResolvedSub = false;
                _fieldsResolvedAbnormalData = false;
                _isScanning = false;
                _diagLines.Clear();
                ClearEnhanceCache();
            }
        }

        private void ClearEnhanceCache()
        {
            _cachedEnhanceParamPtr = 0;
            _origMaxHpArray = null;
            _origMaxMpArray = null;
            _origPowerArrays.Clear();
            _samplePowerArrayPtr = 0;
            _lastPurifiedDefPtr = 0;
        }

        public string GetDiagnosticsText()
        {
            lock (_sync)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== SiNiSistar 2 Sanctum Trainer · 运行时反射诊断 ===");
                sb.AppendLine("进程状态: " + (IsAttached ? ("已连接 PID=" + _pid) : (_accessDenied ? "权限不足(需管理员权限)" : "未连接")));
                if (!string.IsNullOrEmpty(_exePath)) sb.AppendLine("游戏路径: " + _exePath);
                if (!string.IsNullOrEmpty(_gameVersion)) sb.AppendLine("游戏版本: " + _gameVersion);
                sb.AppendLine(string.Format("GameAssembly.dll 基址: 0x{0:X} (ImageSize: 0x{1:X})", _gaBase, _gaImageSize));
                sb.AppendLine(string.Format("ManagerList Il2CppClass*: 0x{0:X} (Slot RVA: 0x{1:X}, 方式: {2})",
                    _managerListClassPtr, _managerListSlotRva, string.IsNullOrEmpty(_resolveMode) ? "未定位" : _resolveMode));
                sb.AppendLine(string.Format("Il2CppClass 布局: name=+0x{0:X}, ns=+0x{1:X}, parent=+0x{2:X}, fields=+0x{3:X}, static=+0x{4:X}, count=+0x{5:X}, fOff=+0x{6:X}",
                    _off.ClassName, _off.ClassNamespace, _off.ClassParent, _off.ClassFields, _off.ClassStaticFields, _off.ClassFieldCount, _off.FieldInfoOffsetField));
                sb.AppendLine();
                sb.AppendLine(string.Format("动态解析字段总数: {0} 项", _resolvedFieldCount));
                sb.AppendLine(string.Format("  ManagerList       : Instance=+0x{0:X}, Debugger=+0x{1:X}, PlayerStatus=+0x{2:X}",
                    _off.ManagerListInstance, _off.MgrDebugger, _off.MgrPlayerStatus));
                sb.AppendLine(string.Format("  Debugger          : CritPoise=+0x{0:X}, CritDmg=+0x{1:X}, Overwrite=+0x{2:X}/+0x{3:X}, NoHold=+0x{4:X}, Tough=+0x{5:X}, NoDefile=+0x{6:X}",
                    _off.DbgCriticalPoise, _off.DbgCriticalDamage, _off.DbgIsOverwriteDamage, _off.DbgOverwriteDamageValue,
                    _off.DbgIsDisableHold, _off.DbgIsPlayerSuperTough, _off.DbgIsNotIncrementalDefilement));
                sb.AppendLine(string.Format("  PlayerStatusMgr   : HP=+0x{0:X}, MP=+0x{1:X}, Dur=+0x{2:X}, AbList=+0x{3:X}, Relics=+0x{4:X}/+0x{5:X}, Enhance=+0x{6:X}, NextRelics=+0x{7:X}",
                    _off.PsHP, _off.PsMP, _off.PsDurability, _off.PsAbnormalList, _off.PsRelicsProp, _off.PsRelicsRoProp, _off.PsEnhanceParam, _off.PsNextAdditiveRelics));
                sb.AppendLine(string.Format("  Stat Levels       : AtkLv=+0x{0:X}, HpLv=+0x{1:X}, MpLv=+0x{2:X}",
                    _off.PsAttackLvProp, _off.PsHpLvProp, _off.PsMpLvProp));
                sb.AppendLine(string.Format("  BattleMainParam   : CurRp=+0x{0:X}, MaxRp=+0x{1:X}, CurRo=+0x{2:X}, MaxRo=+0x{3:X}, DontSub=+0x{4:X}",
                    _off.BmpCurrentRp, _off.BmpMaxRp, _off.BmpCurrentRoRp, _off.BmpMaxRoRp, _off.BmpDontSub));
                sb.AppendLine(string.Format("  UniRx             : Reactive.value=+0x{0:X}, ReadOnly.latestValue=+0x{1:X}",
                    _off.RpValue, _off.RoRpLatestValue));
                sb.AppendLine(string.Format("  EnhanceParameter  : MaxHP[]=+0x{0:X}, MaxMP[]=+0x{1:X}, Attack[]=+0x{2:X}, Power[]=+0x{3:X}",
                    _off.EpMaxHPArray, _off.EpMaxMPArray, _off.EpEnhanceAttackArray, _off.EaPowerArray));
                sb.AppendLine(string.Format("  Abnormal          : Added[]=+0x{0:X}, Type=+0x{1:X}, ToLv0=+0x{2:X}, Timer=+0x{3:X}, LvRp=+0x{4:X}",
                    _off.AbAddedArray, _off.AbDataType, _off.AbDataToLevel0, _off.AbDataDeleteTimer, _off.AbDataLevelRp));
                if (_diagLines.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- 扫描日志 ---");
                    for (int i = 0; i < _diagLines.Count; i++)
                        sb.AppendLine(_diagLines[i]);
                }
                return sb.ToString();
            }
        }

        private void AddDiag(string line)
        {
            lock (_sync)
            {
                if (_diagLines.Count > 80) _diagLines.RemoveAt(0);
                _diagLines.Add(DateTime.Now.ToString("HH:mm:ss.fff ") + line);
            }
        }

        public bool EnsureAttached()
        {
            if (_handle != IntPtr.Zero)
            {
                try
                {
                    Process p = Process.GetProcessById(_pid);
                    if (!p.HasExited) return true;
                }
                catch { }
                Detach();
            }

            Process[] procs = Process.GetProcessesByName("SiNiSistar2");
            if (procs.Length == 0)
            {
                _accessDenied = false;
                return false;
            }

            Process target = procs[0];
            try
            {
                IntPtr h = Native.OpenProcess(
                    Native.PROCESS_VM_READ | Native.PROCESS_VM_WRITE | Native.PROCESS_VM_OPERATION | Native.PROCESS_QUERY_INFORMATION,
                    false, target.Id);
                if (h == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    _accessDenied = (err == Native.ERROR_ACCESS_DENIED);
                    return false;
                }

                ulong ga = 0;
                uint gaSize = 0;
                try
                {
                    foreach (ProcessModule m in target.Modules)
                    {
                        if (string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            ga = (ulong)m.BaseAddress.ToInt64();
                            gaSize = (uint)m.ModuleMemorySize;
                            break;
                        }
                    }
                }
                catch
                {
                    Native.CloseHandle(h);
                    _accessDenied = true;
                    return false;
                }

                if (ga == 0)
                {
                    Native.CloseHandle(h);
                    return false;
                }

                string exePath = "";
                try { exePath = target.MainModule.FileName; } catch { }

                lock (_sync)
                {
                    _handle = h;
                    _pid = target.Id;
                    _gaBase = ga;
                    _gaImageSize = gaSize;
                    _exePath = exePath;
                    _accessDenied = false;
                    _gameVersion = DetectGameVersion(exePath);
                    _off = new DynamicOffsets();
                    _managerListClassPtr = 0;
                    _managerListSlotRva = 0;
                    _fieldsResolvedCore = false;
                    _fieldsResolvedSub = false;
                    _fieldsResolvedAbnormalData = false;
                    _resolvedFieldCount = 0;
                    _diagLines.Clear();
                    ClearEnhanceCache();
                }

                AddDiag("已挂载进程 PID=" + target.Id + ", 版本=" + (_gameVersion ?? "未知"));
                ProbeIl2CppStructLayoutFromExports();
                TriggerResolveIfNeeded();
                return true;
            }
            catch
            {
                Detach();
                return false;
            }
        }

        private static string DetectGameVersion(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return "";
            try
            {
                string dir = Path.GetDirectoryName(exePath);
                if (string.IsNullOrEmpty(dir)) return "";
                string buildInfo = Path.Combine(dir, "build_info.txt");
                if (File.Exists(buildInfo))
                {
                    string txt = File.ReadAllText(buildInfo).Trim();
                    if (!string.IsNullOrEmpty(txt)) return txt;
                }
                string readme = Path.Combine(dir, "readme.txt");
                if (File.Exists(readme))
                {
                    string[] lines = File.ReadAllLines(readme, Encoding.UTF8);
                    for (int i = 0; i < Math.Min(lines.Length, 10); i++)
                    {
                        Match m = Regex.Match(lines[i], @"v?\d+\.\d+\.\d+");
                        if (m.Success) return m.Value.StartsWith("v") ? m.Value : ("v" + m.Value);
                    }
                }
            }
            catch { }
            return "未知版本";
        }

        private bool ReadBytes(ulong addr, byte[] buf)
        {
            IntPtr h = _handle;
            if (h == IntPtr.Zero || addr < 0x10000) return false;
            IntPtr read;
            return Native.ReadProcessMemory(h, addr, buf, buf.Length, out read) && read.ToInt64() == buf.Length;
        }

        private bool WriteBytes(ulong addr, byte[] buf)
        {
            IntPtr h = _handle;
            if (h == IntPtr.Zero || addr < 0x10000) return false;
            IntPtr written;
            return Native.WriteProcessMemory(h, addr, buf, buf.Length, out written) && written.ToInt64() == buf.Length;
        }

        private bool ReadUInt64(ulong addr, out ulong val)
        {
            val = 0;
            byte[] buf = new byte[8];
            if (!ReadBytes(addr, buf)) return false;
            val = BitConverter.ToUInt64(buf, 0);
            return true;
        }

        private bool ReadUInt32(ulong addr, out uint val)
        {
            val = 0;
            byte[] buf = new byte[4];
            if (!ReadBytes(addr, buf)) return false;
            val = BitConverter.ToUInt32(buf, 0);
            return true;
        }

        private bool ReadInt32(ulong addr, out int val)
        {
            val = 0;
            byte[] buf = new byte[4];
            if (!ReadBytes(addr, buf)) return false;
            val = BitConverter.ToInt32(buf, 0);
            return true;
        }

        private bool ReadUInt16(ulong addr, out ushort val)
        {
            val = 0;
            byte[] buf = new byte[2];
            if (!ReadBytes(addr, buf)) return false;
            val = BitConverter.ToUInt16(buf, 0);
            return true;
        }

        private bool ReadBool(ulong addr, out bool val)
        {
            val = false;
            byte[] buf = new byte[1];
            if (!ReadBytes(addr, buf)) return false;
            val = buf[0] != 0;
            return true;
        }

        private string ReadCString(ulong addr, int maxLen)
        {
            if (addr < 0x10000 || addr > 0x7FFFFFFFFFFF) return null;
            byte[] buf = new byte[maxLen];
            IntPtr h = _handle;
            if (h == IntPtr.Zero) return null;
            IntPtr read;
            if (!Native.ReadProcessMemory(h, addr, buf, buf.Length, out read) || read.ToInt64() < 2)
                return null;
            int n = (int)read.ToInt64();
            int len = 0;
            while (len < n && buf[len] != 0)
            {
                if (buf[len] < 0x20 || buf[len] > 0x7E) return null;
                len++;
            }
            if (len == 0) return "";
            return Encoding.ASCII.GetString(buf, 0, len);
        }

        private bool WriteInt32(ulong addr, int val) { return WriteBytes(addr, BitConverter.GetBytes(val)); }
        private bool WriteFloat(ulong addr, float val) { return WriteBytes(addr, BitConverter.GetBytes(val)); }
        private bool WriteBool(ulong addr, bool val) { return WriteBytes(addr, new byte[] { val ? (byte)1 : (byte)0 }); }

        // =========================================================================
        //  PHASE 1: Auto-probe Il2CppClass & FieldInfo struct offsets from PE Exports
        // =========================================================================
        private void ProbeIl2CppStructLayoutFromExports()
        {
            try
            {
                byte[] hdr = new byte[0x1000];
                if (!ReadBytes(_gaBase, hdr)) return;
                if (hdr[0] != 'M' || hdr[1] != 'Z') return;
                int peOff = BitConverter.ToInt32(hdr, 0x3C);
                if (peOff <= 0 || peOff + 0x100 >= hdr.Length) return;
                if (BitConverter.ToUInt32(hdr, peOff) != 0x00004550) return; // "PE\0\0"

                uint optMagic = BitConverter.ToUInt16(hdr, peOff + 0x18);
                if (optMagic != 0x20B) return; // PE32+ (64-bit)

                uint expRva = BitConverter.ToUInt32(hdr, peOff + 0x88);
                if (expRva == 0) return;

                byte[] expDir = new byte[40];
                if (!ReadBytes(_gaBase + expRva, expDir)) return;
                uint numNames = BitConverter.ToUInt32(expDir, 24);
                uint addrTableRva = BitConverter.ToUInt32(expDir, 28);
                uint nameTableRva = BitConverter.ToUInt32(expDir, 32);
                uint ordTableRva = BitConverter.ToUInt32(expDir, 36);
                if (numNames == 0 || numNames > 50000) return;

                byte[] nameRvas = new byte[numNames * 4];
                byte[] ords = new byte[numNames * 2];
                if (!ReadBytes(_gaBase + nameTableRva, nameRvas)) return;
                if (!ReadBytes(_gaBase + ordTableRva, ords)) return;

                string[] wanted = new string[]
                {
                    "il2cpp_class_get_name",
                    "il2cpp_class_get_namespace",
                    "il2cpp_class_get_parent",
                    "il2cpp_class_get_static_field_data",
                    "il2cpp_class_num_fields",
                    "il2cpp_class_get_fields",
                    "il2cpp_field_get_offset"
                };

                Dictionary<string, ulong> exportAddrs = new Dictionary<string, ulong>();
                for (int i = 0; i < wanted.Length; i++)
                {
                    // Binary search PE export name table (exports are lexicographically sorted!)
                    int lo = 0, hi = (int)numNames - 1;
                    while (lo <= hi)
                    {
                        int mid = (lo + hi) >> 1;
                        uint nrva = BitConverter.ToUInt32(nameRvas, mid * 4);
                        string ename = ReadCString(_gaBase + nrva, 64) ?? "";
                        int cmp = string.CompareOrdinal(ename, wanted[i]);
                        if (cmp == 0)
                        {
                            ushort ord = BitConverter.ToUInt16(ords, mid * 2);
                            uint funcRva;
                            if (ReadUInt32(_gaBase + addrTableRva + (ulong)(ord * 4), out funcRva))
                                exportAddrs[wanted[i]] = _gaBase + funcRva;
                            break;
                        }
                        if (cmp < 0) lo = mid + 1;
                        else hi = mid - 1;
                    }
                }

                ulong off;
                if (exportAddrs.ContainsKey("il2cpp_class_get_name") && TryParseMovRaxFromRcx(exportAddrs["il2cpp_class_get_name"], out off))
                    _off.ClassName = off;
                if (exportAddrs.ContainsKey("il2cpp_class_get_namespace") && TryParseMovRaxFromRcx(exportAddrs["il2cpp_class_get_namespace"], out off))
                    _off.ClassNamespace = off;
                if (exportAddrs.ContainsKey("il2cpp_class_get_parent") && TryParseMovRaxFromRcx(exportAddrs["il2cpp_class_get_parent"], out off))
                    _off.ClassParent = off;
                if (exportAddrs.ContainsKey("il2cpp_class_get_static_field_data") && TryParseMovRaxFromRcx(exportAddrs["il2cpp_class_get_static_field_data"], out off))
                    _off.ClassStaticFields = off;
                if (exportAddrs.ContainsKey("il2cpp_class_num_fields") && TryParseMovzxWordFromRcx(exportAddrs["il2cpp_class_num_fields"], out off))
                    _off.ClassFieldCount = off;
                if (exportAddrs.ContainsKey("il2cpp_field_get_offset") && TryParseMovsxdFromRcx(exportAddrs["il2cpp_field_get_offset"], out off))
                    _off.FieldInfoOffsetField = (int)off;
                if (exportAddrs.ContainsKey("il2cpp_class_get_fields") && TryParseFieldsOffset(exportAddrs["il2cpp_class_get_fields"], out off))
                    _off.ClassFields = off;

                AddDiag(string.Format("PE导出表自检完成 ({0}/7): name=+0x{1:X}, ns=+0x{2:X}, fields=+0x{3:X}, static=+0x{4:X}, cnt=+0x{5:X}",
                    exportAddrs.Count, _off.ClassName, _off.ClassNamespace, _off.ClassFields, _off.ClassStaticFields, _off.ClassFieldCount));
            }
            catch (Exception ex)
            {
                AddDiag("PE导出表自检使用默认值: " + ex.Message);
            }
        }

        private ulong FollowJmpIfPresent(ulong fnAddr)
        {
            byte[] b = new byte[8];
            if (!ReadBytes(fnAddr, b)) return fnAddr;
            if (b[0] == 0xE9)
            {
                int rel = BitConverter.ToInt32(b, 1);
                return (ulong)((long)fnAddr + 5 + rel);
            }
            return fnAddr;
        }

        private bool TryParseMovRaxFromRcx(ulong fnAddr, out ulong disp)
        {
            disp = 0;
            fnAddr = FollowJmpIfPresent(fnAddr);
            byte[] b = new byte[16];
            if (!ReadBytes(fnAddr, b)) return false;
            // 48 8B 41 disp8  (mov rax, [rcx + disp8])
            if (b[0] == 0x48 && b[1] == 0x8B && b[2] == 0x41) { disp = b[3]; return true; }
            // 48 8B 81 disp32 (mov rax, [rcx + disp32])
            if (b[0] == 0x48 && b[1] == 0x8B && b[2] == 0x81) { disp = BitConverter.ToUInt32(b, 3); return true; }
            return false;
        }

        private bool TryParseMovzxWordFromRcx(ulong fnAddr, out ulong disp)
        {
            disp = 0;
            fnAddr = FollowJmpIfPresent(fnAddr);
            byte[] b = new byte[16];
            if (!ReadBytes(fnAddr, b)) return false;
            // 0F B7 41 disp8  / 0F B7 81 disp32
            if (b[0] == 0x0F && b[1] == 0xB7 && b[2] == 0x41) { disp = b[3]; return true; }
            if (b[0] == 0x0F && b[1] == 0xB7 && b[2] == 0x81) { disp = BitConverter.ToUInt32(b, 3); return true; }
            return false;
        }

        private bool TryParseMovsxdFromRcx(ulong fnAddr, out ulong disp)
        {
            disp = 0;
            fnAddr = FollowJmpIfPresent(fnAddr);
            byte[] b = new byte[16];
            if (!ReadBytes(fnAddr, b)) return false;
            // 48 63 41 disp8 / 8B 41 disp8
            if (b[0] == 0x48 && b[1] == 0x63 && b[2] == 0x41) { disp = b[3]; return true; }
            if (b[0] == 0x8B && b[1] == 0x41) { disp = b[2]; return true; }
            return false;
        }

        private bool TryParseFieldsOffset(ulong fnAddr, out ulong disp)
        {
            disp = 0;
            fnAddr = FollowJmpIfPresent(fnAddr);
            byte[] b = new byte[80];
            if (!ReadBytes(fnAddr, b)) return false;
            // Look for: mov rax, [rbx + disp32] (48 8B 83 xx xx 00 00) or [rbx + disp8] (48 8B 43 xx)
            for (int i = 0; i < b.Length - 7; i++)
            {
                if (b[i] == 0x48 && b[i + 1] == 0x8B && b[i + 2] == 0x83)
                {
                    uint d = BitConverter.ToUInt32(b, i + 3);
                    if (d >= 0x40 && d <= 0x200) { disp = d; return true; }
                }
                if (b[i] == 0x48 && b[i + 1] == 0x8B && b[i + 2] == 0x43)
                {
                    uint d = b[i + 3];
                    if (d >= 0x40 && d <= 0x7F) { disp = d; return true; }
                }
            }
            return false;
        }

        // =========================================================================
        //  PHASE 2: Locate ManagerList Il2CppClass* (Fast-Path + Universal .data Scan)
        // =========================================================================
        private bool IsManagerListClass(ulong klassPtr)
        {
            if (klassPtr < 0x10000000 || klassPtr > 0x7FFFFFFFFFFF || (klassPtr & 7) != 0)
                return false;
            ulong namePtr, nsPtr;
            if (!ReadUInt64(klassPtr + _off.ClassName, out namePtr)) return false;
            if (namePtr < 0x10000000 || namePtr > 0x7FFFFFFFFFFF) return false;
            string nm = ReadCString(namePtr, 24);
            if (!string.Equals(nm, "ManagerList", StringComparison.Ordinal)) return false;
            if (!ReadUInt64(klassPtr + _off.ClassNamespace, out nsPtr)) return false;
            string ns = ReadCString(nsPtr, 32);
            if (!string.Equals(ns, "SiNiSistar2.Manager", StringComparison.Ordinal)) return false;
            ulong sf;
            if (!ReadUInt64(klassPtr + _off.ClassStaticFields, out sf) || sf < 0x10000) return false;
            return true;
        }

        private void TriggerResolveIfNeeded()
        {
            if (_managerListClassPtr != 0 || _isScanning || !IsAttached) return;
            if ((DateTime.UtcNow - _lastScanFailTime).TotalSeconds < 2.0) return;

            // Step 1: Try ultra-fast validated hints (< 0.2ms)
            for (int i = 0; i < DynamicOffsets.FastPathHints.Length; i++)
            {
                ulong rva = DynamicOffsets.FastPathHints[i];
                ulong cand;
                if (ReadUInt64(_gaBase + rva, out cand) && IsManagerListClass(cand))
                {
                    _managerListClassPtr = cand;
                    _managerListSlotRva = rva;
                    _resolveMode = "快速校验命中";
                    AddDiag(string.Format("快速通道校验命中 ManagerList (Slot RVA=0x{0:X}, Class=0x{1:X})", rva, cand));
                    ResolveManagerListFields();
                    return;
                }
            }

            // Step 2: Universal PE .data scan on background thread (works on ANY version!)
            _isScanning = true;
            int snapPid = _pid;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    ulong foundClass = 0, foundSlotRva = 0;
                    if (ScanDataSectionsForManagerList(out foundClass, out foundSlotRva))
                    {
                        lock (_sync)
                        {
                            if (_pid == snapPid && IsAttached)
                            {
                                _managerListClassPtr = foundClass;
                                _managerListSlotRva = foundSlotRva;
                                _resolveMode = string.Format("全自动特征扫描 ({0}ms)", sw.ElapsedMilliseconds);
                            }
                        }
                        AddDiag(string.Format("通用扫描命中 ManagerList! Slot RVA=0x{0:X}, Class=0x{1:X}, 耗时 {2}ms",
                            foundSlotRva, foundClass, sw.ElapsedMilliseconds));
                        ResolveManagerListFields();
                    }
                    else
                    {
                        _lastScanFailTime = DateTime.UtcNow;
                        AddDiag(string.Format("本轮扫描未找到已初始化的 ManagerList ({0}ms)，将在 2 秒后重试...", sw.ElapsedMilliseconds));
                    }
                }
                catch (Exception ex)
                {
                    _lastScanFailTime = DateTime.UtcNow;
                    AddDiag("扫描异常: " + ex.Message);
                }
                finally
                {
                    _isScanning = false;
                }
            });
        }

        private bool ScanDataSectionsForManagerList(out ulong foundClass, out ulong foundSlotRva)
        {
            foundClass = 0;
            foundSlotRva = 0;

            byte[] hdr = new byte[0x1000];
            if (!ReadBytes(_gaBase, hdr)) return false;
            if (hdr[0] != 'M' || hdr[1] != 'Z') return false;
            int peOff = BitConverter.ToInt32(hdr, 0x3C);
            if (peOff <= 0 || peOff + 0x108 >= hdr.Length) return false;

            ushort numSections = BitConverter.ToUInt16(hdr, peOff + 6);
            ushort optSize = BitConverter.ToUInt16(hdr, peOff + 20);
            int secTable = peOff + 24 + optSize;

            ulong gaEnd = _gaBase + (_gaImageSize > 0 ? _gaImageSize : 0x6000000UL);
            HashSet<ulong> seen = new HashSet<ulong>();
            byte[] probeName = new byte[12];
            byte[] probeClass = new byte[0x28];

            for (int s = 0; s < numSections; s++)
            {
                int so = secTable + s * 40;
                if (so + 40 > hdr.Length) break;
                uint chars = BitConverter.ToUInt32(hdr, so + 36);
                // Readable + Writable, non-executable data sections (.data, etc.)
                bool isWritable = (chars & 0x80000000U) != 0;
                bool isExec = (chars & 0x20000000U) != 0;
                if (!isWritable || isExec) continue;

                uint va = BitConverter.ToUInt32(hdr, so + 12);
                uint vsize = BitConverter.ToUInt32(hdr, so + 8);
                if (vsize == 0 || vsize > 32 * 1024 * 1024) continue;

                const uint chunkSize = 0x100000; // 1MB chunks
                byte[] chunk = new byte[chunkSize];
                for (uint off = 0; off < vsize; off += chunkSize)
                {
                    int toRead = (int)Math.Min(chunkSize, vsize - off);
                    IntPtr read;
                    if (!Native.ReadProcessMemory(_handle, _gaBase + va + off, chunk, toRead, out read))
                        continue;
                    int valid = (int)read.ToInt64();
                    for (int pos = 0; pos <= valid - 8; pos += 8)
                    {
                        ulong cand = BitConverter.ToUInt64(chunk, pos);
                        if (cand < 0x10000000UL || cand > 0x7FFFFFFFFFFFUL || (cand & 7) != 0)
                            continue;
                        // Il2CppClass* lives on the native heap, outside GameAssembly.dll
                        if (cand >= _gaBase && cand < gaEnd)
                            continue;
                        if (!seen.Add(cand))
                            continue;

                        // Fast header read: read bytes [0x00..0x27] of candidate class
                        IntPtr r2;
                        if (!Native.ReadProcessMemory(_handle, cand, probeClass, probeClass.Length, out r2) || r2.ToInt64() < 0x20)
                            continue;
                        ulong namePtr = BitConverter.ToUInt64(probeClass, (int)_off.ClassName);
                        if (namePtr < 0x10000000UL || namePtr > 0x7FFFFFFFFFFFUL)
                            continue;

                        // Fast first-12-byte check for "ManagerList\0"
                        IntPtr r3;
                        if (!Native.ReadProcessMemory(_handle, namePtr, probeName, 12, out r3) || r3.ToInt64() < 12)
                            continue;
                        if (probeName[0] != (byte)'M' || probeName[1] != (byte)'a' || probeName[7] != (byte)'L' || probeName[11] != 0)
                            continue;
                        if (Encoding.ASCII.GetString(probeName, 0, 11) != "ManagerList")
                            continue;

                        if (IsManagerListClass(cand))
                        {
                            foundClass = cand;
                            foundSlotRva = va + off + (uint)pos;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // =========================================================================
        //  PHASE 3: Runtime Reflection Field Offset Resolver (By C# Field Name!)
        // =========================================================================
        private Dictionary<string, ulong> ReadClassFieldMap(ulong klassPtr)
        {
            Dictionary<string, ulong> map = new Dictionary<string, ulong>(StringComparer.Ordinal);
            ulong cur = klassPtr;
            int depth = 0;
            while (cur > 0x10000000 && cur < 0x7FFFFFFFFFFF && depth < 6)
            {
                ushort cnt;
                ulong fieldsPtr;
                if (ReadUInt16(cur + _off.ClassFieldCount, out cnt) && cnt > 0 && cnt < 512 &&
                    ReadUInt64(cur + _off.ClassFields, out fieldsPtr) && fieldsPtr > 0x10000)
                {
                    int stride = _off.FieldInfoSize;
                    byte[] raw = new byte[cnt * stride];
                    if (ReadBytes(fieldsPtr, raw))
                    {
                        for (int i = 0; i < cnt; i++)
                        {
                            int baseIdx = i * stride;
                            ulong fnPtr = BitConverter.ToUInt64(raw, baseIdx);
                            int fOff = BitConverter.ToInt32(raw, baseIdx + _off.FieldInfoOffsetField);
                            if (fOff < 0 || fOff > 0x2000) continue;
                            string fn = ReadCString(fnPtr, 96);
                            if (!string.IsNullOrEmpty(fn) && !map.ContainsKey(fn))
                                map[fn] = (ulong)fOff;
                        }
                    }
                }
                ulong parent;
                if (!ReadUInt64(cur + _off.ClassParent, out parent) || parent == cur) break;
                cur = parent;
                depth++;
            }
            return map;
        }

        private bool ApplyField(Dictionary<string, ulong> map, string name, ref ulong target)
        {
            ulong v;
            if (map.TryGetValue(name, out v))
            {
                target = v;
                _resolvedFieldCount++;
                return true;
            }
            return false;
        }

        private void ResolveManagerListFields()
        {
            if (_managerListClassPtr == 0 || _fieldsResolvedCore) return;
            Dictionary<string, ulong> ml = ReadClassFieldMap(_managerListClassPtr);
            if (ml.Count == 0) return;

            ApplyField(ml, "<Instance>k__BackingField", ref _off.ManagerListInstance);
            ApplyField(ml, "m_Debugger", ref _off.MgrDebugger);
            ApplyField(ml, "m_PlayerStatusManager", ref _off.MgrPlayerStatus);
            _fieldsResolvedCore = true;
            AddDiag(string.Format("ManagerList 反射解析: Instance=+0x{0:X}, Debugger=+0x{1:X}, PlayerStatus=+0x{2:X}",
                _off.ManagerListInstance, _off.MgrDebugger, _off.MgrPlayerStatus));
        }

        private void ResolveSubObjectFields(ulong debugger, ulong playerStatus)
        {
            if (_fieldsResolvedSub) return;

            // 1. Debugger
            ulong dbgCls;
            if (debugger > 0x10000 && ReadUInt64(debugger, out dbgCls))
            {
                Dictionary<string, ulong> d = ReadClassFieldMap(dbgCls);
                ApplyField(d, "<CriticalPoise>k__BackingField", ref _off.DbgCriticalPoise);
                ApplyField(d, "<CriticalDamage>k__BackingField", ref _off.DbgCriticalDamage);
                ApplyField(d, "<IsOverwriteDamage>k__BackingField", ref _off.DbgIsOverwriteDamage);
                ApplyField(d, "<OverwriteDamageValue>k__BackingField", ref _off.DbgOverwriteDamageValue);
                ApplyField(d, "<IsDisableHold>k__BackingField", ref _off.DbgIsDisableHold);
                ApplyField(d, "<IsPlayerSuperTough>k__BackingField", ref _off.DbgIsPlayerSuperTough);
                ApplyField(d, "<IsNotIncrementalDefilement>k__BackingField", ref _off.DbgIsNotIncrementalDefilement);
            }

            // 2. PlayerStatusManager
            ulong psCls;
            if (playerStatus > 0x10000 && ReadUInt64(playerStatus, out psCls))
            {
                Dictionary<string, ulong> p = ReadClassFieldMap(psCls);
                ApplyField(p, "m_MaxHP", ref _off.PsMaxHPField);
                ApplyField(p, "m_MaxMp", ref _off.PsMaxMPField);
                ApplyField(p, "<AbnormalList>k__BackingField", ref _off.PsAbnormalList);
                ApplyField(p, "<Durability>k__BackingField", ref _off.PsDurability);
                ApplyField(p, "<HP>k__BackingField", ref _off.PsHP);
                ApplyField(p, "<MP>k__BackingField", ref _off.PsMP);
                ApplyField(p, "m_AttackLvProp", ref _off.PsAttackLvProp);
                ApplyField(p, "<AttackLvProp>k__BackingField", ref _off.PsAttackLvRoProp);
                ApplyField(p, "m_HpLvProp", ref _off.PsHpLvProp);
                ApplyField(p, "<HpLvProp>k__BackingField", ref _off.PsHpLvRoProp);
                ApplyField(p, "m_MpLvProp", ref _off.PsMpLvProp);
                ApplyField(p, "<MpLvProp>k__BackingField", ref _off.PsMpLvRoProp);
                ApplyField(p, "m_RelicsProp", ref _off.PsRelicsProp);
                ApplyField(p, "<RelicsProp>k__BackingField", ref _off.PsRelicsRoProp);
                ApplyField(p, "m_EnhanceParameter", ref _off.PsEnhanceParam);
                ApplyField(p, "<NextAdditiveRelics>k__BackingField", ref _off.PsNextAdditiveRelics);
            }

            // 3. BattleMainParameter (via HP -> parent)
            ulong hpObj, hpCls;
            if (ReadUInt64(playerStatus + _off.PsHP, out hpObj) && hpObj > 0x10000 && ReadUInt64(hpObj, out hpCls))
            {
                Dictionary<string, ulong> b = ReadClassFieldMap(hpCls);
                ApplyField(b, "m_CurrentProperty", ref _off.BmpCurrentRp);
                ApplyField(b, "m_MaxProperty", ref _off.BmpMaxRp);
                ApplyField(b, "<CurrentProperty>k__BackingField", ref _off.BmpCurrentRoRp);
                ApplyField(b, "<MaxProperty>k__BackingField", ref _off.BmpMaxRoRp);
                ApplyField(b, "<DontSub>k__BackingField", ref _off.BmpDontSub);
            }

            // 4. UniRx ReactiveProperty<int> & ReadOnlyReactiveProperty<int>
            ulong rpObj, rpCls, roObj, roCls;
            if (ReadUInt64(playerStatus + _off.PsRelicsProp, out rpObj) && rpObj > 0x10000 && ReadUInt64(rpObj, out rpCls))
            {
                Dictionary<string, ulong> r = ReadClassFieldMap(rpCls);
                ApplyField(r, "value", ref _off.RpValue);
            }
            if (ReadUInt64(playerStatus + _off.PsRelicsRoProp, out roObj) && roObj > 0x10000 && ReadUInt64(roObj, out roCls))
            {
                Dictionary<string, ulong> ro = ReadClassFieldMap(roCls);
                ApplyField(ro, "latestValue", ref _off.RoRpLatestValue);
            }

            // 5. EnhanceParameter & EnhanceAttack
            ulong epObj, epCls;
            if (ReadUInt64(playerStatus + _off.PsEnhanceParam, out epObj) && epObj > 0x10000 && ReadUInt64(epObj, out epCls))
            {
                Dictionary<string, ulong> e = ReadClassFieldMap(epCls);
                ApplyField(e, "m_MaxHPArray", ref _off.EpMaxHPArray);
                ApplyField(e, "m_MaxMPArray", ref _off.EpMaxMPArray);
                ApplyField(e, "m_EnhanceAttackArray", ref _off.EpEnhanceAttackArray);

                ulong eaArr, ea0, eaCls;
                if (ReadUInt64(epObj + _off.EpEnhanceAttackArray, out eaArr) && eaArr > 0x10000 &&
                    ReadUInt64(eaArr + DynamicOffsets.ArrayData, out ea0) && ea0 > 0x10000 &&
                    ReadUInt64(ea0, out eaCls))
                {
                    Dictionary<string, ulong> ea = ReadClassFieldMap(eaCls);
                    ApplyField(ea, "m_PowerArray", ref _off.EaPowerArray);
                }
            }

            // 6. AbnormalList
            ulong abListObj, abListCls;
            if (ReadUInt64(playerStatus + _off.PsAbnormalList, out abListObj) && abListObj > 0x10000 && ReadUInt64(abListObj, out abListCls))
            {
                Dictionary<string, ulong> al = ReadClassFieldMap(abListCls);
                ApplyField(al, "<AddedArray>k__BackingField", ref _off.AbAddedArray);
            }

            _fieldsResolvedSub = true;
            AddDiag(string.Format("全子对象反射解析完成，累计精准匹配 {0} 个字段偏移", _resolvedFieldCount));
        }

        private void ResolveAbnormalDataFieldsIfNeeded(ulong abDataObj)
        {
            if (_fieldsResolvedAbnormalData || abDataObj < 0x10000) return;
            ulong cls;
            if (!ReadUInt64(abDataObj, out cls)) return;
            Dictionary<string, ulong> a = ReadClassFieldMap(cls);
            if (a.Count > 0)
            {
                ApplyField(a, "m_AbnormalType", ref _off.AbDataType);
                ApplyField(a, "m_ToLevel0", ref _off.AbDataToLevel0);
                ApplyField(a, "<DeleteTimer>k__BackingField", ref _off.AbDataDeleteTimer);
                ApplyField(a, "m_LevelRp", ref _off.AbDataLevelRp);
                ApplyField(a, "m_Level", ref _off.AbDataLevel);
                _fieldsResolvedAbnormalData = true;
                AddDiag("AbnormalData 延迟反射解析完成");
            }
        }

        // =========================================================================
        //  GAME MEMORY READ / WRITE OPERATIONS
        // =========================================================================
        private bool GetManagers(out ulong managerList, out ulong debugger, out ulong playerStatus)
        {
            managerList = 0;
            debugger = 0;
            playerStatus = 0;
            if (!IsAttached || _managerListClassPtr == 0) return false;
            ulong staticFields;
            if (!ReadUInt64(_managerListClassPtr + _off.ClassStaticFields, out staticFields) || staticFields < 0x10000) return false;
            if (!ReadUInt64(staticFields + _off.ManagerListInstance, out managerList) || managerList < 0x10000) return false;
            ReadUInt64(managerList + _off.MgrDebugger, out debugger);
            ReadUInt64(managerList + _off.MgrPlayerStatus, out playerStatus);
            if (playerStatus > 0x10000 && !_fieldsResolvedSub)
                ResolveSubObjectFields(debugger, playerStatus);
            return playerStatus > 0x10000;
        }

        private int[] ReadInt32Array(ulong arrPtr)
        {
            if (arrPtr < 0x10000) return null;
            int len;
            if (!ReadInt32(arrPtr + DynamicOffsets.ArrayLength, out len) || len <= 0 || len > 512) return null;
            byte[] raw = new byte[len * 4];
            if (!ReadBytes(arrPtr + DynamicOffsets.ArrayData, raw)) return null;
            int[] res = new int[len];
            Buffer.BlockCopy(raw, 0, res, 0, raw.Length);
            return res;
        }

        private bool WriteInt32Array(ulong arrPtr, int[] values)
        {
            if (arrPtr < 0x10000 || values == null || values.Length == 0) return false;
            int len;
            if (!ReadInt32(arrPtr + DynamicOffsets.ArrayLength, out len) || len < values.Length) return false;
            byte[] raw = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, raw, 0, raw.Length);
            return WriteBytes(arrPtr + DynamicOffsets.ArrayData, raw);
        }

        private void EnsureEnhanceCache(ulong playerStatus)
        {
            ulong ep;
            if (!ReadUInt64(playerStatus + _off.PsEnhanceParam, out ep) || ep < 0x10000) return;
            if (ep == _cachedEnhanceParamPtr && _origPowerArrays.Count > 0) return;

            ClearEnhanceCache();
            _cachedEnhanceParamPtr = ep;

            ulong hpArrPtr, mpArrPtr, atkArrPtr;
            if (ReadUInt64(ep + _off.EpMaxHPArray, out hpArrPtr) && hpArrPtr > 0x10000)
                _origMaxHpArray = ReadInt32Array(hpArrPtr);
            if (ReadUInt64(ep + _off.EpMaxMPArray, out mpArrPtr) && mpArrPtr > 0x10000)
                _origMaxMpArray = ReadInt32Array(mpArrPtr);

            if (ReadUInt64(ep + _off.EpEnhanceAttackArray, out atkArrPtr) && atkArrPtr > 0x10000)
            {
                int len;
                if (ReadInt32(atkArrPtr + DynamicOffsets.ArrayLength, out len) && len > 0 && len <= 256)
                {
                    for (int i = 0; i < len; i++)
                    {
                        ulong eaPtr;
                        if (!ReadUInt64(atkArrPtr + DynamicOffsets.ArrayData + (ulong)(i * 8), out eaPtr) || eaPtr < 0x10000)
                            continue;
                        ulong pArrPtr;
                        if (!ReadUInt64(eaPtr + _off.EaPowerArray, out pArrPtr) || pArrPtr < 0x10000)
                            continue;
                        if (_origPowerArrays.ContainsKey(pArrPtr)) continue;
                        int[] vals = ReadInt32Array(pArrPtr);
                        if (vals != null && vals.Length > 0)
                        {
                            _origPowerArrays[pArrPtr] = vals;
                            if (_samplePowerArrayPtr == 0 && vals[0] > 0)
                                _samplePowerArrayPtr = pArrPtr;
                        }
                    }
                }
            }
        }

        private bool FindDefilementAbnormal(ulong playerStatus, out ulong abDataPtr, out int level)
        {
            abDataPtr = 0;
            level = 0;
            ulong abList, addedArr;
            if (!ReadUInt64(playerStatus + _off.PsAbnormalList, out abList) || abList < 0x10000) return false;
            if (!ReadUInt64(abList + _off.AbAddedArray, out addedArr) || addedArr < 0x10000) return false;
            int len;
            if (!ReadInt32(addedArr + DynamicOffsets.ArrayLength, out len) || len <= 0 || len > 64) return false;
            for (int i = 0; i < len; i++)
            {
                ulong ptr;
                if (!ReadUInt64(addedArr + DynamicOffsets.ArrayData + (ulong)(i * 8), out ptr) || ptr < 0x10000)
                    continue;
                ResolveAbnormalDataFieldsIfNeeded(ptr);
                int abType;
                if (ReadInt32(ptr + _off.AbDataType, out abType) && abType == DynamicOffsets.AbnormalTypeDefilement)
                {
                    abDataPtr = ptr;
                    ulong lvRp;
                    if (ReadUInt64(ptr + _off.AbDataLevelRp, out lvRp) && lvRp > 0x10000)
                        ReadInt32(lvRp + _off.RpValue, out level);
                    else
                        ReadInt32(ptr + _off.AbDataLevel, out level);
                    return true;
                }
            }
            return false;
        }

        private int ReadRpInt(ulong propFieldAddr)
        {
            ulong rp;
            int val;
            if (ReadUInt64(propFieldAddr, out rp) && rp > 0x10000 && ReadInt32(rp + _off.RpValue, out val))
                return val;
            return 0;
        }

        private bool WriteRpAndRoInt(ulong rpFieldAddr, ulong roFieldAddr, int val)
        {
            bool ok = false;
            ulong rp, ro;
            if (ReadUInt64(rpFieldAddr, out rp) && rp > 0x10000)
                ok |= WriteInt32(rp + _off.RpValue, val);
            if (ReadUInt64(roFieldAddr, out ro) && ro > 0x10000)
                ok |= WriteInt32(ro + _off.RoRpLatestValue, val);
            return ok;
        }

        private int ReadBmpCurrent(ulong bmpPtr)
        {
            ulong ro;
            int val;
            if (ReadUInt64(bmpPtr + _off.BmpCurrentRoRp, out ro) && ro > 0x10000 && ReadInt32(ro + _off.RoRpLatestValue, out val))
                return val;
            return ReadRpInt(bmpPtr + _off.BmpCurrentRp);
        }

        private int ReadBmpMax(ulong bmpPtr)
        {
            ulong ro;
            int val;
            if (ReadUInt64(bmpPtr + _off.BmpMaxRoRp, out ro) && ro > 0x10000 && ReadInt32(ro + _off.RoRpLatestValue, out val))
                return val;
            return ReadRpInt(bmpPtr + _off.BmpMaxRp);
        }

        public GameState ReadState()
        {
            GameState s = new GameState();
            if (!EnsureAttached())
            {
                s.AccessDenied = _accessDenied;
                return s;
            }
            s.Connected = true;
            s.Pid = _pid;
            s.GameVersion = _gameVersion;

            if (_managerListClassPtr == 0)
            {
                TriggerResolveIfNeeded();
                s.Scanning = _isScanning;
                return s;
            }

            s.Resolved = true;
            s.ResolveSummary = string.Format("{0} · {1}字段", _resolveMode, _resolvedFieldCount);

            ulong mgr, dbg, ps;
            if (!GetManagers(out mgr, out dbg, out ps)) return s;

            ulong hpPtr, mpPtr, durPtr;
            ReadUInt64(ps + _off.PsHP, out hpPtr);
            ReadUInt64(ps + _off.PsMP, out mpPtr);
            ReadUInt64(ps + _off.PsDurability, out durPtr);

            if (hpPtr > 0x10000)
            {
                s.HpCurrent = ReadBmpCurrent(hpPtr);
                s.HpMax = ReadBmpMax(hpPtr);
                ReadBool(hpPtr + _off.BmpDontSub, out s.HpDontSub);
            }
            if (mpPtr > 0x10000)
            {
                s.MpCurrent = ReadBmpCurrent(mpPtr);
                s.MpMax = ReadBmpMax(mpPtr);
                ReadBool(mpPtr + _off.BmpDontSub, out s.MpDontSub);
            }
            if (durPtr > 0x10000)
                ReadBool(durPtr + _off.BmpDontSub, out s.DurabilityDontSub);

            s.InGame = (hpPtr > 0x10000 && s.HpMax > 0);
            s.Relics = ReadRpInt(ps + _off.PsRelicsProp);
            s.AttackLv = ReadRpInt(ps + _off.PsAttackLvProp);
            s.HpLv = ReadRpInt(ps + _off.PsHpLvProp);
            s.MpLv = ReadRpInt(ps + _off.PsMpLvProp);

            ulong defPtr;
            int defLv;
            if (FindDefilementAbnormal(ps, out defPtr, out defLv))
            {
                s.DefilementLevel = defLv;
            }
            else if (_lastPurifiedDefPtr > 0x10000)
            {
                WriteBool(_lastPurifiedDefPtr + _off.AbDataToLevel0, false);
                _lastPurifiedDefPtr = 0;
            }

            if (dbg > 0x10000)
            {
                ReadBool(dbg + _off.DbgIsNotIncrementalDefilement, out s.DefilementLocked);
                ReadBool(dbg + _off.DbgCriticalDamage, out s.CriticalDamage);
                ReadBool(dbg + _off.DbgCriticalPoise, out s.CriticalPoise);
                ReadBool(dbg + _off.DbgIsOverwriteDamage, out s.IsOverwriteDamage);
                ReadInt32(dbg + _off.DbgOverwriteDamageValue, out s.OverwriteDamageValue);
                ReadBool(dbg + _off.DbgIsDisableHold, out s.IsDisableHold);
                ReadBool(dbg + _off.DbgIsPlayerSuperTough, out s.IsPlayerSuperTough);
            }

            if (s.InGame)
            {
                EnsureEnhanceCache(ps);
                s.AttackEntryCount = _origPowerArrays.Count;
                if (_samplePowerArrayPtr > 0x10000 && _origPowerArrays.ContainsKey(_samplePowerArrayPtr))
                {
                    int[] orig = _origPowerArrays[_samplePowerArrayPtr];
                    int idx = Math.Max(0, Math.Min(orig.Length - 1, s.AttackLv - 1));
                    s.SampleBaseDamage = orig[idx];
                    int cur;
                    if (ReadInt32(_samplePowerArrayPtr + DynamicOffsets.ArrayData + (ulong)(idx * 4), out cur))
                        s.SampleCurrentDamage = cur;
                }
            }

            return s;
        }

        public bool SetHpCurrent(int value)
        {
            ulong mgr, dbg, ps, hpPtr;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;
            if (!ReadUInt64(ps + _off.PsHP, out hpPtr) || hpPtr < 0x10000) return false;
            int max = ReadBmpMax(hpPtr);
            if (max > 0 && value > max) value = max;
            if (value < 1) value = 1;
            return WriteRpAndRoInt(hpPtr + _off.BmpCurrentRp, hpPtr + _off.BmpCurrentRoRp, value);
        }

        public bool SetHpMax(int newMax, bool alsoFillCurrent)
        {
            if (newMax < 10) newMax = 10;
            ulong mgr, dbg, ps, hpPtr;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;
            if (!ReadUInt64(ps + _off.PsHP, out hpPtr) || hpPtr < 0x10000) return false;

            EnsureEnhanceCache(ps);
            bool ok = WriteRpAndRoInt(hpPtr + _off.BmpMaxRp, hpPtr + _off.BmpMaxRoRp, newMax);
            WriteInt32(ps + _off.PsMaxHPField, newMax);

            ulong ep, arrPtr;
            if (ReadUInt64(ps + _off.PsEnhanceParam, out ep) && ep > 0x10000 &&
                ReadUInt64(ep + _off.EpMaxHPArray, out arrPtr) && arrPtr > 0x10000)
            {
                int arrLen;
                if (ReadInt32(arrPtr + DynamicOffsets.ArrayLength, out arrLen) && arrLen > 0)
                {
                    int lv = Math.Max(1, ReadRpInt(ps + _off.PsHpLvProp));
                    int idx = Math.Max(0, Math.Min(arrLen - 1, lv - 1));
                    WriteInt32(arrPtr + DynamicOffsets.ArrayData + (ulong)(idx * 4), newMax);
                }
            }
            if (alsoFillCurrent || ReadBmpCurrent(hpPtr) > newMax)
                WriteRpAndRoInt(hpPtr + _off.BmpCurrentRp, hpPtr + _off.BmpCurrentRoRp, newMax);
            return ok;
        }

        public bool RestoreMaxHp()
        {
            ulong mgr, dbg, ps, hpPtr, ep, arrPtr;
            if (!GetManagers(out mgr, out dbg, out ps) || _origMaxHpArray == null) return false;
            if (ReadUInt64(ps + _off.PsEnhanceParam, out ep) && ep > 0x10000 &&
                ReadUInt64(ep + _off.EpMaxHPArray, out arrPtr) && arrPtr > 0x10000)
                WriteInt32Array(arrPtr, _origMaxHpArray);

            int lv = Math.Max(1, ReadRpInt(ps + _off.PsHpLvProp));
            int idx = Math.Max(0, Math.Min(_origMaxHpArray.Length - 1, lv - 1));
            int origMax = _origMaxHpArray[idx];
            if (ReadUInt64(ps + _off.PsHP, out hpPtr) && hpPtr > 0x10000)
            {
                WriteRpAndRoInt(hpPtr + _off.BmpMaxRp, hpPtr + _off.BmpMaxRoRp, origMax);
                WriteInt32(ps + _off.PsMaxHPField, origMax);
                if (ReadBmpCurrent(hpPtr) > origMax)
                    WriteRpAndRoInt(hpPtr + _off.BmpCurrentRp, hpPtr + _off.BmpCurrentRoRp, origMax);
            }
            return true;
        }

        public bool SetMpCurrent(int value)
        {
            ulong mgr, dbg, ps, mpPtr;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;
            if (!ReadUInt64(ps + _off.PsMP, out mpPtr) || mpPtr < 0x10000) return false;
            int max = ReadBmpMax(mpPtr);
            if (max > 0 && value > max) value = max;
            if (value < 0) value = 0;
            return WriteRpAndRoInt(mpPtr + _off.BmpCurrentRp, mpPtr + _off.BmpCurrentRoRp, value);
        }

        public bool SetMpMax(int newMax, bool alsoFillCurrent)
        {
            if (newMax < 10) newMax = 10;
            ulong mgr, dbg, ps, mpPtr;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;
            if (!ReadUInt64(ps + _off.PsMP, out mpPtr) || mpPtr < 0x10000) return false;

            EnsureEnhanceCache(ps);
            bool ok = WriteRpAndRoInt(mpPtr + _off.BmpMaxRp, mpPtr + _off.BmpMaxRoRp, newMax);
            WriteInt32(ps + _off.PsMaxMPField, newMax);

            ulong ep, arrPtr;
            if (ReadUInt64(ps + _off.PsEnhanceParam, out ep) && ep > 0x10000 &&
                ReadUInt64(ep + _off.EpMaxMPArray, out arrPtr) && arrPtr > 0x10000)
            {
                int arrLen;
                if (ReadInt32(arrPtr + DynamicOffsets.ArrayLength, out arrLen) && arrLen > 0)
                {
                    int lv = Math.Max(1, ReadRpInt(ps + _off.PsMpLvProp));
                    int idx = Math.Max(0, Math.Min(arrLen - 1, lv - 1));
                    WriteInt32(arrPtr + DynamicOffsets.ArrayData + (ulong)(idx * 4), newMax);
                }
            }
            if (alsoFillCurrent || ReadBmpCurrent(mpPtr) > newMax)
                WriteRpAndRoInt(mpPtr + _off.BmpCurrentRp, mpPtr + _off.BmpCurrentRoRp, newMax);
            return ok;
        }

        public bool RestoreMaxMp()
        {
            ulong mgr, dbg, ps, mpPtr, ep, arrPtr;
            if (!GetManagers(out mgr, out dbg, out ps) || _origMaxMpArray == null) return false;
            if (ReadUInt64(ps + _off.PsEnhanceParam, out ep) && ep > 0x10000 &&
                ReadUInt64(ep + _off.EpMaxMPArray, out arrPtr) && arrPtr > 0x10000)
                WriteInt32Array(arrPtr, _origMaxMpArray);

            int lv = Math.Max(1, ReadRpInt(ps + _off.PsMpLvProp));
            int idx = Math.Max(0, Math.Min(_origMaxMpArray.Length - 1, lv - 1));
            int origMax = _origMaxMpArray[idx];
            if (ReadUInt64(ps + _off.PsMP, out mpPtr) && mpPtr > 0x10000)
            {
                WriteRpAndRoInt(mpPtr + _off.BmpMaxRp, mpPtr + _off.BmpMaxRoRp, origMax);
                WriteInt32(ps + _off.PsMaxMPField, origMax);
                if (ReadBmpCurrent(mpPtr) > origMax)
                    WriteRpAndRoInt(mpPtr + _off.BmpCurrentRp, mpPtr + _off.BmpCurrentRoRp, origMax);
            }
            return true;
        }

        public bool SetHpDontSub(bool enabled) { return SetBmpDontSub(_off.PsHP, enabled); }
        public bool SetMpDontSub(bool enabled) { return SetBmpDontSub(_off.PsMP, enabled); }
        public bool SetDurabilityDontSub(bool enabled) { return SetBmpDontSub(_off.PsDurability, enabled); }

        private bool SetBmpDontSub(ulong bmpFieldOff, bool enabled)
        {
            ulong mgr, dbg, ps, bmpPtr;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;
            if (!ReadUInt64(ps + bmpFieldOff, out bmpPtr) || bmpPtr < 0x10000) return false;
            return WriteBool(bmpPtr + _off.BmpDontSub, enabled);
        }

        public bool SetLockDefilement(bool enabled) { return SetDbgBool(_off.DbgIsNotIncrementalDefilement, enabled); }
        public bool SetCriticalDamage(bool enabled) { return SetDbgBool(_off.DbgCriticalDamage, enabled); }
        public bool SetCriticalPoise(bool enabled) { return SetDbgBool(_off.DbgCriticalPoise, enabled); }
        public bool SetDisableHold(bool enabled) { return SetDbgBool(_off.DbgIsDisableHold, enabled); }
        public bool SetSuperTough(bool enabled) { return SetDbgBool(_off.DbgIsPlayerSuperTough, enabled); }

        private bool SetDbgBool(ulong fieldOff, bool enabled)
        {
            ulong mgr, dbg, ps;
            if (!GetManagers(out mgr, out dbg, out ps) || dbg < 0x10000) return false;
            return WriteBool(dbg + fieldOff, enabled);
        }

        public bool SetOverwriteDamage(bool enabled, int damageValue)
        {
            ulong mgr, dbg, ps;
            if (!GetManagers(out mgr, out dbg, out ps) || dbg < 0x10000) return false;
            WriteInt32(dbg + _off.DbgOverwriteDamageValue, Math.Max(1, damageValue));
            return WriteBool(dbg + _off.DbgIsOverwriteDamage, enabled);
        }

        public bool SetRelics(int target)
        {
            if (target < 0) target = 0;
            ulong mgr, dbg, ps, hpPtr;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;

            bool hpAlive = ReadUInt64(ps + _off.PsHP, out hpPtr) && hpPtr > 0x10000 && ReadBmpCurrent(hpPtr) > 0;
            if (hpAlive)
            {
                WriteRpAndRoInt(ps + _off.PsRelicsProp, ps + _off.PsRelicsRoProp, target - 1);
                return WriteInt32(ps + _off.PsNextAdditiveRelics, 1);
            }
            return WriteRpAndRoInt(ps + _off.PsRelicsProp, ps + _off.PsRelicsRoProp, target);
        }

        public bool SetStatLevels(int attackLv, int hpLv, int mpLv)
        {
            ulong mgr, dbg, ps;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;
            attackLv = Math.Max(1, Math.Min(99, attackLv));
            hpLv = Math.Max(1, Math.Min(99, hpLv));
            mpLv = Math.Max(1, Math.Min(99, mpLv));
            bool ok = true;
            ok &= WriteRpAndRoInt(ps + _off.PsAttackLvProp, ps + _off.PsAttackLvRoProp, attackLv);
            ok &= WriteRpAndRoInt(ps + _off.PsHpLvProp, ps + _off.PsHpLvRoProp, hpLv);
            ok &= WriteRpAndRoInt(ps + _off.PsMpLvProp, ps + _off.PsMpLvRoProp, mpLv);
            return ok;
        }

        public bool ApplyAttackMultiplier(double multiplier)
        {
            ulong mgr, dbg, ps;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;
            EnsureEnhanceCache(ps);
            if (_origPowerArrays.Count == 0) return false;

            multiplier = Math.Max(0.1, Math.Min(1000.0, multiplier));
            bool anyOk = false;
            foreach (KeyValuePair<ulong, int[]> kv in _origPowerArrays)
            {
                int[] orig = kv.Value;
                int[] scaled = new int[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    if (orig[i] <= 0) scaled[i] = orig[i];
                    else scaled[i] = Math.Max(1, (int)Math.Round(orig[i] * multiplier));
                }
                anyOk |= WriteInt32Array(kv.Key, scaled);
            }
            return anyOk;
        }

        public bool DecreaseDefilement(bool purifyAll)
        {
            ulong mgr, dbg, ps, abPtr;
            int lv;
            if (!GetManagers(out mgr, out dbg, out ps)) return false;
            if (!FindDefilementAbnormal(ps, out abPtr, out lv) || abPtr < 0x10000) return false;

            WriteBool(abPtr + _off.AbDataToLevel0, purifyAll);
            if (purifyAll) _lastPurifiedDefPtr = abPtr;
            return WriteFloat(abPtr + _off.AbDataDeleteTimer, 0.0001f);
        }

        public void ResetAllNativeFlags()
        {
            if (!IsAttached) return;
            SetHpDontSub(false);
            SetMpDontSub(false);
            SetDurabilityDontSub(false);
            SetLockDefilement(false);
            SetCriticalDamage(false);
            SetCriticalPoise(false);
            SetOverwriteDamage(false, 999);
            SetDisableHold(false);
            SetSuperTough(false);
            ApplyAttackMultiplier(1.0);
            RestoreMaxHp();
            RestoreMaxMp();
        }
    }

    // =========================================================================
    //  GOTHIC CATHEDRAL UI THEME & CUSTOM CONTROLS
    // =========================================================================
    internal static class Theme
    {
        // Directly inspired by SiNiSistar 2's Moonlit Cathedral aesthetic:
        // Midnight Obsidian + Cathedral Stone + Pale Moonlit Violet + Crimson Rose Petals + Lily Silver
        public static readonly Color BgAbyss       = Color.FromArgb(13, 11, 18);
        public static readonly Color CardBg        = Color.FromArgb(22, 19, 31);
        public static readonly Color CardHeaderBg  = Color.FromArgb(28, 24, 41);
        public static readonly Color InputBg       = Color.FromArgb(15, 13, 22);
        public static readonly Color BorderSubtle  = Color.FromArgb(46, 39, 64);
        public static readonly Color BorderBright  = Color.FromArgb(84, 69, 118);

        public static readonly Color TextPrimary   = Color.FromArgb(238, 234, 246);
        public static readonly Color TextSecondary = Color.FromArgb(158, 149, 178);
        public static readonly Color TextMuted     = Color.FromArgb(106, 98, 126);

        public static readonly Color VioletMain    = Color.FromArgb(138, 107, 199);
        public static readonly Color VioletSoft    = Color.FromArgb(184, 164, 224);
        public static readonly Color VioletDark    = Color.FromArgb(52, 42, 76);

        public static readonly Color CrimsonMain   = Color.FromArgb(168, 50, 78);
        public static readonly Color CrimsonHover  = Color.FromArgb(196, 62, 94);
        public static readonly Color CrimsonSoft   = Color.FromArgb(226, 128, 152);
        public static readonly Color CrimsonDark   = Color.FromArgb(62, 26, 38);

        public static readonly Color PlumDefile    = Color.FromArgb(128, 72, 146);
        public static readonly Color GoldAntique   = Color.FromArgb(196, 168, 126);

        public static readonly Color BtnStone      = Color.FromArgb(36, 31, 50);
        public static readonly Color BtnStoneHover = Color.FromArgb(50, 43, 68);

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class GothicButton : Control
    {
        public Color FillNormal { get; set; }
        public Color FillHover { get; set; }
        public Color BorderNormal { get; set; }
        public Color BorderHover { get; set; }
        private bool _hover;
        private bool _down;

        public GothicButton(string text, Color fill, Color hover, Color border, Color textColor, int w, int h)
        {
            Text = text;
            FillNormal = fill;
            FillHover = hover;
            BorderNormal = border;
            BorderHover = Color.FromArgb(Math.Min(255, border.R + 36), Math.Min(255, border.G + 32), Math.Min(255, border.B + 44));
            ForeColor = textColor;
            Size = new Size(w, h);
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 8.6f, FontStyle.Regular);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = !Enabled ? Color.FromArgb(28, 25, 36) : (_down ? ControlPaint.Dark(FillNormal, 0.1f) : (_hover ? FillHover : FillNormal));
            Color border = !Enabled ? Theme.BorderSubtle : (_hover ? BorderHover : BorderNormal);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(rect, 5))
            using (SolidBrush br = new SolidBrush(fill))
            using (Pen pen = new Pen(border, 1))
            {
                g.FillPath(br, path);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class GothicSwitch : Control
    {
        private bool _checked;
        private bool _hover;
        public Color ActiveColor { get; set; }
        public string LabelText { get; set; }
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public GothicSwitch(string label, Color activeColor, int w, int h)
        {
            LabelText = label;
            ActiveColor = activeColor;
            Size = new Size(w, h);
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 8.8f, FontStyle.Regular);
            ForeColor = Theme.TextPrimary;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackH = Math.Max(14, (int)(Height * 0.68f));
            int trackW = (int)(trackH * 1.85f);
            int trackY = (Height - trackH) / 2;
            Rectangle trackRect = new Rectangle(0, trackY, trackW, trackH);

            Color trackFill = _checked ? ActiveColor : (_hover ? Color.FromArgb(44, 38, 60) : Color.FromArgb(30, 26, 42));
            Color trackBorder = _checked ? ControlPaint.Light(ActiveColor, 0.2f) : (_hover ? Theme.BorderBright : Theme.BorderSubtle);

            using (GraphicsPath path = Theme.RoundedRect(trackRect, trackH / 2))
            using (SolidBrush br = new SolidBrush(trackFill))
            using (Pen pen = new Pen(trackBorder, 1))
            {
                g.FillPath(br, path);
                g.DrawPath(pen, path);
            }

            int pad = Math.Max(2, trackH / 6);
            int knobD = trackH - pad * 2;
            int knobX = _checked ? (trackW - knobD - pad) : pad;
            int knobY = trackY + pad;
            using (SolidBrush kbr = new SolidBrush(_checked ? Color.FromArgb(248, 244, 252) : Color.FromArgb(148, 138, 168)))
            {
                g.FillEllipse(kbr, knobX, knobY, knobD, knobD);
            }

            Rectangle txtRect = new Rectangle(trackW + 8, 0, Width - trackW - 8, Height);
            Color tc = _checked ? Theme.TextPrimary : (_hover ? Theme.TextPrimary : Theme.TextSecondary);
            TextRenderer.DrawText(g, LabelText, Font, txtRect, tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class GothicStepper : Panel
    {
        private readonly TextBox _tb;
        private readonly GothicButton _btnMinus;
        private readonly GothicButton _btnPlus;
        private decimal _min;
        private decimal _max;
        private decimal _step;
        private int _decimals;
        private decimal _value;

        public event EventHandler ValueCommitted;

        public decimal Value
        {
            get { ParseText(); return _value; }
            set
            {
                decimal clamped = Math.Max(_min, Math.Min(_max, value));
                _value = Math.Round(clamped, _decimals);
                string txt = _decimals > 0 ? _value.ToString("0." + new string('0', _decimals), CultureInfo.InvariantCulture) : ((int)_value).ToString();
                if (_tb.Text != txt) _tb.Text = txt;
            }
        }

        public bool IsEditing { get { return _tb.Focused; } }

        public GothicStepper(int x, int y, int w, int h, decimal min, decimal max, decimal initVal, int decimals, decimal step, float scale)
        {
            _min = min;
            _max = max;
            _decimals = decimals;
            _step = step;

            Location = new Point(x, y);
            Size = new Size(w, h);
            BackColor = Theme.InputBg;

            int btnW = (int)Math.Round(20 * scale);
            _btnMinus = new GothicButton("-", Theme.BtnStone, Theme.BtnStoneHover, Theme.BorderSubtle, Theme.TextSecondary, btnW, h - 2);
            _btnMinus.Location = new Point(1, 1);
            _btnMinus.Click += delegate { Value = Value - _step; };

            _btnPlus = new GothicButton("+", Theme.BtnStone, Theme.BtnStoneHover, Theme.BorderSubtle, Theme.TextSecondary, btnW, h - 2);
            _btnPlus.Location = new Point(w - btnW - 1, 1);
            _btnPlus.Click += delegate { Value = Value + _step; };

            _tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBg,
                ForeColor = Theme.TextPrimary,
                TextAlign = HorizontalAlignment.Center,
                Font = new Font("Microsoft YaHei UI", 8.8f),
                Location = new Point(btnW + 3, Math.Max(3, (h - (int)Math.Round(16 * scale)) / 2)),
                Width = Math.Max(20, w - btnW * 2 - 6)
            };
            _tb.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ParseText();
                    if (ValueCommitted != null) ValueCommitted(this, EventArgs.Empty);
                }
                else if (e.KeyCode == Keys.Up) { e.SuppressKeyPress = true; Value = Value + _step; }
                else if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; Value = Value - _step; }
            };
            _tb.Leave += delegate { ParseText(); };
            _tb.MouseWheel += delegate(object s, MouseEventArgs e)
            {
                Value = Value + (e.Delta > 0 ? _step : -_step);
            };

            Controls.Add(_btnMinus);
            Controls.Add(_tb);
            Controls.Add(_btnPlus);
            Value = initVal;
        }

        private void ParseText()
        {
            decimal parsed;
            if (decimal.TryParse(_tb.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                Value = parsed;
            else
                Value = _value;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(_tb.Focused ? Theme.VioletMain : Theme.BorderSubtle, 1))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    internal sealed class GothicBar : Control
    {
        public int Value { get; set; }
        public int Maximum { get; set; }
        public int Segments { get; set; }
        public Color FillStart { get; set; }
        public Color FillEnd { get; set; }

        public GothicBar(Color start, Color end, int max, int segments)
        {
            FillStart = start;
            FillEnd = end;
            Maximum = max;
            Segments = segments;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle full = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush tr = new SolidBrush(Color.FromArgb(14, 12, 20)))
            using (Pen bp = new Pen(Theme.BorderSubtle, 1))
            {
                g.FillRectangle(tr, full);
                if (Segments > 1)
                {
                    int gap = 2;
                    float segW = (float)(Width - 2 - (Segments - 1) * gap) / Segments;
                    for (int i = 0; i < Segments; i++)
                    {
                        float x = 1 + i * (segW + gap);
                        RectangleF sr = new RectangleF(x, 1, Math.Max(2f, segW), Math.Max(2, Height - 2));
                        using (SolidBrush sbr = new SolidBrush(i < Value ? FillEnd : Color.FromArgb(26, 22, 36)))
                            g.FillRectangle(sbr, sr);
                    }
                }
                else
                {
                    double ratio = Maximum > 0 ? Math.Max(0.0, Math.Min(1.0, (double)Value / Maximum)) : 0.0;
                    int w = (int)Math.Round((Width - 2) * ratio);
                    if (w > 1)
                    {
                        Rectangle fr = new Rectangle(1, 1, w, Math.Max(2, Height - 2));
                        using (LinearGradientBrush lbr = new LinearGradientBrush(fr, FillStart, FillEnd, 0f))
                            g.FillRectangle(lbr, fr);
                    }
                }
                g.DrawRectangle(bp, full);
            }
        }
    }

    // =========================================================================
    //  MAIN TRAINER WINDOW
    // =========================================================================
    public sealed class TrainerForm : Form
    {
        private readonly float _dpi = 1.0f;
        private readonly MemoryBridge _mem = new MemoryBridge();
        private readonly System.Windows.Forms.Timer _pollTimer = new System.Windows.Forms.Timer();

        // Sticky user states (auto-reapplied on scene transitions / game reload)
        private bool _wantLockHp;
        private bool _wantLockMp;
        private bool _wantLockDefilement;
        private bool _wantOneHitKill;
        private bool _wantOverwriteDmg;
        private int _wantOverwriteDmgVal = 999;
        private bool _wantDisableHold;
        private bool _wantSuperTough;
        private bool _wantCritPoise;
        private bool _wantLockDurability;
        private double _currentAtkMult = 1.0;
        private int? _customMaxHp = null;
        private int? _customMaxMp = null;
        private bool _suppressEvents = false;
        private bool _hotkeysEnabled = true;
        private bool _wasInGame = false;

        // UI controls
        private Panel _banner;
        private string _statusBadgeText = "○ 等待 SiNiSistar2.exe 启动";
        private Color _statusBadgeColor = Theme.GoldAntique;
        private string _subStatusText = "自动扫描模式 · 支持全版本 IL2CPP 运行时适配";

        private GothicSwitch _swTopMost;
        private GothicSwitch _swHotkeys;

        private Label _lblHpVal;
        private GothicBar _barHp;
        private GothicSwitch _swLockHp;
        private GothicStepper _stHpCur;
        private GothicStepper _stHpMax;

        private Label _lblMpVal;
        private GothicBar _barMp;
        private GothicSwitch _swLockMp;
        private GothicStepper _stMpCur;
        private GothicStepper _stMpMax;

        private Label _lblDefVal;
        private GothicBar _barDef;
        private GothicSwitch _swLockDef;

        private Label _lblAtkInfo;
        private GothicStepper _stAtkMult;
        private GothicSwitch _swOverwriteDmg;
        private GothicStepper _stOverwriteDmg;
        private GothicSwitch _swOneHitKill;

        private Label _lblRelicsVal;
        private GothicStepper _stRelics;
        private GothicStepper _stAtkLv;
        private GothicStepper _stHpLv;
        private GothicStepper _stMpLv;

        private GothicSwitch _swDisableHold;
        private GothicSwitch _swSuperTough;
        private GothicSwitch _swCritPoise;
        private GothicSwitch _swLockDurability;

        private Label _lblFooter;

        private const int HK_LOCK_HP = 101;
        private const int HK_LOCK_MP = 102;
        private const int HK_LOCK_DEF = 103;
        private const int HK_PURIFY_DEF = 104;
        private const int HK_ONE_HIT = 105;

        public TrainerForm()
        {
            using (Graphics g = CreateGraphics())
                _dpi = Math.Max(1.0f, g.DpiX / 96.0f);

            Text = "SiNiSistar 2 · Sanctum Trainer (通用自动适配版)";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgAbyss;
            ForeColor = Theme.TextPrimary;
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(S(596), S(766));

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUI();
            ApplyDarkTitleBar();

            _pollTimer.Interval = 250;
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();

            Shown += delegate { RegisterHotkeys(); OnPollTick(null, EventArgs.Empty); };
            FormClosing += delegate
            {
                _pollTimer.Stop();
                UnregisterHotkeys();
                _mem.Dispose();
            };
        }

        private int S(int px) { return (int)Math.Round(px * _dpi); }

        private void ApplyDarkTitleBar()
        {
            try
            {
                int val = 1;
                Native.DwmSetWindowAttribute(Handle, 20, ref val, sizeof(int));
            }
            catch { }
        }

        private void RegisterHotkeys()
        {
            UnregisterHotkeys();
            if (!_hotkeysEnabled) return;
            Native.RegisterHotKey(Handle, HK_LOCK_HP, Native.MOD_NONE, (uint)Keys.F1);
            Native.RegisterHotKey(Handle, HK_LOCK_MP, Native.MOD_NONE, (uint)Keys.F2);
            Native.RegisterHotKey(Handle, HK_LOCK_DEF, Native.MOD_NONE, (uint)Keys.F3);
            Native.RegisterHotKey(Handle, HK_PURIFY_DEF, Native.MOD_NONE, (uint)Keys.F4);
            Native.RegisterHotKey(Handle, HK_ONE_HIT, Native.MOD_NONE, (uint)Keys.F5);
        }

        private void UnregisterHotkeys()
        {
            Native.UnregisterHotKey(Handle, HK_LOCK_HP);
            Native.UnregisterHotKey(Handle, HK_LOCK_MP);
            Native.UnregisterHotKey(Handle, HK_LOCK_DEF);
            Native.UnregisterHotKey(Handle, HK_PURIFY_DEF);
            Native.UnregisterHotKey(Handle, HK_ONE_HIT);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HK_LOCK_HP) _swLockHp.Checked = !_swLockHp.Checked;
                else if (id == HK_LOCK_MP) _swLockMp.Checked = !_swLockMp.Checked;
                else if (id == HK_LOCK_DEF) _swLockDef.Checked = !_swLockDef.Checked;
                else if (id == HK_PURIFY_DEF) DoPurifyDefilement(true);
                else if (id == HK_ONE_HIT) _swOneHitKill.Checked = !_swOneHitKill.Checked;
            }
            base.WndProc(ref m);
        }

        private Panel MakeGothicCard(string title, string subtitle, int x, int y, int w, int h)
        {
            Panel card = new Panel
            {
                Location = new Point(S(x), S(y)),
                Size = new Size(S(w), S(h)),
                BackColor = Theme.CardBg
            };
            int hdrH = S(30);
            card.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Subtle top header strip
                using (SolidBrush hbr = new SolidBrush(Theme.CardHeaderBg))
                    g.FillRectangle(hbr, 1, 1, card.Width - 2, hdrH);
                // Divider under header
                using (Pen div = new Pen(Theme.BorderSubtle, 1))
                    g.DrawLine(div, 1, hdrH, card.Width - 2, hdrH);
                // Outer stone frame
                using (Pen border = new Pen(Theme.BorderSubtle, 1))
                    g.DrawRectangle(border, 0, 0, card.Width - 1, card.Height - 1);
                // Delicate gothic corner diamond ornament (top-left)
                int dx = S(13), dy = hdrH / 2, dr = Math.Max(3, S(4));
                Point[] dm = new Point[] { new Point(dx, dy - dr), new Point(dx + dr, dy), new Point(dx, dy + dr), new Point(dx - dr, dy) };
                using (SolidBrush dbr = new SolidBrush(Theme.CrimsonMain))
                    g.FillPolygon(dbr, dm);
                // Subtle violet hairline accent on top center
                using (Pen accent = new Pen(Color.FromArgb(95, Theme.VioletMain), 1))
                    g.DrawLine(accent, S(12), 0, S(130), 0);
            };

            Label lblTitle = new Label
            {
                Text = title,
                Location = new Point(S(24), S(6)),
                AutoSize = true,
                BackColor = Theme.CardHeaderBg,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Bold)
            };
            card.Controls.Add(lblTitle);

            if (!string.IsNullOrEmpty(subtitle))
            {
                Label lblSub = new Label
                {
                    Text = subtitle,
                    UseMnemonic = false,
                    Location = new Point(S(w - 250), S(7)),
                    Size = new Size(S(236), S(18)),
                    TextAlign = ContentAlignment.MiddleRight,
                    BackColor = Theme.CardHeaderBg,
                    ForeColor = Theme.TextMuted,
                    Font = new Font("Microsoft YaHei UI", 8.0f)
                };
                card.Controls.Add(lblSub);
            }

            Controls.Add(card);
            return card;
        }

        private GothicButton BtnStone(string text, int x, int y, int w, int h)
        {
            GothicButton b = new GothicButton(text, Theme.BtnStone, Theme.BtnStoneHover, Theme.BorderSubtle, Theme.TextPrimary, S(w), S(h));
            b.Location = new Point(S(x), S(y));
            return b;
        }

        private GothicButton BtnCrimson(string text, int x, int y, int w, int h)
        {
            GothicButton b = new GothicButton(text, Theme.CrimsonMain, Theme.CrimsonHover, Color.FromArgb(204, 82, 112), Theme.TextPrimary, S(w), S(h));
            b.Location = new Point(S(x), S(y));
            return b;
        }

        private GothicButton BtnViolet(string text, int x, int y, int w, int h)
        {
            GothicButton b = new GothicButton(text, Color.FromArgb(72, 54, 112), Color.FromArgb(92, 70, 142), Theme.VioletMain, Theme.TextPrimary, S(w), S(h));
            b.Location = new Point(S(x), S(y));
            return b;
        }

        private GothicStepper MakeStepper(int x, int y, int w, int h, decimal min, decimal max, decimal initVal, int decimals, decimal step)
        {
            return new GothicStepper(S(x), S(y), S(w), S(h), min, max, initVal, decimals, step, _dpi);
        }

        private void BuildUI()
        {
            int pad = 16;
            int cardW = 564;

            // =================================================================
            //  GOTHIC SANCTUARY BANNER HEADER
            // =================================================================
            _banner = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width, S(94)),
                BackColor = Theme.CardBg
            };
            _banner.Paint += PaintBanner;

            _swTopMost = new GothicSwitch("置顶窗口", Theme.VioletMain, S(98), S(22));
            _swTopMost.Location = new Point(S(pad), S(64));
            _swTopMost.CheckedChanged += delegate { TopMost = _swTopMost.Checked; };
            _banner.Controls.Add(_swTopMost);

            _swHotkeys = new GothicSwitch("全局热键 F1~F5", Theme.VioletMain, S(142), S(22));
            _swHotkeys.Location = new Point(S(pad + 104), S(64));
            _swHotkeys.Checked = true;
            _swHotkeys.CheckedChanged += delegate
            {
                _hotkeysEnabled = _swHotkeys.Checked;
                RegisterHotkeys();
                SetFooter(_hotkeysEnabled ? "全局热键已启用 (F1锁血 / F2锁蓝 / F3锁污秽 / F4净化 / F5秒杀)" : "全局热键已暂停");
            };
            _banner.Controls.Add(_swHotkeys);

            GothicButton btnDiag = BtnStone("偏移诊断", 322, 62, 74, 24);
            btnDiag.Click += delegate { ShowDiagnosticsDialog(); };
            _banner.Controls.Add(btnDiag);

            GothicButton btnMods = BtnStone("Mod目录", 402, 62, 74, 24);
            btnMods.Click += delegate { OpenModFolder(); };
            _banner.Controls.Add(btnMods);

            GothicButton btnLaunch = BtnCrimson("启动游戏", 482, 62, 98, 24);
            btnLaunch.Click += delegate { LaunchSteamGame(); };
            _banner.Controls.Add(btnLaunch);

            Controls.Add(_banner);

            int y = 106;

            // =================================================================
            //  CARD 1: 生命与魔力 (VITALITY & MANA)
            // =================================================================
            Panel cardVit = MakeGothicCard("生命与魔力", "VITALITY & MANA", pad, y, cardW, 184);

            _lblHpVal = new Label { Text = "HP   -- / --", Location = new Point(S(14), S(38)), Size = new Size(S(210), S(20)), ForeColor = Theme.TextPrimary, Font = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Bold) };
            _swLockHp = new GothicSwitch("锁血 · 原生免扣血 [F1]", Theme.CrimsonMain, S(220), S(22));
            _swLockHp.Location = new Point(S(330), S(37));
            _swLockHp.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantLockHp = _swLockHp.Checked;
                _mem.SetHpDontSub(_wantLockHp);
                SetFooter(_wantLockHp ? "已开启锁血 (原生 HP.DontSub)" : "已关闭锁血");
            };

            _barHp = new GothicBar(Color.FromArgb(124, 34, 56), Theme.CrimsonHover, 100, 1);
            _barHp.Location = new Point(S(14), S(62));
            _barHp.Size = new Size(S(cardW - 28), S(7));

            Label lHpCur = new Label { Text = "当前", Location = new Point(S(14), S(79)), AutoSize = true, ForeColor = Theme.TextSecondary };
            _stHpCur = MakeStepper(48, 75, 96, 26, 1, 99999, 100, 0, 10);
            _stHpCur.ValueCommitted += delegate { if (_mem.SetHpCurrent((int)_stHpCur.Value)) SetFooter("已写入当前 HP = " + (int)_stHpCur.Value); };
            GothicButton bSetHpCur = BtnStone("应用", 150, 75, 52, 26);
            bSetHpCur.Click += delegate { if (_mem.SetHpCurrent((int)_stHpCur.Value)) SetFooter("已写入当前 HP = " + (int)_stHpCur.Value); };
            GothicButton bFullHp = BtnCrimson("回满", 208, 75, 54, 26);
            bFullHp.Click += delegate { GameState st = _mem.ReadState(); if (st.InGame && _mem.SetHpCurrent(st.HpMax)) SetFooter("HP 已恢复至上限 (" + st.HpMax + ")"); };

            Label lHpMax = new Label { Text = "上限", Location = new Point(S(284), S(79)), AutoSize = true, ForeColor = Theme.TextSecondary };
            _stHpMax = MakeStepper(318, 75, 100, 26, 10, 99999, 100, 0, 20);
            _stHpMax.ValueCommitted += delegate { ApplyCustomMaxHp(); };
            GothicButton bSetHpMax = BtnCrimson("改上限", 424, 75, 64, 26);
            bSetHpMax.Click += delegate { ApplyCustomMaxHp(); };
            GothicButton bResHpMax = BtnStone("还原", 494, 75, 56, 26);
            bResHpMax.Click += delegate { _customMaxHp = null; if (_mem.RestoreMaxHp()) SetFooter("已恢复原版血量上限"); };

            // MP Row
            _lblMpVal = new Label { Text = "MP   -- / --", Location = new Point(S(14), S(112)), Size = new Size(S(210), S(20)), ForeColor = Theme.TextPrimary, Font = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Bold) };
            _swLockMp = new GothicSwitch("锁蓝 · 无限箭矢魔法 [F2]", Theme.VioletMain, S(220), S(22));
            _swLockMp.Location = new Point(S(330), S(111));
            _swLockMp.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantLockMp = _swLockMp.Checked;
                _mem.SetMpDontSub(_wantLockMp);
                SetFooter(_wantLockMp ? "已开启锁蓝 / 无限箭矢 (原生 MP.DontSub)" : "已关闭锁蓝");
            };

            _barMp = new GothicBar(Color.FromArgb(74, 56, 120), Theme.VioletMain, 100, 1);
            _barMp.Location = new Point(S(14), S(136));
            _barMp.Size = new Size(S(cardW - 28), S(7));

            Label lMpCur = new Label { Text = "当前", Location = new Point(S(14), S(153)), AutoSize = true, ForeColor = Theme.TextSecondary };
            _stMpCur = MakeStepper(48, 149, 96, 26, 0, 99999, 100, 0, 10);
            _stMpCur.ValueCommitted += delegate { if (_mem.SetMpCurrent((int)_stMpCur.Value)) SetFooter("已写入当前 MP = " + (int)_stMpCur.Value); };
            GothicButton bSetMpCur = BtnStone("应用", 150, 149, 52, 26);
            bSetMpCur.Click += delegate { if (_mem.SetMpCurrent((int)_stMpCur.Value)) SetFooter("已写入当前 MP = " + (int)_stMpCur.Value); };
            GothicButton bFullMp = BtnViolet("回满", 208, 149, 54, 26);
            bFullMp.Click += delegate { GameState st = _mem.ReadState(); if (st.InGame && _mem.SetMpCurrent(st.MpMax)) SetFooter("MP 已恢复至上限 (" + st.MpMax + ")"); };

            Label lMpMax = new Label { Text = "上限", Location = new Point(S(284), S(153)), AutoSize = true, ForeColor = Theme.TextSecondary };
            _stMpMax = MakeStepper(318, 149, 100, 26, 10, 99999, 100, 0, 20);
            _stMpMax.ValueCommitted += delegate { ApplyCustomMaxMp(); };
            GothicButton bSetMpMax = BtnViolet("改上限", 424, 149, 64, 26);
            bSetMpMax.Click += delegate { ApplyCustomMaxMp(); };
            GothicButton bResMpMax = BtnStone("还原", 494, 149, 56, 26);
            bResMpMax.Click += delegate { _customMaxMp = null; if (_mem.RestoreMaxMp()) SetFooter("已恢复原版魔力上限"); };

            cardVit.Controls.AddRange(new Control[]
            {
                _lblHpVal, _swLockHp, _barHp, lHpCur, _stHpCur, bSetHpCur, bFullHp, lHpMax, _stHpMax, bSetHpMax, bResHpMax,
                _lblMpVal, _swLockMp, _barMp, lMpCur, _stMpCur, bSetMpCur, bFullMp, lMpMax, _stMpMax, bSetMpMax, bResMpMax
            });
            y += 194;

            // =================================================================
            //  CARD 2: 污秽净化 (DEFILEMENT)
            // =================================================================
            Panel cardDef = MakeGothicCard("污秽净化", "DEFILEMENT PURIFICATION", pad, y, cardW, 96);

            _lblDefVal = new Label { Text = "当前污秽度:  0 / 10  (纯净)", Location = new Point(S(14), S(39)), Size = new Size(S(240), S(20)), ForeColor = Theme.TextPrimary, Font = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Bold) };
            _swLockDef = new GothicSwitch("锁定污秽 · 免疫累积 [F3]", Theme.CrimsonMain, S(220), S(22));
            _swLockDef.Location = new Point(S(330), S(38));
            _swLockDef.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantLockDefilement = _swLockDef.Checked;
                _mem.SetLockDefilement(_wantLockDefilement);
                SetFooter(_wantLockDefilement ? "已锁定污秽值 (免疫新增污秽)" : "已解除污秽锁定");
            };

            _barDef = new GothicBar(Theme.PlumDefile, Color.FromArgb(174, 96, 196), 10, 10);
            _barDef.Location = new Point(S(14), S(67));
            _barDef.Size = new Size(S(260), S(11));

            GothicButton bDefMinus = BtnStone("污秽 -1", 296, 62, 86, 26);
            bDefMinus.Click += delegate { DoPurifyDefilement(false); };

            GothicButton bDefZero = BtnViolet("一键净化归零 [F4]", 390, 62, 158, 26);
            bDefZero.Click += delegate { DoPurifyDefilement(true); };

            cardDef.Controls.AddRange(new Control[] { _lblDefVal, _swLockDef, _barDef, bDefMinus, bDefZero });
            y += 106;

            // =================================================================
            //  CARD 3: 武装与伤害 (OFFENSE)
            // =================================================================
            Panel cardAtk = MakeGothicCard("角色攻击力", "OFFENSE · 仅对敌人生效", pad, y, cardW, 134);

            _lblAtkInfo = new Label { Text = "倍率: 1.0x  ·  等待进入游戏存档读取武器攻击表...", Location = new Point(S(14), S(38)), Size = new Size(S(cardW - 28), S(19)), ForeColor = Theme.TextSecondary };

            Label lMult = new Label { Text = "倍率", Location = new Point(S(14), S(68)), AutoSize = true, ForeColor = Theme.TextSecondary };
            _stAtkMult = MakeStepper(48, 64, 86, 26, 0.1m, 100.0m, 1.0m, 1, 0.5m);
            _stAtkMult.ValueCommitted += delegate { ApplyMultiplier((double)_stAtkMult.Value); };
            GothicButton bApplyMult = BtnCrimson("应用倍率", 140, 64, 72, 26);
            bApplyMult.Click += delegate { ApplyMultiplier((double)_stAtkMult.Value); };

            double[] presets = new double[] { 1.0, 2.0, 3.0, 5.0, 10.0 };
            int px = 222;
            for (int i = 0; i < presets.Length; i++)
            {
                double val = presets[i];
                string txt = val == 1.0 ? "原版 1x" : (val.ToString("0") + "x");
                int bw = val == 1.0 ? 64 : 50;
                GothicButton bp = BtnStone(txt, px, 64, bw, 26);
                bp.Click += delegate { _stAtkMult.Value = (decimal)val; ApplyMultiplier(val); };
                cardAtk.Controls.Add(bp);
                px += bw + 6;
            }

            _swOverwriteDmg = new GothicSwitch("固定伤害覆盖", Theme.VioletMain, S(130), S(22));
            _swOverwriteDmg.Location = new Point(S(14), S(100));
            _swOverwriteDmg.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantOverwriteDmg = _swOverwriteDmg.Checked;
                _wantOverwriteDmgVal = (int)_stOverwriteDmg.Value;
                _mem.SetOverwriteDamage(_wantOverwriteDmg, _wantOverwriteDmgVal);
                SetFooter(_wantOverwriteDmg ? "已开启固定伤害覆盖 = " + _wantOverwriteDmgVal : "已关闭固定伤害覆盖");
            };

            _stOverwriteDmg = MakeStepper(148, 98, 98, 26, 1, 9999999, 999, 0, 100);
            _stOverwriteDmg.ValueCommitted += delegate { ApplyOverwriteDamage(); };
            GothicButton bWriteOver = BtnStone("写入", 252, 98, 52, 26);
            bWriteOver.Click += delegate { ApplyOverwriteDamage(); };

            _swOneHitKill = new GothicSwitch("一击必杀 · 秒杀敌人 [F5]", Theme.CrimsonMain, S(210), S(22));
            _swOneHitKill.Location = new Point(S(330), S(100));
            _swOneHitKill.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantOneHitKill = _swOneHitKill.Checked;
                _mem.SetCriticalDamage(_wantOneHitKill);
                SetFooter(_wantOneHitKill ? "已开启一击必杀 (仅对敌人)" : "已关闭一击必杀");
            };

            cardAtk.Controls.AddRange(new Control[]
            {
                _lblAtkInfo, lMult, _stAtkMult, bApplyMult, _swOverwriteDmg, _stOverwriteDmg, bWriteOver, _swOneHitKill
            });
            y += 144;

            // =================================================================
            //  CARD 4: 圣遗物、等级与圣堂庇护 (RELICS, LEVELS & BLESSINGS)
            // =================================================================
            Panel cardMisc = MakeGothicCard("圣遗物、强化等级与庇护", "RELICS & SANCTUM BLESSINGS", pad, y, cardW, 166);

            _lblRelicsVal = new Label { Text = "Relics:  --", Location = new Point(S(14), S(42)), Size = new Size(S(136), S(20)), ForeColor = Theme.GoldAntique, Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold) };
            _stRelics = MakeStepper(154, 38, 106, 26, 0, 99999999, 99999, 0, 1000);
            _stRelics.ValueCommitted += delegate { int v = (int)_stRelics.Value; if (_mem.SetRelics(v)) SetFooter("已设置 Relics = " + v); };

            GothicButton bSetRel = BtnViolet("设置", 266, 38, 54, 26);
            bSetRel.Click += delegate { int v = (int)_stRelics.Value; if (_mem.SetRelics(v)) SetFooter("已设置 Relics = " + v); };

            GothicButton bAdd1k = BtnStone("+1,000", 326, 38, 66, 26);
            bAdd1k.Click += delegate { GameState st = _mem.ReadState(); if (st.InGame && _mem.SetRelics(st.Relics + 1000)) SetFooter("Relics +1,000"); };

            GothicButton bAdd10k = BtnStone("+10,000", 398, 38, 72, 26);
            bAdd10k.Click += delegate { GameState st = _mem.ReadState(); if (st.InGame && _mem.SetRelics(st.Relics + 10000)) SetFooter("Relics +10,000"); };

            GothicButton bMaxRel = BtnStone("99999", 476, 38, 66, 26);
            bMaxRel.Click += delegate { if (_mem.SetRelics(99999)) SetFooter("Relics = 99,999"); };

            // Levels row
            Label lAtkLv = new Label { Text = "攻击 Lv", Location = new Point(S(14), S(78)), AutoSize = true, ForeColor = Theme.TextSecondary };
            _stAtkLv = MakeStepper(66, 74, 72, 26, 1, 99, 1, 0, 1);
            Label lHpLv = new Label { Text = "血量 Lv", Location = new Point(S(152), S(78)), AutoSize = true, ForeColor = Theme.TextSecondary };
            _stHpLv = MakeStepper(204, 74, 72, 26, 1, 99, 1, 0, 1);
            Label lMpLv = new Label { Text = "魔力 Lv", Location = new Point(S(290), S(78)), AutoSize = true, ForeColor = Theme.TextSecondary };
            _stMpLv = MakeStepper(342, 74, 72, 26, 1, 99, 1, 0, 1);

            GothicButton bApplyLv = BtnStone("写入等级", 428, 74, 114, 26);
            bApplyLv.Click += delegate
            {
                if (_mem.SetStatLevels((int)_stAtkLv.Value, (int)_stHpLv.Value, (int)_stMpLv.Value))
                    SetFooter(string.Format("已写入强化等级: 攻Lv{0} / 血Lv{1} / 魔Lv{2}", (int)_stAtkLv.Value, (int)_stHpLv.Value, (int)_stMpLv.Value));
            };

            // Hairline divider inside card
            Panel divLine = new Panel { Location = new Point(S(14), S(111)), Size = new Size(S(cardW - 28), 1), BackColor = Theme.BorderSubtle };

            // 4 Blessing Switches
            _swDisableHold = new GothicSwitch("防抓取 / 免束缚", Theme.VioletMain, S(136), S(22));
            _swDisableHold.Location = new Point(S(14), S(124));
            _swDisableHold.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantDisableHold = _swDisableHold.Checked;
                _mem.SetDisableHold(_wantDisableHold);
                SetFooter(_wantDisableHold ? "已开启防抓取 / 免束缚" : "已关闭防抓取");
            };

            _swSuperTough = new GothicSwitch("霸体 / 免硬直", Theme.VioletMain, S(128), S(22));
            _swSuperTough.Location = new Point(S(162), S(124));
            _swSuperTough.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantSuperTough = _swSuperTough.Checked;
                _mem.SetSuperTough(_wantSuperTough);
                SetFooter(_wantSuperTough ? "已开启霸体 / 免硬直" : "已关闭霸体");
            };

            _swCritPoise = new GothicSwitch("必定破韧", Theme.VioletMain, S(104), S(22));
            _swCritPoise.Location = new Point(S(302), S(124));
            _swCritPoise.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantCritPoise = _swCritPoise.Checked;
                _mem.SetCriticalPoise(_wantCritPoise);
                SetFooter(_wantCritPoise ? "已开启必定破韧" : "已关闭必定破韧");
            };

            _swLockDurability = new GothicSwitch("锁衣物耐久", Theme.VioletMain, S(116), S(22));
            _swLockDurability.Location = new Point(S(424), S(124));
            _swLockDurability.CheckedChanged += delegate
            {
                if (_suppressEvents) return;
                _wantLockDurability = _swLockDurability.Checked;
                _mem.SetDurabilityDontSub(_wantLockDurability);
                SetFooter(_wantLockDurability ? "已锁定衣物耐久" : "已解除衣物耐久锁定");
            };

            cardMisc.Controls.AddRange(new Control[]
            {
                _lblRelicsVal, _stRelics, bSetRel, bAdd1k, bAdd10k, bMaxRel,
                lAtkLv, _stAtkLv, lHpLv, _stHpLv, lMpLv, _stMpLv, bApplyLv,
                divLine, _swDisableHold, _swSuperTough, _swCritPoise, _swLockDurability
            });
            y += 176;

            // =================================================================
            //  FOOTER ACTION BAR
            // =================================================================
            GothicButton bResetAll = new GothicButton("一键还原全部修改", Theme.CrimsonDark, Theme.CrimsonMain, Theme.CrimsonMain, Theme.TextPrimary, S(138), S(28));
            bResetAll.Location = new Point(S(pad), S(y));
            bResetAll.Click += delegate { ResetAll(); };
            Controls.Add(bResetAll);

            _lblFooter = new Label
            {
                Text = "就绪 — 启动游戏并进入存档后自动反射适配",
                Location = new Point(S(pad + 148), S(y + 4)),
                Size = new Size(S(cardW - 148), S(22)),
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_lblFooter);
        }

        private void PaintBanner(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = _banner.ClientRectangle;

            // Moonlit Cathedral gradient backdrop
            using (LinearGradientBrush bg = new LinearGradientBrush(r, Color.FromArgb(30, 22, 46), Color.FromArgb(16, 13, 24), 18f))
                g.FillRectangle(bg, r);

            // Subtle diagonal violet moonlight shaft
            Point[] beam = new Point[] { new Point(S(340), 0), new Point(S(460), 0), new Point(S(290), r.Height), new Point(S(190), r.Height) };
            using (SolidBrush bbr = new SolidBrush(Color.FromArgb(16, 168, 136, 232)))
                g.FillPolygon(bbr, beam);

            // Tiny decorative rose petal motes (subtle & refined)
            using (SolidBrush petal = new SolidBrush(Color.FromArgb(90, 196, 62, 94)))
            {
                g.FillEllipse(petal, S(245), S(16), S(5), S(3));
                g.FillEllipse(petal, S(312), S(40), S(4), S(2));
                g.FillEllipse(petal, S(548), S(14), S(5), S(3));
            }

            // Gothic cross crest on left
            int cx = S(28), cy = S(30);
            using (Pen crossGlow = new Pen(Color.FromArgb(80, Theme.VioletMain), Math.Max(2, S(2))))
            using (SolidBrush ivory = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush gem = new SolidBrush(Theme.CrimsonMain))
            {
                g.DrawEllipse(crossGlow, cx - S(15), cy - S(15), S(30), S(30));
                int w = Math.Max(2, S(3));
                g.FillRectangle(ivory, cx - w / 2, cy - S(10), w, S(20));
                g.FillRectangle(ivory, cx - S(7), cy - S(3), S(14), w);
                Point[] d = new Point[] { new Point(cx, cy - S(4)), new Point(cx + S(4), cy), new Point(cx, cy + S(4)), new Point(cx - S(4), cy) };
                g.FillPolygon(gem, d);
            }

            // Title & subtitle
            using (Font fTitle = new Font("Georgia", 13.5f, FontStyle.Bold))
            using (Font fSub = new Font("Microsoft YaHei UI", 8.0f))
            {
                TextRenderer.DrawText(g, "SiNiSistar 2", fTitle, new Point(S(50), S(11)), Theme.TextPrimary);
                TextRenderer.DrawText(g, _subStatusText, fSub, new Point(S(52), S(36)), Theme.TextSecondary);
            }

            // Live status pill badge on top-right
            using (Font fBadge = new Font("Microsoft YaHei UI", 8.4f, FontStyle.Bold))
            {
                Size sz = TextRenderer.MeasureText(_statusBadgeText, fBadge);
                int pw = sz.Width + S(18);
                int ph = S(24);
                int px = r.Width - pw - S(16);
                int py = S(16);
                Rectangle pr = new Rectangle(px, py, pw, ph);
                using (GraphicsPath path = Theme.RoundedRect(pr, ph / 2))
                using (SolidBrush pbg = new SolidBrush(Color.FromArgb(24, 20, 35)))
                using (Pen pb = new Pen(Color.FromArgb(140, _statusBadgeColor), 1))
                {
                    g.FillPath(pbg, path);
                    g.DrawPath(pb, path);
                }
                TextRenderer.DrawText(g, _statusBadgeText, fBadge, pr, _statusBadgeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Bottom hairline separator
            using (Pen bp = new Pen(Theme.BorderSubtle, 1))
                g.DrawLine(bp, 0, r.Height - 1, r.Width, r.Height - 1);
            using (Pen accent = new Pen(Color.FromArgb(110, Theme.CrimsonMain), 1))
                g.DrawLine(accent, S(16), r.Height - 1, S(160), r.Height - 1);
        }

        private void SetFooter(string msg)
        {
            _lblFooter.Text = DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
        }

        private void ApplyCustomMaxHp()
        {
            int v = (int)_stHpMax.Value;
            _customMaxHp = v;
            if (_mem.SetHpMax(v, true)) SetFooter("已将血量上限改为 " + v + " 并回满 (切图自动保活)");
            else SetFooter("已记录目标血量上限 " + v + "，进入游戏后生效");
        }

        private void ApplyCustomMaxMp()
        {
            int v = (int)_stMpMax.Value;
            _customMaxMp = v;
            if (_mem.SetMpMax(v, true)) SetFooter("已将蓝量上限改为 " + v + " 并回满");
            else SetFooter("已记录目标蓝量上限 " + v + "，进入游戏后生效");
        }

        private void ApplyMultiplier(double mult)
        {
            _currentAtkMult = mult;
            if (_mem.ApplyAttackMultiplier(mult))
                SetFooter(string.Format("已将全部武器/魔法攻击力调整为 {0:0.0}x", mult));
            else
                SetFooter("已记录攻击倍率 " + mult.ToString("0.0") + "x，进入游戏后自动生效");
        }

        private void ApplyOverwriteDamage()
        {
            _wantOverwriteDmgVal = (int)_stOverwriteDmg.Value;
            if (!_swOverwriteDmg.Checked) _swOverwriteDmg.Checked = true;
            else
            {
                _mem.SetOverwriteDamage(true, _wantOverwriteDmgVal);
                SetFooter("固定伤害已更新为 " + _wantOverwriteDmgVal);
            }
        }

        private void DoPurifyDefilement(bool purifyAll)
        {
            GameState st = _mem.ReadState();
            if (!st.InGame) { SetFooter("请先进入游戏存档后再操作污秽值"); return; }
            if (st.DefilementLevel <= 0) { SetFooter("当前污秽值已是 0 (纯净状态)"); return; }
            if (_mem.DecreaseDefilement(purifyAll))
                SetFooter(purifyAll ? "已触发原生净化，污秽将在下一帧归零！" : "已触发原生减污秽，下一帧 -1！");
            else
                SetFooter("未能定位污秽状态对象");
        }

        private void ResetAll()
        {
            _suppressEvents = true;
            _wantLockHp = _wantLockMp = _wantLockDefilement = _wantOneHitKill = _wantOverwriteDmg = false;
            _wantDisableHold = _wantSuperTough = _wantCritPoise = _wantLockDurability = false;
            _currentAtkMult = 1.0;
            _customMaxHp = null;
            _customMaxMp = null;
            _swLockHp.Checked = _swLockMp.Checked = _swLockDef.Checked = false;
            _swOneHitKill.Checked = _swOverwriteDmg.Checked = false;
            _swDisableHold.Checked = _swSuperTough.Checked = _swCritPoise.Checked = _swLockDurability.Checked = false;
            _stAtkMult.Value = 1.0m;
            _suppressEvents = false;
            _mem.ResetAllNativeFlags();
            SetFooter("已还原所有开关、攻击力倍率及血/蓝上限！");
        }

        private void ShowDiagnosticsDialog()
        {
            Form dlg = new Form
            {
                Text = "IL2CPP 自动反射诊断",
                Size = new Size(S(620), S(420)),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.BgAbyss,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Consolas", 9f),
                MinimizeBox = false
            };
            try { int v = 1; Native.DwmSetWindowAttribute(dlg.Handle, 20, ref v, sizeof(int)); } catch { }
            TextBox tb = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                BackColor = Theme.CardBg,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Text = _mem.GetDiagnosticsText()
            };
            dlg.Controls.Add(tb);
            dlg.ShowDialog(this);
        }

        private void LaunchSteamGame()
        {
            string localExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SiNiSistar2.exe");
            try
            {
                Process.Start(new ProcessStartInfo("steam://rungameid/4466620") { UseShellExecute = true });
                SetFooter("正在通过 Steam 启动 SiNiSistar 2...");
            }
            catch
            {
                if (File.Exists(localExe)) Process.Start(localExe);
                else SetFooter("无法唤起 Steam，请手动启动游戏");
            }
        }

        private static string FindModDirectoryAuto(string runningExePath)
        {
            // 1. From running game process
            if (!string.IsNullOrEmpty(runningExePath))
            {
                string d = Path.Combine(Path.GetDirectoryName(runningExePath), "Mod", "Player");
                if (Directory.Exists(d)) return d;
                string dRoot = Path.Combine(Path.GetDirectoryName(runningExePath), "Mod");
                if (Directory.Exists(dRoot)) return dRoot;
            }
            // 2. Next to trainer exe
            string localMod = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mod", "Player");
            if (Directory.Exists(localMod)) return localMod;

            // 3. Auto-discover via Steam Registry + libraryfolders.vdf (zero hardcoded user paths)
            try
            {
                string steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                if (string.IsNullOrEmpty(steamPath))
                    steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
                if (!string.IsNullOrEmpty(steamPath))
                {
                    List<string> libs = new List<string>();
                    libs.Add(steamPath.Replace('/', '\\'));
                    string vdf = Path.Combine(libs[0], "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        string text = File.ReadAllText(vdf);
                        foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
                            libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
                    }
                    for (int i = 0; i < libs.Count; i++)
                    {
                        string cand = Path.Combine(libs[i], "steamapps", "common", "SiNiSistar 2", "Mod", "Player");
                        if (Directory.Exists(cand)) return cand;
                    }
                }
            }
            catch { }
            return null;
        }

        private void OpenModFolder()
        {
            string modDir = FindModDirectoryAuto(_mem.ExePath);
            if (!string.IsNullOrEmpty(modDir))
            {
                Process.Start("explorer.exe", modDir);
                SetFooter("已打开 Mod 目录: " + modDir);
            }
            else
            {
                SetFooter("未检测到 Mod\\Player 目录，请先启动游戏或将修改器放入游戏目录");
            }
        }

        private void UpdateBannerStatus(string badgeText, Color badgeColor, string subText)
        {
            if (_statusBadgeText != badgeText || _statusBadgeColor != badgeColor || _subStatusText != subText)
            {
                _statusBadgeText = badgeText;
                _statusBadgeColor = badgeColor;
                _subStatusText = subText;
                _banner.Invalidate();
            }
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            GameState st = _mem.ReadState();
            if (!st.Connected)
            {
                _wasInGame = false;
                if (st.AccessDenied)
                    UpdateBannerStatus("▲ 需管理员权限", Theme.CrimsonSoft, "检测到游戏进程但权限不足 · 请右键以管理员身份运行");
                else
                    UpdateBannerStatus("○ 等待游戏启动", Theme.GoldAntique, "通用 IL2CPP 自动适配 · 支持 Steam 正版与各版本");

                _lblHpVal.Text = "HP   -- / --";
                _lblMpVal.Text = "MP   -- / --";
                _lblDefVal.Text = "当前污秽度:  -- / 10";
                _lblRelicsVal.Text = "Relics:  --";
                _barHp.Value = 0; _barMp.Value = 0; _barDef.Value = 0;
                _barHp.Invalidate(); _barMp.Invalidate(); _barDef.Invalidate();
                return;
            }

            if (!st.Resolved)
            {
                _wasInGame = false;
                string badge = st.Scanning ? "◌ 自动扫描基址中..." : "◌ 等待引擎初始化...";
                UpdateBannerStatus(badge, Theme.VioletSoft, "PID " + st.Pid + " · " + st.GameVersion + " · 正在通过 IL2CPP 反射定位类结构");
                return;
            }

            if (!st.InGame)
            {
                _wasInGame = false;
                UpdateBannerStatus("● 已适配 " + st.GameVersion, Theme.VioletSoft, "等待进入存档 · " + st.ResolveSummary);
                return;
            }

            UpdateBannerStatus("● 运行中 " + st.GameVersion, Color.FromArgb(146, 214, 178),
                string.Format("PID {0} · {1}", st.Pid, st.ResolveSummary));

            // Keep sticky switches alive across scene reloads
            if (_wantLockHp && !st.HpDontSub) _mem.SetHpDontSub(true);
            if (_wantLockMp && !st.MpDontSub) _mem.SetMpDontSub(true);
            if (_wantLockDurability && !st.DurabilityDontSub) _mem.SetDurabilityDontSub(true);
            if (_wantLockDefilement && !st.DefilementLocked) _mem.SetLockDefilement(true);
            if (_wantOneHitKill && !st.CriticalDamage) _mem.SetCriticalDamage(true);
            if (_wantCritPoise && !st.CriticalPoise) _mem.SetCriticalPoise(true);
            if (_wantOverwriteDmg && (!st.IsOverwriteDamage || st.OverwriteDamageValue != _wantOverwriteDmgVal))
                _mem.SetOverwriteDamage(true, _wantOverwriteDmgVal);
            if (_wantDisableHold && !st.IsDisableHold) _mem.SetDisableHold(true);
            if (_wantSuperTough && !st.IsPlayerSuperTough) _mem.SetSuperTough(true);

            if (!_wasInGame)
            {
                _wasInGame = true;
                if (Math.Abs(_currentAtkMult - 1.0) > 0.01) _mem.ApplyAttackMultiplier(_currentAtkMult);
                if (_customMaxHp.HasValue) _mem.SetHpMax(_customMaxHp.Value, false);
                if (_customMaxMp.HasValue) _mem.SetMpMax(_customMaxMp.Value, false);

                if (!_stHpCur.IsEditing) _stHpCur.Value = st.HpCurrent;
                if (!_stHpMax.IsEditing) _stHpMax.Value = st.HpMax;
                if (!_stMpCur.IsEditing) _stMpCur.Value = st.MpCurrent;
                if (!_stMpMax.IsEditing) _stMpMax.Value = st.MpMax;
                if (!_stRelics.IsEditing) _stRelics.Value = st.Relics;
                if (!_stAtkLv.IsEditing) _stAtkLv.Value = Math.Max(1, st.AttackLv);
                if (!_stHpLv.IsEditing) _stHpLv.Value = Math.Max(1, st.HpLv);
                if (!_stMpLv.IsEditing) _stMpLv.Value = Math.Max(1, st.MpLv);
            }
            else
            {
                if (_customMaxHp.HasValue && st.HpMax != _customMaxHp.Value)
                    _mem.SetHpMax(_customMaxHp.Value, false);
                if (_customMaxMp.HasValue && st.MpMax != _customMaxMp.Value)
                    _mem.SetMpMax(_customMaxMp.Value, false);
            }

            // Refresh displays
            _lblHpVal.Text = string.Format("HP   {0} / {1}", st.HpCurrent, st.HpMax);
            _barHp.Maximum = Math.Max(1, st.HpMax);
            _barHp.Value = Math.Max(0, st.HpCurrent);
            _barHp.Invalidate();

            _lblMpVal.Text = string.Format("MP   {0} / {1}", st.MpCurrent, st.MpMax);
            _barMp.Maximum = Math.Max(1, st.MpMax);
            _barMp.Value = Math.Max(0, st.MpCurrent);
            _barMp.Invalidate();

            string defDesc = st.DefilementLevel <= 0 ? "纯净" : (st.DefilementLevel >= 8 ? "严重" : "已累积");
            _lblDefVal.Text = string.Format("当前污秽度:  {0} / 10  ({1})", st.DefilementLevel, defDesc);
            _barDef.Value = Math.Max(0, Math.Min(10, st.DefilementLevel));
            _barDef.Invalidate();

            _lblRelicsVal.Text = string.Format("Relics:  {0:N0}", st.Relics);

            if (st.AttackEntryCount > 0)
            {
                _lblAtkInfo.Text = string.Format("当前倍率: {0:0.0}x  ·  已挂载 {1} 项攻击表  ·  基础伤害示例: {2} → {3}",
                    _currentAtkMult, st.AttackEntryCount, st.SampleBaseDamage, st.SampleCurrentDamage);
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrainerForm());
        }
    }
}
