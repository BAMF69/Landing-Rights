// Generated from sheets/*.json by tools/gen.py. Do not edit; change the sheet.
namespace LandingRights {
internal static class Tables {
	internal struct Shipment { public string Id; public int ItemId; public string ItemName; public string Commodity; public int ItemsPerTon; }
	internal static readonly Shipment[] Shipments = {
		new Shipment { Id = "iron-ingot", ItemId = 1101, ItemName = "Iron Ingot", Commodity = "Metal", ItemsPerTon = 20 },
		new Shipment { Id = "copper-ingot", ItemId = 1104, ItemName = "Copper Ingot", Commodity = "Metal", ItemsPerTon = 20 },
		new Shipment { Id = "steel", ItemId = 1103, ItemName = "Steel", Commodity = "Heavy Metals", ItemsPerTon = 10 },
		new Shipment { Id = "silicon", ItemId = 1105, ItemName = "High-Purity Silicon", Commodity = "Electronics", ItemsPerTon = 10 },
		new Shipment { Id = "gear", ItemId = 1201, ItemName = "Gear", Commodity = "Equipment", ItemsPerTon = 10 },
		new Shipment { Id = "magnetic-coil", ItemId = 1202, ItemName = "Magnetic Coil", Commodity = "Electronics", ItemsPerTon = 10 },
		new Shipment { Id = "circuit-board", ItemId = 1301, ItemName = "Circuit Board", Commodity = "Electronics", ItemsPerTon = 5 },
		new Shipment { Id = "plant-fuel", ItemId = 1031, ItemName = "Plant Fuel", Commodity = "Food", ItemsPerTon = 20 },
		new Shipment { Id = "organic-crystal", ItemId = 1117, ItemName = "Organic Crystal", Commodity = "Clothing", ItemsPerTon = 5 },
		new Shipment { Id = "plastic", ItemId = 1115, ItemName = "Plastic", Commodity = "Plastic", ItemsPerTon = 10 },
		new Shipment { Id = "sulfuric-acid", ItemId = 1116, ItemName = "Sulfuric Acid", Commodity = "Medical", ItemsPerTon = 10 },
		new Shipment { Id = "stone-brick", ItemId = 1108, ItemName = "Stone Brick", Commodity = "Industrial", ItemsPerTon = 40 },
		new Shipment { Id = "glass", ItemId = 1110, ItemName = "Glass", Commodity = "Industrial", ItemsPerTon = 20 },
		new Shipment { Id = "electric-motor", ItemId = 1203, ItemName = "Electric Motor", Commodity = "Industrial", ItemsPerTon = 5 },
		new Shipment { Id = "titanium-ingot", ItemId = 1106, ItemName = "Titanium Ingot", Commodity = "Heavy Metals", ItemsPerTon = 10 },
		new Shipment { Id = "titanium-alloy", ItemId = 1107, ItemName = "Titanium Alloy", Commodity = "Heavy Metals", ItemsPerTon = 4 },
		new Shipment { Id = "microcrystalline", ItemId = 1302, ItemName = "Microcrystalline Component", Commodity = "Electronics", ItemsPerTon = 4 },
		new Shipment { Id = "processor", ItemId = 1303, ItemName = "Processor", Commodity = "Electronics", ItemsPerTon = 2 },
		new Shipment { Id = "em-turbine", ItemId = 1204, ItemName = "Electromagnetic Turbine", Commodity = "Equipment", ItemsPerTon = 4 },
		new Shipment { Id = "thruster", ItemId = 1405, ItemName = "Thruster", Commodity = "Equipment", ItemsPerTon = 2 },
		new Shipment { Id = "prism", ItemId = 1111, ItemName = "Prism", Commodity = "Luxury Goods", ItemsPerTon = 10 },
		new Shipment { Id = "diamond", ItemId = 1112, ItemName = "Diamond", Commodity = "Luxury Goods", ItemsPerTon = 5 },
		new Shipment { Id = "casimir-crystal", ItemId = 1126, ItemName = "Casimir Crystal", Commodity = "Luxury Goods", ItemsPerTon = 2 },
	};
	internal struct World { public string Id; public string Label; public EPlanetType Type; }
	internal static readonly World[] Worlds = {
		new World { Id = "ocean", Label = "Ocean world", Type = EPlanetType.Ocean },
		new World { Id = "volcanic", Label = "Volcanic world", Type = EPlanetType.Vocano },
		new World { Id = "ice", Label = "Frozen world", Type = EPlanetType.Ice },
		new World { Id = "desert", Label = "Desert world", Type = EPlanetType.Desert },
	};
	internal struct Claim { public string Id; public string Planet; public string World; public bool Birth; }
	internal static readonly Claim[] Claims = {
		new Claim { Id = "new-boston", Planet = "New Boston", World = "ocean", Birth = true },
		new Claim { Id = "gemstone", Planet = "Gemstone", World = "volcanic", Birth = false },
		new Claim { Id = "new-greenland", Planet = "New Greenland", World = "ice", Birth = false },
	};
	internal const string BridgeDir = "landing-rights";
	internal const float PollSeconds = 0.5f;
	internal const float HeartbeatSeconds = 2.0f;
	internal const string ReturnKey = "F8";
	internal const bool MuteInBackground = true;
	internal const float ArrivalAltitude = 40.0f;
}
}
