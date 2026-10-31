#!/usr/bin/env bash
# Headless test of the Landing Rights Endless Sky build under Wine + Xvfb.
# Needs: the built exe, the player's ES DLLs in $DLLS (test only, never shipped),
# ES resources (data/images/sounds) in $RES.
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$HERE/../..
EXE=${EXE:-$ROOT/out/es/Endless Sky Landing Rights.exe}
DLLS=${DLLS:-/mnt/user-data/uploads/Endless Sky}
RES=${RES:-/home/claude/ext/es-src}
W=${W:-/home/claude/test}
export WINEDEBUG=-all WINEPREFIX=$W/wineprefix

rm -rf "$W/run" && mkdir -p "$W/run/bin" "$W/run/cfg/plugins" "$W/run/cfg/landing-rights"
cp "$DLLS"/*.dll "$W/run/bin/"; cp "$EXE" "$W/run/bin/es.exe"
cp -r "$ROOT/src/es-plugin/landing-rights" "$HERE/landing-rights-tests" "$RES/tests/integration/config/plugins/integration-tests" "$W/run/cfg/plugins/"
cp "$RES/tests/integration/config/preferences.txt" "$W/run/cfg/"
# Pretend DSP is running and has already shipped goods up from the New Boston claim.
printf 'time=9999999999\ninGame=1\nplanet=\n' > "$W/run/cfg/landing-rights/dsp.txt"
printf 'seq=1\nclaim=New Boston\nreturn=1\ncargo.Metal=3\ncargo.Electronics=2\ncargo.Bogus=9\n' > "$W/run/cfg/landing-rights/shipment-1.txt"

winpath() { echo "Z:$(echo "$1" | tr '/' '\\')"; }
cd "$W/run/bin"
set +e
timeout 600 xvfb-run -a --server-args="-screen 0 1280x1024x24 +extension GLX +render -noreset" \
	wine es.exe --resources "$(winpath "$RES")" --config "$(winpath "$W/run/cfg")" --test "Landing Rights claim and ship" > "$W/run/es.log" 2>&1
code=$?
set -e
fail=0
echo "ES exit code: $code"; [[ $code == 0 ]] || fail=1
S="$W/run/cfg/landing-rights/state.txt"
grep -q '^claimed=1$' "$S" && echo "PASS claim condition reached the bridge" || { echo "FAIL state.txt"; cat "$S"; fail=1; }
grep -q '^world=ocean$' "$S" && echo "PASS world type sent" || { echo "FAIL world"; fail=1; }
[[ ! -e "$W/run/cfg/landing-rights/shipment-1.txt" ]] && echo "PASS shipment consumed" || { echo "FAIL shipment not consumed"; fail=1; }
SAVE="$W/run/cfg/saves/Landing Rights New Boston Save.txt"
python3 - "$SAVE" <<'EOF' || fail=1
import re, sys
s = open(sys.argv[1]).read()
credits = int(re.search(r"^account\n\tcredits (\d+)", s, re.M).group(1))
block = re.search(r"^cargo\n\tcommodities\n((?:\t\t.*\n)*)", s, re.M).group(1)
cargo = {m.group(1).strip('"'): int(m.group(2)) for m in re.finditer(r'\t\t("[^"]+"|\S+) (\d+)', block)}
ok = cargo == {"Metal": 3, "Electronics": 2}
print(("PASS" if ok else "FAIL") + f" saved cargo {cargo} (unknown commodity dropped)")
ok2 = credits == 95000
print(("PASS" if ok2 else "FAIL") + f" claim cost 5,000 credits (credits now {credits})")
sys.exit(0 if ok and ok2 else 1)
EOF
exit $fail
