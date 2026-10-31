#!/usr/bin/env python3
"""Preflight: lay every sheet's rows and columns over each other and list every
unfilled cell and every cross-sheet reference that doesn't resolve.

Usage: preflight.py <sheets dir> <ES data dir> <DSP Managed dir> <ES source dir>
Reads the player's own game files only to check names; copies nothing.
Exit code 0 = clean.
"""
import json, os, re, subprocess, sys

sheets_dir, es_data, dsp_managed, es_src = sys.argv[1:5]
problems, checks, runtime = [], 0, []

def load(name):
    with open(os.path.join(sheets_dir, name + ".json")) as f:
        return json.load(f)

S = {n: load(n) for n in ["claims", "worlds", "shipments", "hooks", "bridge", "settings"]}

# 1. every cell filled
for name, sheet in S.items():
    for i, row in enumerate(sheet["rows"]):
        for col in sheet["columns"]:
            checks += 1
            v = row.get(col)
            if v is None or v == "" or v == []:
                problems.append(f"{name}[{row.get('id', i)}].{col}: empty")
        extra = set(row) - set(sheet["columns"])
        if extra:
            problems.append(f"{name}[{row.get('id', i)}]: columns not in header: {sorted(extra)}")
    ids = [r["id"] for r in sheet["rows"]]
    if len(ids) != len(set(ids)):
        problems.append(f"{name}: duplicate ids")

# 2. game data: Endless Sky planets & commodities (player's copy)
planets_txt = open(os.path.join(es_data, "map planets.txt"), encoding="utf-8").read()
blocks = {}
for b in re.split(r"\n(?=planet )", planets_txt):
    m = re.match(r'planet "?([^"\n]+?)"?\s*$', b.split("\n", 1)[0])
    if m:
        blocks[m.group(1)] = b
commodities = set(re.findall(r'^\tcommodity "([^"]+)"', open(os.path.join(es_data, "commodities.txt"), encoding="utf-8").read(), re.M))

world_ids = {r["id"] for r in S["worlds"]["rows"]}
for r in S["claims"]["rows"]:
    checks += 4
    b = blocks.get(r["esPlanet"])
    if b is None:
        problems.append(f"claims[{r['id']}].esPlanet: '{r['esPlanet']}' not in player's map planets.txt"); continue
    if f"\n\tlandscape {r['esLandscape']}" not in b:
        problems.append(f"claims[{r['id']}].esLandscape: doesn't match player's data")
    if f"\n\tattributes {r['esAttributes']}" not in b:
        problems.append(f"claims[{r['id']}].esAttributes: doesn't match player's data")
    if "\n\tspaceport" not in b:
        problems.append(f"claims[{r['id']}]: planet has no spaceport, mission can't be offered")
    if r["world"] not in world_ids:
        problems.append(f"claims[{r['id']}].world: '{r['world']}' not a worlds id")
    # world column should agree with the planet's own landscape/attributes
    w = next((x for x in S["worlds"]["rows"] if x["id"] == r["world"]), None)
    if w and not (any(k in r["esLandscape"] for k in w["esLandscapeKeywords"]) or any(k in r["esAttributes"] for k in w["esAttributeKeywords"])):
        problems.append(f"claims[{r['id']}].world: '{r['world']}' doesn't fit landscape {r['esLandscape']}")
    if not isinstance(r["price"], int) or r["price"] <= 0:
        problems.append(f"claims[{r['id']}].price: must be positive int")
if sum(1 for r in S["claims"]["rows"] if r["bindToBirthPlanet"] is True) != 1:
    problems.append("claims: exactly one row must have bindToBirthPlanet true")

for r in S["shipments"]["rows"]:
    checks += 2
    if r["esCommodity"] not in commodities:
        problems.append(f"shipments[{r['id']}].esCommodity: '{r['esCommodity']}' not in player's commodities.txt")
    if not isinstance(r["itemsPerTon"], int) or r["itemsPerTon"] <= 0:
        problems.append(f"shipments[{r['id']}].itemsPerTon: must be positive int")
    runtime.append(f"shipments[{r['id']}]: DSP item {r['dspItemId']} == '{r['dspItemName']}' (checked by plugin on load, logged)")

# 3. DSP enum names from the player's Assembly-CSharp (metadata only)
dump = os.path.join(os.path.dirname(__file__), "dump", "dump.exe")
if not os.path.exists(dump):  # build the metadata reader on first use (Mono + Mono.Cecil)
    cecil = "/usr/lib/mono/gac/Mono.Cecil/0.11.0.0__0738eb9f132ed756/Mono.Cecil.dll"
    subprocess.run(["mcs", "-r:" + cecil, os.path.join(os.path.dirname(dump), "Dump.cs"), "-out:" + dump], check=True, capture_output=True)
    import shutil; shutil.copy(cecil, os.path.dirname(dump))
out = subprocess.run(["mono", dump, os.path.join(dsp_managed, "Assembly-CSharp.dll"), "EPlanetType"], capture_output=True, text=True).stdout
ptypes = set(re.findall(r"F static EPlanetType (\w+)", out))
for r in S["worlds"]["rows"]:
    checks += 1
    if r["dspPlanetType"] not in ptypes:
        problems.append(f"worlds[{r['id']}].dspPlanetType: '{r['dspPlanetType']}' not in DSP EPlanetType {sorted(ptypes)}")

# 4. hooks resolve: bridge file ids, and the ES functions exist in source
bridge_ids = {r["id"] for r in S["bridge"]["rows"]}
for r in S["hooks"]["rows"]:
    checks += 2
    if r["bridgeFile"] not in bridge_ids:
        problems.append(f"hooks[{r['id']}].bridgeFile: '{r['bridgeFile']}' not a bridge id")
    if r["game"] == "endless-sky":
        m = re.match(r"((?:\w+::)?\w+) \((source/[\w/]+\.cpp)\)", r["where"])
        if not m:
            problems.append(f"hooks[{r['id']}].where: unparseable"); continue
        src = open(os.path.join(es_src, m.group(2)), encoding="utf-8").read()
        if not re.search(r"^\w[\w :&*<>]*\b" + re.escape(m.group(1)) + r"\(", src, re.M):
            problems.append(f"hooks[{r['id']}].where: {m.group(1)} not defined in {m.group(2)}")
    elif r["game"] != "dyson-sphere-program":
        problems.append(f"hooks[{r['id']}].game: unknown game")

# 5. DSP key name valid
key = next(r for r in S["settings"]["rows"] if r["id"] == "returnKey")["value"]
core = os.path.join(dsp_managed, "UnityEngine.CoreModule.dll")
out = subprocess.run(["mono", dump, os.path.join(dsp_managed, "UnityEngine.InputLegacyModule.dll"), "UnityEngine.KeyCode"], capture_output=True, text=True).stdout
if f"F static KeyCode {key}\n" not in out + "\n":
    out2 = subprocess.run(["mono", dump, core, "UnityEngine.KeyCode"], capture_output=True, text=True).stdout
    if f"F static KeyCode {key}" not in out2:
        problems.append(f"settings[returnKey]: '{key}' not a UnityEngine.KeyCode")
checks += 1

print(f"preflight: {checks} checks, {len(problems)} problems")
for p in problems:
    print("  FAIL", p)
for r in runtime:
    print("  RUNTIME", r)
sys.exit(1 if problems else 0)
