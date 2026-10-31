// Generated from sheets/*.json by tools/gen.py. Do not edit; change the sheet.
#pragma once
#include <string>

namespace LandingRightsTables {
struct Claim { const char *id; const char *planet; const char *world; int price; bool birth; };
inline constexpr Claim CLAIMS[] = {
	{"new-boston", "New Boston", "ocean", 5000, true},
	{"gemstone", "Gemstone", "volcanic", 40000, false},
	{"new-greenland", "New Greenland", "ice", 25000, false},
};
inline constexpr const char *BRIDGE_DIR = "landing-rights";
inline constexpr const char *CLAIM_PREFIX = "landing rights: claim ";
inline constexpr double POLL_SECONDS = 0.5;
inline constexpr double HEARTBEAT_STALE_SECONDS = 10.0;
inline constexpr const char *RETURN_KEY = "F8";
}
