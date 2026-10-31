#!/usr/bin/env bash
# Builds the DSP side: Doorstop proxy (winhttp.dll), preloader and plugin.
# Compiles against the player's own DSP assemblies (reference only; never shipped).
set -euo pipefail
ROOT=/home/claude/landing-rights
M=${M:-"/mnt/user-data/uploads/Dyson Sphere Program/DSPGAME_Data/Managed"}
OUT=$ROOT/out/dsp/LandingRights
CECIL=/usr/lib/mono/gac/Mono.Cecil/0.11.0.0__0738eb9f132ed756/Mono.Cecil.dll
mkdir -p "$OUT"
REFS=(-nostdlib -noconfig -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" -r:"$M/netstandard.dll")

mcs -target:library -optimize+ -out:"$OUT/LandingRights.dll" "${REFS[@]}" \
	-r:"$M/Assembly-CSharp.dll" -r:"$M/UnityEngine.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.InputLegacyModule.dll" -r:"$M/UnityEngine.AudioModule.dll" -r:"$M/UnityEngine.IMGUIModule.dll" -r:"$M/UnityEngine.UIModule.dll" -r:"$M/UnityEngine.TextRenderingModule.dll" -r:"$M/UnityEngine.UI.dll" \
	"$ROOT/src/dsp-plugin/LandingRights.cs" "$ROOT/src/dsp-plugin/Tables.g.cs"

mcs -target:library -optimize+ -out:"$OUT/LandingRights.Preloader.dll" "${REFS[@]}" -r:"$CECIL" \
	"$ROOT/src/dsp-preloader/Entrypoint.cs"
cp "$CECIL" "$OUT/Mono.Cecil.dll"

# Unity Doorstop 4.3.0 (LGPL-2.1), built from source as winhttp.dll
cp /home/claude/ext/doorstop/build/winhttp.dll "$ROOT/out/dsp/winhttp.dll"
cat > "$ROOT/out/dsp/doorstop_config.ini" <<'EOF'
# Unity Doorstop config written by Landing Rights (Endless Sky x Dyson Sphere Program)
[General]
enabled=true
target_assembly=LandingRights\LandingRights.Preloader.dll
redirect_output_log=false
boot_config_override=
ignore_disable_switch=false
[UnityMono]
dll_search_path_override=
debug_enabled=false
debug_address=127.0.0.1:10000
debug_suspend=false
[Il2Cpp]
coreclr_path=
corlib_dir=
EOF
echo "built: $ROOT/out/dsp"; ls -la "$ROOT/out/dsp" "$OUT"
