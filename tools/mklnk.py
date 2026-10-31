#!/usr/bin/env python3
"""Write a minimal Windows shortcut (.lnk, MS-SHLLINK) that points at a local file by path.
Uses LinkInfo/LocalBasePath (no shell item ID list), which Windows resolves reliably.
Usage: mklnk.py out.lnk "C:\\path\\target.exe" "C:\\working\\dir" "description"
"""
import struct, sys

out, target, workdir, desc = sys.argv[1:5]

def ustr(s):  # StringData: count of UTF-16 chars + chars
    return struct.pack("<H", len(s)) + s.encode("utf-16-le")

HasLinkInfo, HasName, HasWorkingDir, HasIconLocation, IsUnicode = 0x2, 0x4, 0x10, 0x40, 0x80
flags = HasLinkInfo | HasName | HasWorkingDir | HasIconLocation | IsUnicode
clsid = bytes.fromhex("0114020000000000c000000000000046")
header = struct.pack("<I16sII", 0x4C, clsid, flags, 0x20)  # FILE_ATTRIBUTE_ARCHIVE
header += b"\0" * 24  # creation/access/write times
header += struct.pack("<IiIH", 0, 0, 1, 0)  # file size, icon index, SW_SHOWNORMAL, hotkey
header += b"\0" * 10  # reserved
assert len(header) == 0x4C

volume = struct.pack("<IIII", 16 + 1, 3, 0, 16) + b"\0"  # size, DRIVE_FIXED, serial, label offset, empty label
base = target.encode("mbcs" if sys.platform == "win32" else "latin-1") + b"\0"
li_header = 28
vol_off = li_header
base_off = vol_off + len(volume)
suffix_off = base_off + len(base)
li_size = suffix_off + 1
linkinfo = struct.pack("<IIIIIII", li_size, li_header, 0x1, vol_off, base_off, 0, suffix_off) + volume + base + b"\0"

data = header + linkinfo + ustr(desc) + ustr(workdir) + ustr(target) + struct.pack("<I", 0)
open(out, "wb").write(data)
print("wrote", out, len(data), "bytes")
