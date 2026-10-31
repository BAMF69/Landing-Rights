// Landing Rights plugin for Dyson Sphere Program (MIT).
// Runs inside the player's own DSP. Talks to the Landing Rights build of Endless Sky
// through small key=value files in %APPDATA%\endless-sky\landing-rights\ (sheets/bridge.json).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace LandingRights
{
	public static class Loader
	{
		static bool booted;

		// Called from the first line of VFPreload.Start / DSPGame.Awake (added in memory by the preloader).
		public static void Boot()
		{
			if (booted) return;
			booted = true;
			try
			{
				var go = new GameObject("Landing Rights");
				UnityEngine.Object.DontDestroyOnLoad(go);
				go.AddComponent<Bridge>();
			}
			catch (Exception e) { Log.Write("Boot failed: " + e); }
		}
	}

	internal static class Log
	{
		static readonly object gate = new object();
		internal static string Dir;
		internal static void Write(string line)
		{
			try
			{
				lock (gate) File.AppendAllText(Path.Combine(Dir ?? ".", "LandingRights.log"), DateTime.Now.ToString("HH:mm:ss ") + line + "\r\n");
			}
			catch { }
		}
	}

	internal static class Native
	{
		[DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
		[DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hWnd, int cmd);
		[DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(int pid);
		[DllImport("user32.dll")] internal static extern IntPtr GetActiveWindow();
		[DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hWnd);
		internal const int ASFW_ANY = -1, SW_RESTORE = 9;
	}

	public class Bridge : MonoBehaviour
	{
		string bridgeDir, modDir;
		long lastSeq = -1;
		float nextPoll, nextBeat;
		KeyCode returnKey = KeyCode.F8;
		bool muteInBackground = Tables.MuteInBackground;
		float savedVolume = -1f;
		bool itemsChecked;

		// The claim the player is landed on in Endless Sky right now (null when flying).
		Tables.Claim? activeClaim;
		PlanetData travelTarget;
		int travelStep;
		float travelTimeout;
		IntPtr hwnd;

		void Awake()
		{
			modDir = Path.GetDirectoryName(typeof(Bridge).Assembly.Location);
			if (string.IsNullOrEmpty(modDir)) modDir = Path.Combine(Application.dataPath, "../LandingRights");
			Log.Dir = modDir;
			bridgeDir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "endless-sky"), Tables.BridgeDir);
			Directory.CreateDirectory(bridgeDir);
			LoadConfig();
			Log.Write("Landing Rights started. Bridge: " + bridgeDir + "  Return key: " + returnKey);
		}

		void LoadConfig()
		{
			string path = Path.Combine(modDir, "LandingRights.cfg");
			if (!File.Exists(path))
				File.WriteAllText(path, "# Key that ships your goods and sends you back to your Endless Sky fleet (UnityEngine.KeyCode name)\r\nreturnKey=" + Tables.ReturnKey + "\r\n# Silence Dyson Sphere Program while it is in the background (true/false)\r\nmuteInBackground=" + (Tables.MuteInBackground ? "true" : "false") + "\r\n");
			foreach (var kv in ReadKV(path))
				if (kv.Key == "muteInBackground") muteInBackground = kv.Value.Trim().ToLowerInvariant() != "false";
				else if (kv.Key == "returnKey")
					try { returnKey = (KeyCode)Enum.Parse(typeof(KeyCode), kv.Value, true); } catch { Log.Write("bad returnKey " + kv.Value); }
		}

		static Dictionary<string, string> ReadKV(string path)
		{
			var d = new Dictionary<string, string>();
			if (!File.Exists(path)) return d;
			string text;
			try { text = File.ReadAllText(path); } catch { return d; }
			foreach (var raw in text.Split('\n'))
			{
				var line = raw.TrimEnd('\r');
				if (line.StartsWith("#")) continue;
				int eq = line.IndexOf('=');
				if (eq > 0) d[line.Substring(0, eq)] = line.Substring(eq + 1);
			}
			return d;
		}

		static void WriteKV(string path, IEnumerable<KeyValuePair<string, string>> kv)
		{
			var sb = new StringBuilder();
			foreach (var p in kv) sb.Append(p.Key).Append('=').Append(p.Value).Append('\n');
			string tmp = path + ".tmp";
			File.WriteAllText(tmp, sb.ToString());
			if (File.Exists(path)) File.Delete(path);
			File.Move(tmp, path);
		}

		static bool InGame()
		{
			return GameMain.instance != null && GameMain.isRunning && !GameMain.instance.isMenuDemo
				&& GameMain.mainPlayer != null && GameMain.galaxy != null && !GameMain.isLoading;
		}

		void Update()
		{
			try
			{
				float now = Time.realtimeSinceStartup;
				if (now >= nextBeat) { nextBeat = now + Tables.HeartbeatSeconds; Heartbeat(); }
				if (!InGame()) return;
				if (!itemsChecked) CheckItems();
				if (now >= nextPoll) { nextPoll = now + Tables.PollSeconds; Poll(); }
				if (travelTarget != null) StepTravel();
				if (Input.GetKeyDown(returnKey)) ShipAndReturn();
			}
			catch (Exception e) { Log.Write("Update: " + e); }
		}

		// Unity calls this when the DSP window gains or loses focus.
		void OnApplicationFocus(bool focused)
		{
			try
			{
				if (!focused && muteInBackground && savedVolume < 0f)
				{
					savedVolume = AudioListener.volume;
					AudioListener.volume = 0f;
				}
				else if (focused && savedVolume >= 0f)
				{
					AudioListener.volume = savedVolume;
					savedVolume = -1f;
				}
			}
			catch (Exception e) { Log.Write("mute: " + e.Message); }
		}

		void OnApplicationQuit()
		{
			if (savedVolume >= 0f) AudioListener.volume = savedVolume;
		}

		void Heartbeat()
		{
			double unix = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
			var p = InGame() && GameMain.localPlanet != null ? GameMain.localPlanet.displayName : "";
			WriteKV(Path.Combine(bridgeDir, "dsp.txt"), new[] {
				new KeyValuePair<string, string>("time", unix.ToString("F0")),
				new KeyValuePair<string, string>("inGame", InGame() ? "1" : "0"),
				new KeyValuePair<string, string>("planet", p),
			});
		}

		void CheckItems()
		{
			itemsChecked = true;
			for (int i = 0; i < Tables.Shipments.Length; i++)
			{
				var row = Tables.Shipments[i];
				var proto = LDB.items.Select(row.ItemId);
				if (proto != null && string.Equals(proto.name, row.ItemName, StringComparison.OrdinalIgnoreCase))
				{
					Log.Write("item " + row.ItemId + " = '" + proto.name + "' OK -> " + row.Commodity);
					continue;
				}
				// ID didn't match: look the item up by its English name in the player's own item table.
				ItemProto byName = null;
				foreach (var it in LDB.items.dataArray)
					if (it != null && string.Equals(it.name, row.ItemName, StringComparison.OrdinalIgnoreCase)) { byName = it; break; }
				if (byName != null)
				{
					Tables.Shipments[i].ItemId = byName.ID;
					Log.Write("item '" + row.ItemName + "' found as id " + byName.ID + " (sheet said " + row.ItemId + ") -> " + row.Commodity);
				}
				else if (proto != null)
					Log.Write("item " + row.ItemId + " = '" + proto.name + "' (sheet says '" + row.ItemName + "'; non-English game or mismatch, using the id) -> " + row.Commodity);
				else
					Log.Write("ITEM MISSING " + row.ItemId + " (" + row.ItemName + ")");
			}
		}

		void Poll()
		{
			var st = ReadKV(Path.Combine(bridgeDir, "state.txt"));
			string seqText;
			if (!st.TryGetValue("seq", out seqText)) return;
			long seq;
			if (!long.TryParse(seqText, out seq) || seq == lastSeq) return;
			bool first = lastSeq < 0;
			lastSeq = seq;
			string ev = Get(st, "event"), planet = Get(st, "planet");
			Log.Write("ES " + ev + " " + planet + " claimed=" + Get(st, "claimed"));
			if (ev == "landed" && Get(st, "claimed") == "1")
			{
				var claim = Tables.Claims.FirstOrDefault(c => c.Planet == planet);
				if (claim.Planet == null) return;
				activeClaim = claim;
				var target = BoundPlanet(claim);
				if (target == null) { Popup("Landing Rights: no planet in this galaxy fits your " + planet + " claim."); return; }
				BringToFront();
				Popup("Landed on your " + planet + " claim  →  " + target.displayName + ".  Build, switch a Depot to \"Ship to Endless Sky\", fill it, then press " + returnKey + " to ship goods up to your fleet.");
				if (GameMain.localPlanet != target) BeginTravel(target);
			}
			else if (ev == "departed" || ev == "landed")
			{
				activeClaim = null;
				if (!first && ev == "departed") Log.Write("player left " + planet);
			}
		}

		static string Get(Dictionary<string, string> d, string k) { string v; return d.TryGetValue(k, out v) ? v : ""; }

		// ---------- claim -> planet binding, saved per galaxy seed ----------
		string BindingsPath { get { return Path.Combine(modDir, "bindings.txt"); } }
		string SeedKey { get { return GameMain.data.gameDesc != null ? GameMain.data.gameDesc.galaxySeed.ToString() : "0"; } }

		PlanetData BoundPlanet(Tables.Claim claim)
		{
			var galaxy = GameMain.galaxy;
			var all = ReadKV(BindingsPath);
			string key = SeedKey + "|" + claim.Planet;
			string idText;
			int id;
			if (all.TryGetValue(key, out idText) && int.TryParse(idText, out id) && galaxy.PlanetById(id) != null)
				return galaxy.PlanetById(id);

			var used = new HashSet<int>();
			foreach (var kv in all) if (kv.Key.StartsWith(SeedKey + "|") && int.TryParse(kv.Value, out id)) used.Add(id);
			PlanetData pick = null;
			if (claim.Birth) pick = galaxy.PlanetById(galaxy.birthPlanetId);
			if (pick == null || used.Contains(pick.id))
			{
				var world = Tables.Worlds.First(w => w.Id == claim.World);
				var birthStar = galaxy.StarById(galaxy.birthStarId);
				var planets = galaxy.stars.Where(s => s != null).SelectMany(s => s.planets).Where(p => p != null && p.type != EPlanetType.Gas && !used.Contains(p.id));
				Func<PlanetData, double> dist = p => birthStar == null ? 0 : (p.star.uPosition - birthStar.uPosition).magnitude;
				pick = planets.Where(p => p.type == world.Type).OrderBy(dist).FirstOrDefault() ?? planets.OrderBy(dist).FirstOrDefault();
			}
			if (pick == null) return null;
			all[key] = pick.id.ToString();
			WriteKV(BindingsPath, all);
			Log.Write("bound " + claim.Planet + " -> " + pick.displayName + " (" + pick.type + ", id " + pick.id + ")");
			return pick;
		}

		// ---------- travel: put the mecha on the bound planet ----------
		void BeginTravel(PlanetData target)
		{
			travelTarget = target;
			travelStep = 0;
			travelTimeout = Time.realtimeSinceStartup + 30f;
			Log.Write("travel to " + target.displayName);
		}

		void StepTravel()
		{
			var data = GameMain.data;
			var t = travelTarget;
			if (Time.realtimeSinceStartup > travelTimeout) { Log.Write("travel timed out at step " + travelStep); travelTarget = null; Popup("Landing Rights: couldn't reach " + t.displayName + ". Fly there yourself; shipping still works from any of its storage boxes."); return; }
			switch (travelStep)
			{
				case 0:
					if (GameMain.localPlanet != null && GameMain.localPlanet != t) data.LeavePlanet();
					if (GameMain.localStar != t.star)
					{
						if (GameMain.localStar != null) data.LeaveStar();
						data.ArriveStar(t.star);
					}
					travelStep = 1;
					break;
				case 1:
					if (!t.loaded) return; // star's planets load over a few frames
					var player = GameMain.mainPlayer;
					var up = (VectorLF3)(t.runtimeRotation * Vector3.up);
					player.uPosition = t.uPosition + up * (t.realRadius + Tables.ArrivalAltitude);
					player.controller.velocityOnLanding = Vector3.zero;
					data.ArrivePlanet(t);
					travelStep = 2;
					break;
				case 2:
					if (GameMain.localPlanet != t) return;
					GameMain.mainPlayer.position = Vector3.up * (t.realRadius + Tables.ArrivalAltitude);
					GameMain.mainPlayer.controller.movementStateInFrame = EMovementState.Fly;
					Log.Write("arrived at " + t.displayName);
					travelTarget = null;
					break;
			}
		}

		// ---------- spaceport depots: only these ship to Endless Sky ----------
		string SpaceportsPath { get { return Path.Combine(modDir, "spaceports.txt"); } }
		HashSet<string> spaceports;
		GUIStyle toggleStyle;

		static string SpaceportKey(int planetId, int entityId)
		{
			return (GameMain.data.gameDesc != null ? GameMain.data.gameDesc.galaxySeed : 0) + "|" + planetId + "|" + entityId;
		}

		HashSet<string> Spaceports()
		{
			if (spaceports == null)
				spaceports = new HashSet<string>(ReadKV(SpaceportsPath).Where(kv => kv.Value == "1").Select(kv => kv.Key));
			return spaceports;
		}

		void SetSpaceport(string key, bool on)
		{
			if (on) Spaceports().Add(key); else Spaceports().Remove(key);
			WriteKV(SpaceportsPath, Spaceports().Select(k => new KeyValuePair<string, string>(k, "1")));
			Log.Write((on ? "spaceport on: " : "spaceport off: ") + key);
		}

		// Draws a "Ship to Endless Sky" switch just above the open Depot window.
		void OnGUI()
		{
			try
			{
				if (!InGame() || UIRoot.instance == null || UIRoot.instance.uiGame == null) return;
				var win = UIRoot.instance.uiGame.storageWindow;
				if (win == null || !win.active || win.factory == null || win.factoryStorage == null) return;
				int sid = win.storageId;
				if (sid <= 0 || sid >= win.factoryStorage.storagePool.Length) return;
				var box = win.factoryStorage.storagePool[sid];
				if (box == null || box.entityId <= 0) return;
				string key = SpaceportKey(win.factory.planetId, box.entityId);
				bool on = Spaceports().Contains(key);

				Rect r = new Rect(Screen.width / 2f - 150f, 90f, 300f, 34f);
				if (win.windowTrans != null)
				{
					var corners = new Vector3[4];
					win.windowTrans.GetWorldCorners(corners); // 0 bottom-left, 1 top-left, 2 top-right
					var canvas = win.windowTrans.GetComponentInParent<Canvas>();
					Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
					Vector2 tl = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
					Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
					float w = Mathf.Max(220f, Mathf.Min(320f, tr.x - tl.x));
					r = new Rect(tl.x, Screen.height - tl.y - 38f, w, 34f);
				}
				if (toggleStyle == null)
				{
					toggleStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
				}
				Color old = GUI.backgroundColor;
				GUI.backgroundColor = on ? new Color(0.35f, 0.85f, 1f) : new Color(0.45f, 0.45f, 0.45f);
				string label = on ? "\u2714  Spaceport: ships to Endless Sky" : "\u2610  Ship to Endless Sky (off)";
				if (GUI.Button(r, label, toggleStyle))
				{
					SetSpaceport(key, !on);
					Popup(!on ? "This depot is now a spaceport: its ingots and parts ship to your Endless Sky fleet when you press " + returnKey + "." : "This depot no longer ships to Endless Sky.");
				}
				GUI.backgroundColor = old;
			}
			catch (Exception e) { Log.Write("OnGUI: " + e.Message); }
		}

		// ---------- shipping ----------
		void ShipAndReturn()
		{
			if (activeClaim == null)
			{
				Popup("Landing Rights: you're not landed on a claim in Endless Sky.");
				return;
			}
			var claim = activeClaim.Value;
			var planet = BoundPlanet(claim);
			var tons = new Dictionary<string, int>();
			var detail = new List<string>();
			var found = new Dictionary<int, int>();   // item id -> count seen in depots
			int depots = 0;
			var factory = planet != null ? planet.factory : null;
			if (factory != null && factory.factoryStorage != null && factory.factoryStorage.storagePool != null)
			{
				foreach (var box in factory.factoryStorage.storagePool)
				{
					if (box == null || box.id <= 0 || box.entityId <= 0) continue;
					if (!Spaceports().Contains(SpaceportKey(planet.id, box.entityId))) continue;
					depots++;
					foreach (var s in Tables.Shipments)
					{
						int have = box.GetItemCount(s.ItemId);
						if (have <= 0) continue;
						int seen; found.TryGetValue(s.ItemId, out seen); found[s.ItemId] = seen + have;
					}
				}
				// Ship whole tons per item type across all depots, so items spread over several depots add up.
				foreach (var s in Tables.Shipments)
				{
					int total; found.TryGetValue(s.ItemId, out total);
					int want = (total / s.ItemsPerTon) * s.ItemsPerTon;
					if (want <= 0) continue;
					int took = 0;
					foreach (var box in factory.factoryStorage.storagePool)
					{
						if (box == null || box.id <= 0 || box.entityId <= 0 || took >= want) continue;
						if (!Spaceports().Contains(SpaceportKey(planet.id, box.entityId))) continue;
						int n = Math.Min(want - took, box.GetItemCount(s.ItemId));
						if (n <= 0) continue;
						int inc;
						took += box.TakeItem(s.ItemId, n, out inc);
					}
					int gotTons = took / s.ItemsPerTon;
					if (gotTons <= 0) continue;
					int prev; tons.TryGetValue(s.Commodity, out prev);
					tons[s.Commodity] = prev + gotTons;
					detail.Add(took + " " + s.ItemName);
				}
			}
			Log.Write("spaceport depots on " + (planet != null ? planet.displayName : "?") + ": " + depots + "; found " +
				(found.Count == 0 ? "no shippable items" : string.Join(", ", found.Select(f => f.Value + " x item " + f.Key).ToArray())));
			var kv = new List<KeyValuePair<string, string>> {
				new KeyValuePair<string, string>("seq", DateTime.UtcNow.Ticks.ToString()),
				new KeyValuePair<string, string>("claim", claim.Planet),
				new KeyValuePair<string, string>("return", "1"),
			};
			foreach (var p in tons) kv.Add(new KeyValuePair<string, string>("cargo." + p.Key, p.Value.ToString()));
			Native.AllowSetForegroundWindow(Native.ASFW_ANY);
			WriteKV(Path.Combine(bridgeDir, "shipment-" + DateTime.UtcNow.Ticks + ".txt"), kv);
			Log.Write("shipped from " + claim.Planet + ": " + (detail.Count > 0 ? string.Join(", ", detail.ToArray()) : "nothing"));
			if (detail.Count > 0)
				Popup("Cargo lifter away: " + string.Join(", ", detail.ToArray()) + ". Back to your fleet.");
			else if (depots == 0)
				Popup("No spaceport depots on " + (planet != null ? planet.displayName : "this planet") + " yet. Open a Depot and click \"Ship to Endless Sky\" above its window, fill it, then press " + returnKey + ". Back to your fleet.");
			else if (found.Count == 0)
				Popup("Your spaceport depots hold nothing on the shipping list (ingots, parts, plastic, glass, diamonds and more; see the Landing Rights README). Back to your fleet.");
			else
				Popup("Not a full ton yet: " + string.Join(", ", Tables.Shipments.Where(x => found.ContainsKey(x.ItemId)).Select(x => found[x.ItemId] + "/" + x.ItemsPerTon + " " + x.ItemName).ToArray()) + ". Leftovers stay in the depot. Back to your fleet.");
			activeClaim = null;
		}

		void BringToFront()
		{
			try
			{
				if (hwnd == IntPtr.Zero) hwnd = Process.GetCurrentProcess().MainWindowHandle;
				if (hwnd == IntPtr.Zero) hwnd = Native.GetActiveWindow();
				if (hwnd == IntPtr.Zero) return;
				if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
				Native.SetForegroundWindow(hwnd);
			}
			catch (Exception e) { Log.Write("focus: " + e.Message); }
		}

		static void Popup(string text)
		{
			Log.Write("tip: " + text);
			try { UIRealtimeTip.Popup(text, false, 0); } catch { }
		}
	}
}
