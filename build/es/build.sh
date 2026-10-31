#!/usr/bin/env bash
# Cross-compiles the Landing Rights build of Endless Sky (GPLv3) for Windows x64.
# Links against the DLLs already in the player's Endless Sky install (import libs
# generated from them); none of the game's files are copied into the output.
set -euo pipefail
EXT=/home/claude/ext
SRC=${SRC:-$EXT/es-src}
SYS=$EXT/sysroot
OUT=${OUT:-/home/claude/landing-rights/out/es}
OBJ=$OUT/obj
mkdir -p "$OBJ"
CXX="python3 -m ziglang c++ -target x86_64-windows-gnu"
CC="python3 -m ziglang cc -target x86_64-windows-gnu"
CXXFLAGS="-std=c++20 -O2 -DNDEBUG -DFLAC__NO_DLL -DUNICODE -D_UNICODE -DSDL_MAIN_HANDLED \
 -I$SYS/include -I$SYS/include/SDL2 -I$SRC/source -Wno-everything"

# Engine sources (no tests) + FLAC++ C++ wrapper (BSD, built statically against libFLAC.dll).
mapfile -t SRCS < <(find "$SRC/source" -name '*.cpp')
SRCS+=("$EXT/flac/src/libFLAC++/metadata.cpp" "$EXT/flac/src/libFLAC++/stream_decoder.cpp" "$EXT/flac/src/libFLAC++/stream_encoder.cpp")

compile() {
	local f=$1 o
	o="$OBJ/$(echo "${f#$EXT/}" | tr '/' '_').o"
	if [[ ! -f $o || $f -nt $o ]]; then
		$CXX $CXXFLAGS -DHAVE_FSEEKO -I"$EXT/flac/include" -c "$f" -o "$o" || { echo "FAILED: $f"; return 1; }
	fi
}
export -f compile; export CXX CXXFLAGS OBJ EXT
printf '%s\n' "${SRCS[@]}" | xargs -P "$(nproc)" -I{} bash -c 'compile "$@"' _ {}

# Windows resource (icon/version), optional
RES=""
if command -v x86_64-w64-mingw32-windres >/dev/null; then
	x86_64-w64-mingw32-windres -I"$SRC" -I"$SRC/source/windows" "$SRC/source/windows/WinApp.rc" -O coff -o "$OBJ/winapp.res.o" 2>/dev/null && RES="$OBJ/winapp.res.o" || true
fi

$CXX -o "$OUT/Endless Sky Landing Rights.exe" "$OBJ"/*.o $RES -Wl,--subsystem,windows \
	"$SYS/lib/libSDL2.dll.a" "$SYS/lib/liblibpng16.dll.a" "$SYS/lib/liblibjpeg-62.dll.a" "$SYS/lib/liblibavif.dll.a" \
	"$SYS/lib/liblibFLAC.dll.a" "$SYS/lib/libglew32.dll.a" "$SYS/lib/libOpenAL32.dll.a" "$SYS/lib/liblibminizip.dll.a" "$SYS/lib/libmad.a" \
	-lopengl32 -lrpcrt4 -lwinmm -lntdll -lpsapi -lshell32 -ladvapi32 -lole32 -luser32
echo "built: $OUT/Endless Sky Landing Rights.exe"
