#!/usr/bin/env python3
"""Generate code from the sheets. One row -> one generated struct/entry.
Outputs:
  src/es-plugin/landing-rights/data/landing rights.txt   (Endless Sky missions, one per claims row)
  src/es-engine/LandingRightsTables.h                   (C++ tables for the ES bridge)
  src/dsp-plugin/Tables.g.cs                            (C# tables for the DSP plugin)
"""
import json, os, sys

root = sys.argv[1]
S = {n: json.load(open(os.path.join(root, "sheets", n + ".json"))) for n in ["claims", "worlds", "shipments", "settings"]}
setting = {r["id"]: r["value"] for r in S["settings"]["rows"]}
HDR = "Generated from sheets/*.json by tools/gen.py. Do not edit; change the sheet."

def cstr(s): return json.dumps(s, ensure_ascii=False)

# ---------- Endless Sky data plugin ----------
es = [f"# {HDR}", "# Landing Rights: stake a claim on a planet, then build on it in Dyson Sphere Program.", ""]
for r in S["claims"]["rows"]:
    cond = setting["claimConditionPrefix"] + r["esPlanet"]
    world = next(w for w in S["worlds"]["rows"] if w["id"] == r["world"])
    es += [
        f'mission "Landing Rights: claim {r["esPlanet"]}"',
        "\tlanding", "\tinvisible", "\trepeat",
        f'\tsource "{r["esPlanet"]}"',
        "\tto offer",
        '\t\thas "landing rights: engine"',
        f'\t\tnot "{cond}"',
        "\ton offer",
        "\t\tconversation",
        f"\t\t\t`{r['pitch']}`",
        "\t\t\tchoice",
        f"\t\t\t\t`\t\"I'll take it.\" (Pay {r['price']:,} credits.)`",
        "\t\t\t\t`\t\"Maybe another time.\"`",
        "\t\t\t\t\tdecline",
        "\t\t\tbranch \"can pay\"",
        f'\t\t\t\t"credits" >= {r["price"]}',
        "\t\t\t`\tYou check your account. You don't have enough credits, and the deed goes back into the coat.`",
        "\t\t\t\tdecline",
        "\t\t\tlabel \"can pay\"",
        "\t\t\taction",
        f"\t\t\t\tpayment -{r['price']}",
        f'\t\t\t\tset "{cond}"',
        f"\t\t\t`\tThe deed is yours: {'an' if world['label'][0].lower() in 'aeiou' else 'a'} {world['label'].lower()} plot on {r['esPlanet']}. Your engineering mecha is already heading down to the surface. (Landing Rights: build in Dyson Sphere Program, click \"Ship to Endless Sky\" on a Depot there, fill it, and press {setting['returnKey']} to ship them up to your fleet. Back here, press F at any time to go down to the surface again.)`",
        "\t\t\t\tdecline",
        "",
    ]
p = os.path.join(root, "src/es-plugin/landing-rights/data")
os.makedirs(p, exist_ok=True)
open(os.path.join(p, "landing rights.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(es))
open(os.path.join(root, "src/es-plugin/landing-rights/plugin.txt"), "w", newline="\n").write(
    'name "Landing Rights"\nabout "Stake claims on planets and build factories on them in Dyson Sphere Program. Requires the Landing Rights launcher."\nversion "0.1.1"\n')

# ---------- C++ ----------
h = [f"// {HDR}", "#pragma once", "#include <string>", "", "namespace LandingRightsTables {",
     "struct Claim { const char *id; const char *planet; const char *world; int price; bool birth; };",
     "inline constexpr Claim CLAIMS[] = {"]
for r in S["claims"]["rows"]:
    h.append(f"\t{{{cstr(r['id'])}, {cstr(r['esPlanet'])}, {cstr(r['world'])}, {r['price']}, {str(r['bindToBirthPlanet']).lower()}}},")
h += ["};",
      f"inline constexpr const char *BRIDGE_DIR = {cstr(setting['bridgeDirName'])};",
      f"inline constexpr const char *CLAIM_PREFIX = {cstr(setting['claimConditionPrefix'])};",
      f"inline constexpr double POLL_SECONDS = {float(setting['pollSeconds'])};",
      f"inline constexpr double HEARTBEAT_STALE_SECONDS = {float(setting['heartbeatStaleSeconds'])};",
      f"inline constexpr const char *RETURN_KEY = {cstr(setting['returnKey'])};",
      "}", ""]
p = os.path.join(root, "src/es-engine"); os.makedirs(p, exist_ok=True)
open(os.path.join(p, "LandingRightsTables.h"), "w", newline="\n").write("\n".join(h))

# ---------- C# ----------
c = [f"// {HDR}", "namespace LandingRights {", "internal static class Tables {",
     "\tinternal struct Shipment { public string Id; public int ItemId; public string ItemName; public string Commodity; public int ItemsPerTon; }",
     "\tinternal static readonly Shipment[] Shipments = {"]
for r in S["shipments"]["rows"]:
    c.append(f"\t\tnew Shipment {{ Id = {cstr(r['id'])}, ItemId = {r['dspItemId']}, ItemName = {cstr(r['dspItemName'])}, Commodity = {cstr(r['esCommodity'])}, ItemsPerTon = {r['itemsPerTon']} }},")
c += ["\t};",
      "\tinternal struct World { public string Id; public string Label; public EPlanetType Type; }",
      "\tinternal static readonly World[] Worlds = {"]
for r in S["worlds"]["rows"]:
    c.append(f"\t\tnew World {{ Id = {cstr(r['id'])}, Label = {cstr(r['label'])}, Type = EPlanetType.{r['dspPlanetType']} }},")
c += ["\t};",
      "\tinternal struct Claim { public string Id; public string Planet; public string World; public bool Birth; }",
      "\tinternal static readonly Claim[] Claims = {"]
for r in S["claims"]["rows"]:
    c.append(f"\t\tnew Claim {{ Id = {cstr(r['id'])}, Planet = {cstr(r['esPlanet'])}, World = {cstr(r['world'])}, Birth = {str(r['bindToBirthPlanet']).lower()} }},")
c += ["\t};",
      f"\tinternal const string BridgeDir = {cstr(setting['bridgeDirName'])};",
      f"\tinternal const float PollSeconds = {float(setting['pollSeconds'])}f;",
      f"\tinternal const float HeartbeatSeconds = {float(setting['heartbeatSeconds'])}f;",
      f"\tinternal const string ReturnKey = {cstr(setting['returnKey'])};",
      f"\tinternal const bool MuteInBackground = {str(setting['muteInBackground']).lower()};",
      f"\tinternal const float ArrivalAltitude = {float(setting['arrivalAltitude'])}f;",
      "}", "}", ""]
p = os.path.join(root, "src/dsp-plugin"); os.makedirs(p, exist_ok=True)
open(os.path.join(p, "Tables.g.cs"), "w", newline="\n").write("\n".join(c))
print("generated: ES missions", len(S["claims"]["rows"]), "| shipments", len(S["shipments"]["rows"]), "| worlds", len(S["worlds"]["rows"]))
