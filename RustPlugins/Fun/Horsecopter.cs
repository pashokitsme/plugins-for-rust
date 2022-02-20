using System.Collections.Generic;
using System.Linq;
using Network;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("HorseCopter", "pashamaladec", "1")]
	public class Horsecopter : CovalencePlugin
	{
		private const string HORSE_PREFAB = "assets/rust.ai/nextai/testridablehorse.prefab";
		private const string MINICOPTER_PREFAB = "assets/content/vehicles/minicopter/minicopter.entity.prefab";

		private readonly Dictionary<RidableHorse, MiniCopter> _copters = new Dictionary<RidableHorse, MiniCopter>();

		private void Init()
		{
			server.Broadcast("хуйня плагин загрузился");
		}

		#region Commands

		[Command("vertihorse")]
		private void CallVertiHorse(IPlayer pl, string cmd, string[] args)
		{
			var player = AsBase(pl);
			RaycastHit hit;
			if (Physics.Raycast(player.eyes.HeadRay(), out hit, 30f) == false)
			{
				player.ChatMessage("Иди нахуй");
				return;
			}

			var copter = GameManager.server.CreateEntity(MINICOPTER_PREFAB, hit.point) as MiniCopter;
			copter.Spawn();
			var horse = GameManager.server.CreateEntity(HORSE_PREFAB, hit.point) as RidableHorse;
			horse.Spawn();
			copter.GetFuelSystem().AdminAddFuel();

			copter.limitNetworking = true;
			
			copter.mountPoints = horse.mountPoints;
			horse.maxSpeed = 0f;
			horse.walkSpeed = 0f;
			horse.runSpeed = 0f;
			horse.turnSpeed = 0f;
			
			horse.SendNetworkUpdateImmediate(true);
			_copters.Add(horse, copter);
		}

		#endregion

		private void OnTick()
		{
			foreach (var pair in _copters)
			{
				if (pair.Key == null || pair.Value == null)
				{
					continue;
				}
				
				var copterTransform = pair.Value.transform;

				pair.Key.transform.position = copterTransform.position;
				pair.Key.transform.rotation = copterTransform.rotation;
				
				pair.Key.SendNetworkUpdate();
			}
		}
		
		private void OnPlayerInput(BasePlayer player, InputState input)
		{
			if (player.isMounted == false)
				return;

			var saddle = player.mounted.Get(true);
		
			if (saddle == null)
				return;
			
			var horse = saddle.parentEntity.Get(true) as RidableHorse;
			if (horse == null)
				return;
			
			if (_copters.ContainsKey(horse) == false)
				return;

			var copter = _copters[horse];
			copter.PilotInput(input, player);
		}

		private static BasePlayer AsBase(IPlayer player) => player.Object as BasePlayer;
	}
}