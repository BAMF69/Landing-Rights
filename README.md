# Landing Rights: Endless Sky × Dyson Sphere Program

You fly, trade and fight in **Endless Sky**. You can buy a land claim on a planet. When you land there, **Dyson Sphere Program** comes to the front, and your mecha is standing on a matching planet in your own DSP galaxy. Build a factory, open a Depot, click **Ship to Endless Sky** above its window to make it a spaceport, fill it with ingots or parts, then press **F8**. Only spaceport depots ship, so everything else stays put. The goods are lifted up to your Endless Sky fleet as cargo you can sell or haul.

Status: **0.1.0, first playable build.** Played on Windows with both games running side by side. Tested so far: buying claims on New Boston and New Greenland, being sent to a matching DSP planet (an ice world for New Greenland), F to go back down, F8 to return, and 20 Iron Ingots from a Depot arriving as 1 ton of Metal in the Endless Sky hold.

## What players need

* Endless Sky 0.11.2 (Steam, free) and Dyson Sphere Program (Steam), both installed.
* Nothing else to install by hand. Everything the mashup needs is in the release.

## How it plays

| Claim (Endless Sky) | Price | Becomes in DSP |
|---|---|---|
| New Boston (Rutilicus) | 5,000 | your DSP starting planet |
| New Greenland | 25,000 | nearest Ice planet |
| Gemstone | 40,000 | nearest Volcanic planet |

The deed is offered at that planet's spaceport when you land. While you're landed on a claim, press **F** on the Endless Sky planet screen to go back down to Dyson Sphere Program. DSP mutes itself while it's in the background (set `muteInBackground=false` in `Dyson Sphere Program/LandingRights/LandingRights.cfg` to turn that off). Items ship in whole tons from spaceport depots only. Partial tons stay in the depot.

| ES commodity | DSP items (items per ton) |
|---|---|
| Food | Plant Fuel (20) |
| Clothing | Organic Crystal (5) |
| Metal | Iron Ingot (20), Copper Ingot (20) |
| Plastic | Plastic (10) |
| Equipment | Gear (10), Electromagnetic Turbine (4), Thruster (2) |
| Medical | Sulfuric Acid (10) |
| Industrial | Stone Brick (40), Glass (20), Electric Motor (5) |
| Electronics | High-Purity Silicon (10), Magnetic Coil (10), Circuit Board (5), Microcrystalline Component (4), Processor (2) |
| Heavy Metals | Steel (10), Titanium Ingot (10), Titanium Alloy (4) |
| Luxury Goods | Prism (10), Diamond (5), Casimir Crystal (2) |

## How it works

* **Endless Sky (host):** `Endless Sky Landing Rights.exe` is Endless Sky 0.11.2 built from its GPLv3 source with four small hooks: start, land, take off, and a per-frame step (see `build/es/patch.py`). It sits in the player's Endless Sky folder and uses that copy's own data, images, sounds and DLLs. Your normal pilots and saves work with it. The claim missions are a normal ES data plugin (`plugins/landing-rights`).
* **Dyson Sphere Program (companion):** Unity Doorstop (`winhttp.dll`) loads `LandingRights.Preloader.dll`. The preloader adds one call into an in-memory copy of DSP's scripts and changes no game files. That call starts `LandingRights.dll`, a MonoBehaviour that reads Endless Sky's landings, moves the mecha to the planet tied to the claim, and ships goods out of the depots.
* **Between them:** small text files in `%APPDATA%\endless-sky\landing-rights\` (see `sheets/bridge.json`). When ES starts, it starts DSP through Steam if DSP isn't already running. It never closes or restarts anything.

Nothing from either game is shipped. Each player's own installed copies are used.

## Installing (by hand)

1. Unzip `LandingRights-0.1.0-endless-sky.zip` into your Endless Sky folder (Steam: right-click Endless Sky > Manage > Browse local files).
2. Unzip `LandingRights-0.1.0-dyson-sphere-program.zip` into your Dyson Sphere Program folder.
3. Run `Endless Sky Landing Rights.exe` from the Endless Sky folder. It opens Dyson Sphere Program through Steam. Start or load a factory there, then fly.

To uninstall, delete those files. Neither game's own files are changed.

## Building

The design lives in `sheets/*.json`. `tools/preflight.py` checks every cell, every cross-reference and the game data. `tools/gen.py` generates the ES missions plus the C++ and C# tables. `build/es/build.sh` cross-compiles the ES build (Zig/clang, UCRT). `build/dsp/build.sh` builds the DSP parts. `tests/es/run.sh` runs the headless Endless Sky integration test.

## Credits & licenses

* Landing Rights itself (sheets, tools, DSP plugin and preloader): MIT, remixing welcome (`LICENSE`). The parts compiled into the Endless Sky build are GPLv3, like Endless Sky.

* Endless Sky © Michael Zahniser and contributors, GPLv3. The Landing Rights ES build and its changes are GPLv3 (`licenses/endless-sky-GPL-3.0.txt`). Source: upstream tag v0.11.2 plus `build/es/patch.py` and `src/es-engine/`.
* libmad 0.15.1b © Underbit Technologies, GPLv2+, statically linked into the ES build (MP3 music).
* FLAC++ 1.5.0 © Xiph.Org Foundation, BSD-3-Clause, statically linked (`licenses/flac-BSD.txt`).
* Unity Doorstop 4.3.0 © NeighTools, LGPL-2.1, unmodified, built from source (`licenses/doorstop-LGPL-2.1.txt`).
* Mono.Cecil © Jb Evain, MIT (`licenses/cecil-MIT.txt`).
* Dyson Sphere Program © Youthcat Studio / Gamera Games. This is an unofficial fan mashup and is not affiliated with either game.
