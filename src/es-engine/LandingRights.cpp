/* LandingRights.cpp
Landing Rights: Endless Sky x Dyson Sphere Program.
Copyright (c) 2026 Landing Rights contributors. Added to Endless Sky, which is
free software under the GNU General Public License v3 or later; this file is too.
*/

#include "LandingRights.h"
#include "LandingRightsTables.h"

#include "ConditionsStore.h"
#include "DialogPanel.h"
#include "Files.h"
#include "GameData.h"
#include "GameWindow.h"
#include "Logger.h"
#include "Messages.h"
#include "Planet.h"
#include "PlayerInfo.h"
#include "System.h"
#include "UI.h"

#include <SDL2/SDL.h>

#include <chrono>
#include <filesystem>
#include <fstream>
#include <map>
#include <sstream>
#include <string>
#include <vector>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <tlhelp32.h>
#endif

using namespace std;
namespace fs = std::filesystem;

namespace {
	bool landed = false;
	bool lastClaimed = false;
	const Planet *landedPlanet = nullptr;
	long long seq = 0;
	double nextPoll = 0.;
	bool warnedNoDsp = false;
	bool dspWasInGame = false;
	double nextDspCheck = 0.;
	const char *ENGINE_CONDITION = "landing rights: engine";

	bool DspProcessRunning()
	{
#ifdef _WIN32
		HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
		if(snap == INVALID_HANDLE_VALUE)
			return false;
		PROCESSENTRY32W entry;
		entry.dwSize = sizeof(entry);
		bool found = false;
		for(BOOL ok = Process32FirstW(snap, &entry); ok && !found; ok = Process32NextW(snap, &entry))
			found = (_wcsicmp(entry.szExeFile, L"DSPGAME.exe") == 0);
		CloseHandle(snap);
		return found;
#else
		return false;
#endif
	}

	double Now()
	{
		return chrono::duration<double>(chrono::system_clock::now().time_since_epoch()).count();
	}

	fs::path Dir()
	{
		fs::path dir = Files::Config() / LandingRightsTables::BRIDGE_DIR;
		error_code ec;
		fs::create_directories(dir, ec);
		return dir;
	}

	map<string, string> ReadKV(const fs::path &path)
	{
		map<string, string> kv;
		ifstream in(path, ios::binary);
		string line;
		while(getline(in, line))
		{
			if(!line.empty() && line.back() == '\r')
				line.pop_back();
			size_t eq = line.find('=');
			if(eq != string::npos)
				kv[line.substr(0, eq)] = line.substr(eq + 1);
		}
		return kv;
	}

	void WriteKV(const fs::path &path, const vector<pair<string, string>> &kv)
	{
		fs::path tmp = path;
		tmp += ".tmp";
		{
			ofstream out(tmp, ios::binary | ios::trunc);
			for(const auto &it : kv)
				out << it.first << '=' << it.second << '\n';
		}
		error_code ec;
		fs::rename(tmp, path, ec);
		if(ec)
			Logger::Log("Landing Rights: could not write " + path.string() + ": " + ec.message(),
				Logger::Level::WARNING);
	}

	const LandingRightsTables::Claim *ClaimFor(const Planet *planet)
	{
		if(!planet)
			return nullptr;
		for(const auto &claim : LandingRightsTables::CLAIMS)
			if(planet->TrueName() == claim.planet)
				return &claim;
		return nullptr;
	}

	bool IsClaimed(PlayerInfo &player, const Planet *planet)
	{
		const auto *claim = ClaimFor(planet);
		return claim && player.Conditions().Get(string(LandingRightsTables::CLAIM_PREFIX) + claim->planet) > 0;
	}

	// 0 = Dyson Sphere Program not running, 1 = running but no factory loaded (main menu), 2 = in a game.
	int DspStatus()
	{
		auto kv = ReadKV(Dir() / "dsp.txt");
		if(kv.empty())
			return 0;
		double t = atof(kv["time"].c_str());
		if(Now() - t >= LandingRightsTables::HEARTBEAT_STALE_SECONDS)
			return 0;
		return kv["inGame"] == "1" ? 2 : 1;
	}

	bool DspRunning()
	{
		return DspStatus() == 2;
	}

	void WriteState(PlayerInfo &player, const string &event)
	{
		const Planet *planet = landedPlanet;
		const auto *claim = ClaimFor(planet);
		bool claimed = IsClaimed(player, planet);
		++seq;
		// Seed the sequence with the clock so a restarted ES never repeats an old number.
		if(seq < 1000)
			seq = static_cast<long long>(Now() * 10.);
		WriteKV(Dir() / "state.txt", {
			{"seq", to_string(seq)},
			{"event", event},
			{"planet", planet ? planet->TrueName() : ""},
			{"system", (planet && planet->GetSystem()) ? planet->GetSystem()->TrueName() : ""},
			{"claimed", claimed ? "1" : "0"},
			{"world", claim ? claim->world : ""},
			{"birth", (claim && claim->birth) ? "1" : "0"},
		});
		lastClaimed = claimed;
#ifdef _WIN32
		// Let the Dyson Sphere Program window come to the front when the player lands on a claim.
		if(claimed && event == "landed")
			AllowSetForegroundWindow(ASFW_ANY);
#endif
	}

	void Raise()
	{
		SDL_Window *window = SDL_GL_GetCurrentWindow();
		if(window)
		{
			SDL_RestoreWindow(window);
			SDL_RaiseWindow(window);
		}
	}

