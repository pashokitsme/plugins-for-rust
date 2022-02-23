using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Teleporter", "pashamaladec", "1.0.0")]
	public class Teleporter : CovalencePlugin
	{
		private readonly Dictionary<BasePlayer, IPendingTeleportation> _pendingTeleports = new Dictionary<BasePlayer, IPendingTeleportation>();

		#region Localization

		public static class Localization
		{
			public const string TooManyResults = "ToManyResults";
			public const string NotFound = "NotFound";
			public const string NotEnoughArguments = "NotEnoughArguments";
			public const string WaitForTp = "WaitForTp";
			public const string CancelledForDamage = "CancelledForDamage";
			//public const string PlayerCancelledForDamage = "PlayerCancelledForDamage";
			public const string Cancelled = "Cancelled";
			//public const string PlayerCancelled = "PlayerCancelled";
			public const string AlreadyHasPendingTeleportation = "AlreadyHasPendingTeleportation";
			public const string HasNotPendingTeleport = "HasNotPendingTeleport";
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[Localization.TooManyResults] = "<color=red> »</color> Найдено несколько игроков:\n{0}",
				[Localization.NotEnoughArguments] = "<color=red> »</color> Нужно ввести имя игрока.",
				[Localization.NotFound] = "<color=red> »</color> Игрок {0} не найден.",
				[Localization.WaitForTp] = "<color=green> »</color> Телепорт к {0} через {1} секунд.",
				[Localization.CancelledForDamage] = "<color=red> »</color> Телепорт отменён из-за получения урона.",
				//[Localization.PlayerCancelledForDamage] = "<color=red> »</color> {0} отменил телепорт к тебе из-за получения урона.",
				[Localization.Cancelled] = "<color=red> »</color> Телепорт отменён.",
				//[Localization.PlayerCancelled] = "<color=green> »</color> {0} отменил телепорт к тебе.",
				[Localization.AlreadyHasPendingTeleportation] = "<color=red> »</color> Ты уже телепортируешься.",
				[Localization.HasNotPendingTeleport] = "<color=red> »</color> Ты не телепортируешься.",
			}, this);
		}

		#endregion

		#region Commands

		[Command("tp"), UsedImplicitly]
		private void TeleportCommand(IPlayer caller, string cmd, string[] args)
		{
			var player = AsBase(caller);
			BasePlayer target;

			if (HasPendingTeleportion(player))
			{
				player.ChatMessage(GetString(Localization.AlreadyHasPendingTeleportation));
				return;
			}

			if (TryFindPlayer(player, args, out target) == false)
				return;

			ScheduleTeleport(player, target);
		}

		[Command("tpc"), UsedImplicitly]
		private void CancelTeleport(IPlayer caller, string cmd, string[] args)
		{
			var player = AsBase(caller);
			if (HasPendingTeleportion(player) == false)
			{
				player.ChatMessage(GetString(Localization.HasNotPendingTeleport));
				return;
			}

			CancelTeleport(_pendingTeleports[player]);
			player.ChatMessage(GetString(Localization.Cancelled));
		}

		#endregion

		#region Hooks

		[UsedImplicitly]
		private object OnEntityTakeDamage(BasePlayer player, HitInfo info)
		{
			if (_pendingTeleports.ContainsKey(player) == false)
				return null;

			CancelTeleport(_pendingTeleports[player]);
			player.ChatMessage(GetString(Localization.CancelledForDamage));
			
			return null;
		}

		#endregion

		#region Methods

		private void CancelTeleport(IPendingTeleportation teleportation)
		{
			teleportation.Cancel();
			_pendingTeleports.Remove(teleportation.From);
		}

		public bool HasPendingTeleportion(BasePlayer player) => _pendingTeleports.ContainsKey(player);
		
		public bool TryGetPendingTeleportion(BasePlayer player, out IPendingTeleportation teleportation)
		{
			teleportation = _pendingTeleports[player];
			return teleportation != null;
		}

		public void ScheduleTeleport(BasePlayer player, BasePlayer other, float delaySeconds = 5f)
		{
			player.ChatMessage(GetString(Localization.WaitForTp, other.displayName, delaySeconds));

			var callback = new Action(() =>
			{
				if (HasPendingTeleportion(player))
					_pendingTeleports.Remove(player);

				TeleportImmediately(player, other.transform.position);
			});

			_pendingTeleports.Add(player, new P2PTeleportation(player, other, callback, delaySeconds));
		}

		public void ScheduleTeleport(BasePlayer player, Vector3 position, float delaySeconds = 5f)
		{
			var callback = new Action(() =>
			{
				if (HasPendingTeleportion(player))
					_pendingTeleports.Remove(player);

				TeleportImmediately(player, position);
			});

			_pendingTeleports.Add(player, new Teleportation(player, position, callback, delaySeconds));
		}

		private static void TeleportImmediately(BasePlayer player, Vector3 position)
		{
			player.EndLooting();
			player.UpdateActiveItem(0);
			player.RemoveFromTriggers();
			player.Teleport(position);
			player.SendEntityUpdate();
			player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
			player.ClientRPCPlayer(null, player, "StartLoading");
		}

		public bool TryFindPlayer(BasePlayer caller, string[] args, out BasePlayer target)
		{
			target = null;
			var targetName = string.Join(" ", args);
			if (args.Length == 0)
			{
				caller.ChatMessage(GetString(Localization.NotEnoughArguments));
				return false;
			}

			var targets = players.FindPlayers(targetName).Where(p => p.IsConnected).ToArray();

			if (targets.Length == 0)
			{
				caller.ChatMessage(GetString(Localization.NotFound, targetName));
				return false;
			}

			if (targets.Length > 1)
			{
				var builder = new StringBuilder();
				foreach (var pl in targets)
				{
					builder.AppendLine($"\t{pl.Name}");
				}

				caller.ChatMessage(GetString(Localization.TooManyResults, builder.ToString()));
				return false;
			}

			target = AsBase(targets[0]);
			return true;
		}
		
		#endregion

		#region Classes

		public interface IPendingTeleportation
		{
			BasePlayer From { get; }
			Vector3 To { get; }
			void Cancel();
		}

		public class Teleportation : IPendingTeleportation
		{
			private static readonly Oxide.Core.Libraries.Timer _timerFactory = Interface.Oxide.GetLibrary<Oxide.Core.Libraries.Timer>("Timer");
			
			public BasePlayer From { get; }
			public Vector3 To {get;}
			
			private readonly Timer _timer;

			public Teleportation(BasePlayer from, Vector3 to, Action onReady, float delay = 5f)
			{
				From = from;
				To = to;
				_timer = new Timer(_timerFactory.Once(delay, onReady));
			}
			
			protected Teleportation(BasePlayer from, Action onReady, float delay = 5f)
			{
				From = from;
				_timer = new Timer(_timerFactory.Once(delay, onReady));
			}

			public void Cancel()
			{
				_timer.Destroy();
			}
		}
		
		public class P2PTeleportation : Teleportation, IPendingTeleportation 
		{
			public new Vector3 To => _to.transform.position;
			private readonly BasePlayer _to;

			public P2PTeleportation(BasePlayer from, BasePlayer to, Action onReady, float delay = 5f) : base(from, onReady, delay)
			{
				_to = to;
			}
		}

		#endregion

		#region Extentions

		private string GetString(string key, params object[] args) => string.Format(lang.GetMessage(key, this), args);
		private static BasePlayer AsBase(IPlayer player) => player.Object as BasePlayer;

		#endregion
	}
}