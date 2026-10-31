// Landing Rights preloader for Dyson Sphere Program (MIT).
// Unity Doorstop calls Doorstop.Entrypoint.Start() before the game loads its scripts.
// We add one call, LandingRights.Loader.Boot(), to the start of VFPreload.Start and
// DSPGame.Awake in an in-memory copy of Assembly-CSharp (the same technique BepInEx's
// preloader uses). The game's files on disk are never changed.
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Doorstop
{
	public static class Entrypoint
	{
		static string ourDir;
		static StreamWriter log;

		public static void Start()
		{
			try
			{
				ourDir = Path.GetDirectoryName(Path.GetFullPath(Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH") ?? typeof(Entrypoint).Assembly.Location));
				log = new StreamWriter(Path.Combine(ourDir, "preloader.log"), false) { AutoFlush = true };
				log.WriteLine("Landing Rights preloader " + DateTime.Now.ToString("u"));
				AppDomain.CurrentDomain.AssemblyResolve += Resolve;
				Patch();
			}
			catch (Exception e)
			{
				try { log.WriteLine("FAILED: " + e); } catch { }
			}
		}

		static Assembly Resolve(object sender, ResolveEventArgs args)
		{
			string name = new AssemblyName(args.Name).Name;
			string path = Path.Combine(ourDir, name + ".dll");
			return File.Exists(path) ? Assembly.LoadFrom(path) : null;
		}

		// Kept in its own method so Mono.Cecil is only needed once AssemblyResolve is in place.
		static void Patch()
		{
			string managed = Environment.GetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR");
			if (string.IsNullOrEmpty(managed))
			{
				string exe = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
				managed = Path.Combine(Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + "_Data"), "Managed");
			}
			log.WriteLine("Managed: " + managed);
			var resolver = new Mono.Cecil.DefaultAssemblyResolver();
			resolver.AddSearchDirectory(managed);
			resolver.AddSearchDirectory(ourDir);
			var rp = new Mono.Cecil.ReaderParameters { AssemblyResolver = resolver, ReadingMode = Mono.Cecil.ReadingMode.Immediate };
			var game = Mono.Cecil.AssemblyDefinition.ReadAssembly(Path.Combine(managed, "Assembly-CSharp.dll"), rp);
			var plugin = Mono.Cecil.AssemblyDefinition.ReadAssembly(Path.Combine(ourDir, "LandingRights.dll"), rp);
			var boot = plugin.MainModule.GetType("LandingRights.Loader").Methods.First(m => m.Name == "Boot");
			var bootRef = game.MainModule.ImportReference(boot);

			int patched = 0;
			foreach (var spot in new[] { "VFPreload:Start", "DSPGame:Awake" })
			{
				var type = game.MainModule.GetType(spot.Split(':')[0]);
				var method = type == null ? null : type.Methods.FirstOrDefault(m => m.Name == spot.Split(':')[1] && !m.HasParameters);
				if (method == null || !method.HasBody) { log.WriteLine("hook point missing: " + spot); continue; }
				var il = method.Body.GetILProcessor();
				il.InsertBefore(method.Body.Instructions[0], il.Create(Mono.Cecil.Cil.OpCodes.Call, bootRef));
				patched++;
				log.WriteLine("hooked " + spot);
			}
			if (patched == 0) { log.WriteLine("nothing hooked; Landing Rights stays off"); return; }

			var ms = new MemoryStream();
			game.Write(ms);
			// Load our plugin first so the patched Assembly-CSharp's reference resolves to it,
			// then the patched game assembly; Mono reuses already-loaded assemblies by name.
			Assembly.LoadFrom(Path.Combine(ourDir, "LandingRights.dll"));
			Assembly.Load(ms.ToArray());
			log.WriteLine("patched Assembly-CSharp loaded (" + ms.Length + " bytes)");
		}
	}
}
