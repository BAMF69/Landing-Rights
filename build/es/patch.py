#!/usr/bin/env python3
"""Apply the Landing Rights hooks (sheets/hooks.json es-* rows) to an Endless Sky 0.11.2 source tree."""
import os, shutil, sys

src, ours = sys.argv[1], sys.argv[2]   # es-src, landing-rights/src/es-engine
S = os.path.join(src, "source")

def patch(rel, anchor, insert, after=True, include=None):
    p = os.path.join(S, rel)
    s = open(p, encoding="utf-8").read()
    if s.count(anchor + insert if after else insert + anchor) == 1:
        return
    assert s.count(anchor) == 1, f"{rel}: anchor not unique/found: {anchor!r}"
    s = s.replace(anchor, anchor + insert if after else insert + anchor)
    if include and include not in s:
        first = s.index("#include")
        s = s[:first] + include + "\n" + s[first:]
    open(p, "w", encoding="utf-8", newline="\n").write(s)

for f in ["LandingRights.h", "LandingRights.cpp", "LandingRightsTables.h"]:
    shutil.copy(os.path.join(ours, f), os.path.join(S, f))

# es-landed
patch("PlayerInfo.cpp", "\t// Mark this planet as visited.\n\tVisit(*planet);\n",
      "\tLandingRights::OnLanded(*this);\n", include='#include "LandingRights.h"')
# es-departed
patch("PlayerInfo.cpp", "\tthis->previousPlanet = planet;\n\treturn true;\n}",
      "\tLandingRights::OnDeparted(*this);\n", after=False)
# es-poll
patch("main.cpp", "\t\t\t((!isDebugPaused && menuPanels.IsEmpty()) ? gamePanels : menuPanels).StepAll();\n",
      "\t\t\tLandingRights::Step(player, gamePanels);\n", include='#include "LandingRights.h"')
# es-poll (headless/integration-test loop)
patch("main.cpp", "\t\t\t(menuPanels.IsEmpty() ? gamePanels : menuPanels).StepAll();\n",
      "\t\t\tLandingRights::Step(player, gamePanels);\n")
# es-start
patch("main.cpp", "\tbool isConsoleOnly = loadOnly || printTests || printData;\n",
      "\tLandingRights::StartCompanion(!isConsoleOnly && !isTesting);\n")
# es-build-key
patch("PlanetPanel.cpp", "\telse if(key == 'd' && flagship && flagship->CanBeFlagship())\n",
      "\telse if(key == 'f' && LandingRights::RequestBuild(player))\n\t{\n\t\tUI::PlaySound(UI::UISound::NORMAL);\n\t\treturn true;\n\t}\n",
      after=False, include='#include "LandingRights.h"')
print("patched")
