using System.Collections.Generic;
using JetBrains.Annotations;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins.ProjectileAdditionsPlugin.Extentions;

namespace Oxide.Plugins
{
	[Info("Fun.ProjectileAdditions", "pashamaladec", "1")]
	public class ProjectileAdditions : CovalencePlugin
	{
		private readonly Dictionary<ulong, string> _using = new Dictionary<ulong, string>();

		private void Init() => Extentions.Init(players);

		[Command("projectile"), Permission("projectileadditions.entities.use"), UsedImplicitly]
		private void OnProjectileAdditionCommand(IPlayer player, string cmd, string[] args)
		{
			var baseplayer = player.ToBase();
			if (args.Length < 1)
			{
				if (_using.ContainsKey(baseplayer.userID))
				{
					_using.Remove(baseplayer.userID);
					player.Reply("Addition removed");
					return;
				}

				player.Reply("Need entity name (eg. assets/prefabs/ammo/rocket/rocket_basic.prefab)");
				return;
			}

			var prefab = args[0];

			if (GameManager.server.FindPrefab(prefab) == null)
			{
				player.Reply($"Prefab {prefab} doesn't exists");
				return;
			}

			player.Reply($"Selected prefab {prefab}");

			if (_using.ContainsKey(baseplayer.userID))
				_using[baseplayer.userID] = prefab;
			else
				_using.Add(baseplayer.userID, prefab);
		}

		[Command("projectile.selected"), Permission("projectileadditions.entities.use"), UsedImplicitly]
		private void OnProjectileAdditionSelectedCommand(IPlayer player, string cmd, string[] args)
		{
			var baseplayer = player.ToBase();
			if (_using.ContainsKey(baseplayer.userID) == false)
			{
				player.Reply("No prefab selected");
				return;
			}

			player.Reply($"Selected: {_using[baseplayer.userID]}");
		}

		private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
		{
			if (_using.ContainsKey(attacker.userID) == false)
				return;

			var prefab = _using[attacker.userID];

			var entity = GameManager.server.CreateEntity(prefab, info.HitPositionWorld);
			entity.Spawn();
			ServerProjectile projectile;

			if (entity.TryGetComponent(out projectile))
			{
				projectile.radius = 100f;
				projectile.scanRange = 100f;
			}
		}
	}

	namespace ProjectileAdditionsPlugin.Extentions
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
		}
	}
}