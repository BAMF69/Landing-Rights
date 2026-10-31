/* LandingRights.h
Landing Rights: Endless Sky x Dyson Sphere Program.
Copyright (c) 2026 Landing Rights contributors. Added to Endless Sky, which is
free software under the GNU General Public License v3 or later; this file is too.
*/

#pragma once

class PlayerInfo;
class UI;

// Bridge between Endless Sky and the Landing Rights plugin running in Dyson Sphere Program.
// Both games exchange small key=value text files in <ES config>/landing-rights/.
namespace LandingRights {
	// Called from main() after Files::Init. Starts Dyson Sphere Program (through Steam) unless it
	// is already running; never closes or restarts anything. Skipped for tests and console runs.
	void StartCompanion(bool interactive);
	// Called at the end of PlayerInfo::Land().
	void OnLanded(PlayerInfo &player);
	// Called when PlayerInfo::TakeOff() succeeds.
	void OnDeparted(PlayerInfo &player);
	// Called when the player presses F on the planet screen. If landed on an owned claim,
	// re-announces the landing so Dyson Sphere Program comes back to the front. Returns true if handled.
	bool RequestBuild(PlayerInfo &player);
	// Called once per frame from the game loop.
	void Step(PlayerInfo &player, UI &gamePanels);
}
