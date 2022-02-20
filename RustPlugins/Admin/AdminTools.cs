using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins.AdminToolsPlugin.Extentions;
using Rust.Modular;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Oxide.Plugins
{
	[Info("Admin.Tools", "pashamaladec", "2")]
	public class AdminTools : CovalencePlugin
	{
		private void Init() => Extentions.Init(players);

		[Command("heal"), Permission("admintools.use.heal"), UsedImplicitly]
		private void OnHealCommand(IPlayer player, string cmd, string[] args)
		{
			var target = args.ParseTarget(player);
			var basetarget = target.ToBase();

			Debug.Assert(basetarget != null, nameof(basetarget) + " != null");
			player.Reply($"Healed {basetarget}");

			if (basetarget.IsWounded())
				basetarget.RecoverFromWounded();

			basetarget.metabolism.hydration.SetValue(1000f);
			basetarget.metabolism.calories.SetValue(1000f);
			basetarget.metabolism.radiation_poison.SetValue(0f);
			basetarget.metabolism.bleeding.SetValue(0f);
			target.Health = 100f;
			target.MaxHealth = 100f;
		}

		[Command("wound"), Permission("admintools.use.wound"), UsedImplicitly]
		private void OnWoundCommand(IPlayer player, string cmd, string[] args)
		{
			var target = args.ParseTarget(player).ToBase();
			target.BecomeWounded();
			player.Reply($"Wounded {target}");
		}

		[Command("buildtools"), Permission("admintools.use.buildtools"), UsedImplicitly]
		private void OnBuildToolsCommand(IPlayer player, string cmd, string[] args)
		{
			var target = args.ParseTarget(player).ToBase();
			target.GiveItem(ItemManager.CreateByItemID(-151838493, 100000));
			target.GiveItem(ItemManager.CreateByItemID(-2099697608, 100000));
			target.GiveItem(ItemManager.CreateByItemID(69511070, 100000));
			target.GiveItem(ItemManager.CreateByItemID(317398316, 100000));
			target.GiveItem(ItemManager.CreateByItemID(1525520776));
			target.GiveItem(ItemManager.CreateByItemID(200773292));
			target.GiveItem(ItemManager.CreateByItemID(-97956382));
			player.Reply($"Gived build tools to {target}");
		}

		[Command("strip"), Permission("admintools.use.strip"), UsedImplicitly]
		private void OnStripCommand(IPlayer player, string cmd, string[] args)
		{
			var target = args.ParseTarget(player).ToBase();
			target.inventory.Strip();
			player.Reply($"Stripped {target} inventory");
		}

		[Command("lookat"), Permission("admintools.use.lookat"), UsedImplicitly]
		private void OnLookAtCommand(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();
			var entity = hit.GetEntity();

			var links = entity.GetEntityLinks();
			
			var builder = new StringBuilder($"You hitted: {entity.name} {entity.transform.position} {entity.transform.rotation}\n");

			ParseComponents(builder, entity);
			
			player.Reply(builder.ToString());
		}

		[Command("sportcar"), Permission("admintools.use.sportcar"), UsedImplicitly]
		private void OnSportCarCalled(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();

			var car = GameManager.server.CreateEntity("assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab", hit.point) as ModularCar;
			var cockpit = ItemManager.CreateByItemID(170758448);

			car.Spawn();

			car.TryAddModule(ItemManager.CreateByItemID(1559779253), 0);
			car.TryAddModule(cockpit, 1);
			car.TryAddModule(ItemManager.CreateByItemID(1559779253), 2);
			
			var fuel = car.GetFuelSystem().GetFuelContainer();
			var fuelItem = ItemManager.CreateByItemID(-946369541, 10000);
			fuel.inventory.Insert(fuelItem);
			
			NextTick(() =>
			{
				foreach (var engine in car.AttachedModuleEntities.OfType<VehicleModuleEngine>())
				{
					engine.engine.engineKW = 300;
					
					var storage = engine.GetContainer() as EngineStorage;
					if (storage == null)
						continue;

					var engineRawItems = new List<Item>()
					{
						ItemManager.CreateByItemID(656371026),
						ItemManager.CreateByItemID(1158340332),
						ItemManager.CreateByItemID(1072924620, 2),
						ItemManager.CreateByItemID(-1802083073, 2),
						ItemManager.CreateByItemID(1883981800, 2),
					};
					
					var engineParts = engineRawItems.ToDictionary(key => key.info.GetComponent<ItemModEngineItem>().engineItemType,
						value => value);
					
					for (var i = 0; i < storage.inventory.capacity; i++)
					{
						var slotType = storage.slotTypes[i];
						var part = engineParts[slotType];

						part.MoveToContainer(storage.inventory, i, false);
					}
				}
			});
		}
		
		[Command("carlock")]
		private void GetCarLock(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();
			var entity = hit.GetEntity();

			ModularCar car;
			if (entity.TryGetComponent(out car) == false)
				return;

			player.Reply(car.carLock.HasALock ? car.carLock.LockID.ToString() : "No lock");
		}
		
		private void ParseComponents(StringBuilder builder, BaseEntity entity)
		{
			builder.AppendLine($"\n\nComponents of {entity.name}");
			if (entity.HasParent())
				builder.Append($"(parent {entity.parentEntity.Get(true).name})");
			
			var list = new List<Component>();
			entity.GetComponents(list);
			list.ForEach(x => builder.AppendLine(x.ToString()));

			if (entity.children.Any() == false)
			{
				builder.AppendLine($"\nNo childs for {entity}");
				return;
			}

			foreach (var child in entity.children)
			{
				ParseComponents(builder, child);
			}
		}
	}

	namespace AdminToolsPlugin.Extentions
	{
		public static class Extentions
		{
			private static IPlayerManager _players;
			public static void Init(IPlayerManager manager) => _players = manager;

			public static BasePlayer ToBase(this IPlayer player) => player.Object as BasePlayer;

			public static IPlayer ParseTarget(this string[] commandArgs, IPlayer ifNull = null, int offset = 0)
			{
				if (offset >= commandArgs.Length)
					return ifNull;

				var target = _players.FindPlayer(commandArgs[offset]);

				if (target == null)
					return ifNull;

				return target;
			}

			public static RaycastHit RaycastFromEyes(this IPlayer player)
			{
				var baseplayer = player.ToBase();
				RaycastHit hit;
				Physics.Raycast(baseplayer.eyes.HeadRay(), out hit);
				return hit;
			}
		}
	}
}