	// Load every shipment file into the landed cargo pool.
	void ReceiveShipments(PlayerInfo &player, UI &ui)
	{
		error_code ec;
		vector<fs::path> files;
		for(const auto &entry : fs::directory_iterator(Dir(), ec))
		{
			string name = entry.path().filename().string();
			if(name.rfind("shipment-", 0) == 0 && entry.path().extension() == ".txt")
				files.push_back(entry.path());
		}
		sort(files.begin(), files.end());
		for(const fs::path &file : files)
		{
			auto kv = ReadKV(file);
			fs::remove(file, ec);
			string planetName = kv["claim"];
			// Shipments only land in your hold while you are docked at that claim.
			if(!landedPlanet || landedPlanet->TrueName() != planetName)
			{
				Logger::Log("Landing Rights: dropped shipment for " + planetName + " (not landed there).",
					Logger::Level::WARNING);
				continue;
			}
			ostringstream summary;
			int total = 0;
			for(const auto &it : kv)
			{
				if(it.first.rfind("cargo.", 0) != 0)
					continue;
				string commodity = it.first.substr(6);
				int tons = atoi(it.second.c_str());
				bool known = false;
				for(const auto &c : GameData::Commodities())
					known |= (c.name == commodity);
				if(!known || tons <= 0)
					continue;
				player.Cargo().Add(commodity, tons);
				total += tons;
				summary << "\n\t" << tons << (tons == 1 ? " ton of " : " tons of ") << commodity;
			}
			bool back = kv["return"] == "1";
			if(back)
				Raise();
			if(total > 0)
				ui.Push(DialogPanel::Info("A cargo lifter rises from your claim on " + planetName
					+ " and docks with your fleet:" + summary.str()
					+ "\n\nIt's in your hold now; sell it at the trading desk or haul it somewhere that pays better."
					+ " (Press F here to go back down to your claim.)"));
			else if(back)
				ui.Push(DialogPanel::Info("Your mecha returns from the surface of " + planetName
					+ ". Your depots held no full tons to ship this time. (Press F here to go back down to your claim.)"));
		}
	}
}



void LandingRights::StartCompanion(bool interactive)
{
	if(!interactive)
		return;
	if(DspProcessRunning())
	{
		Logger::Log("Landing Rights: Dyson Sphere Program is already running.", Logger::Level::INFO);
		return;
	}
#ifdef _WIN32
	// Steam starts the player's own copy (app 1366540); the Landing Rights loader in its folder does the rest.
	ShellExecuteW(nullptr, L"open", L"steam://rungameid/1366540", nullptr, nullptr, SW_SHOWMINNOACTIVE);
	Logger::Log("Landing Rights: asked Steam to start Dyson Sphere Program.", Logger::Level::INFO);
#endif
}



void LandingRights::OnLanded(PlayerInfo &player)
{
	// Only this build offers claims; plain Endless Sky never sets this.
	player.Conditions().Set(ENGINE_CONDITION, 1);
	landed = true;
	landedPlanet = player.GetPlanet();
	warnedNoDsp = false;
	WriteState(player, "landed");
}



void LandingRights::OnDeparted(PlayerInfo &player)
{
	if(!landed)
		return;
	WriteState(player, "departed");
	player.Conditions().Set(ENGINE_CONDITION, 0);
	landed = false;
	landedPlanet = nullptr;
}



bool LandingRights::RequestBuild(PlayerInfo &player)
{
	if(!landed || !landedPlanet || !IsClaimed(player, landedPlanet))
		return false;
	WriteState(player, "landed");
	return true;
}



void LandingRights::Step(PlayerInfo &player, UI &gamePanels)
{
	double now = Now();
	// When DSP finishes loading a factory, come back to the front so the player can fly.
	if(now >= nextDspCheck)
	{
		nextDspCheck = now + 1.;
		bool inGame = DspRunning();
		if(inGame && !dspWasInGame)
		{
			Raise();
			Messages::Add({"Landing Rights: Dyson Sphere Program is ready. Land on a claim to build there.",
				GameData::MessageCategories().Get("normal")});
		}
		dspWasInGame = inGame;
	}
	if(!landed || !landedPlanet)
		return;
	if(now < nextPoll)
		return;
	nextPoll = now + LandingRightsTables::POLL_SECONDS;

	// The claim mission may have just been accepted while landed here.
	bool claimed = IsClaimed(player, landedPlanet);
	if(claimed != lastClaimed)
		WriteState(player, "landed");

	int dsp = claimed && !warnedNoDsp ? DspStatus() : 2;
	if(dsp != 2)
	{
		warnedNoDsp = true;
		if(dsp == 1)
			gamePanels.Push(DialogPanel::Info("Your claim on " + landedPlanet->TrueName()
				+ " is waiting. Dyson Sphere Program is open but still at its main menu."
				"\n\nStart a new game or load a factory save there, and your mecha will head straight down to this claim."));
		else
			gamePanels.Push(DialogPanel::Info("Your claim on " + landedPlanet->TrueName()
				+ " is quiet. Dyson Sphere Program isn't running, so nobody is working the surface."
				"\n\nStart Landing Rights from its shortcut (it opens Dyson Sphere Program for you), load a factory save there, and your mecha will head down."));
	}
	ReceiveShipments(player, gamePanels);
}
