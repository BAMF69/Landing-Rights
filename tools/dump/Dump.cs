using System;
using System.Linq;
using Mono.Cecil;

// Prints public/private member signatures of the named types in an assembly.
class Dump {
	static void Main(string[] args) {
		var asm = AssemblyDefinition.ReadAssembly(args[0]);
		foreach (var name in args.Skip(1)) {
			var filter = (string)null;
			var n = name;
			if (n.Contains(":")) { filter = n.Split(':')[1].ToLower(); n = n.Split(':')[0]; }
			var t = asm.MainModule.Types.FirstOrDefault(x => x.FullName == n);
			if (t == null) { Console.WriteLine("## " + n + " NOT FOUND"); continue; }
			Console.WriteLine("## " + t.FullName + " : " + (t.BaseType?.FullName ?? ""));
			foreach (var f in t.Fields) if (filter == null || f.Name.ToLower().Contains(filter)) Console.WriteLine("  F " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name);
			foreach (var p in t.Properties) if (filter == null || p.Name.ToLower().Contains(filter)) Console.WriteLine("  P " + p.PropertyType.Name + " " + p.Name);
			foreach (var m in t.Methods) if (!m.IsGetter && !m.IsSetter && (filter == null || m.Name.ToLower().Contains(filter)))
				Console.WriteLine("  M " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
		}
	}
}
