// Requires: Teleporter

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using JetBrains.Annotations;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("TeleporterRequests", "pashamaladec", "1.0.0")]
	public class TeleporterRequests : CovalencePlugin
	{
		private Teleporter _teleporter;

		private readonly Dictionary<BasePlayer, HashSet<IncomingRequest>> _requests = new Dictionary<BasePlayer, HashSet<IncomingRequest>>();
		private readonly HashSet<BasePlayer> _hasOutcomingRequest = new HashSet<BasePlayer>();

		#region Init

		private void Loaded()
		{
			_teleporter = Manager.GetPlugin("Teleporter") as Teleporter;
			if (_teleporter == null)
				RaiseError("Teleporter is missing");
		}

		#endregion

		#region Localization

		private class Localization
		{
			public const string HasRequest = "TargetHasRequest";
			public const string CantAcceptWhileTeleporting = "CantAcceptWhileTeleporting";
			public const string NoneRequestForAccept = "NoneRequestForAccept";
			public const string Requested = "Requested";
			public const string IncomingRequest = "IncomingRequest";
			public const string YouAccepted = "Accepted";
			public const string Cancelled = "Cancelled";
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[Localization.HasRequest] = "<color=red> »</color> Ты уже имеешь активный запрос на телепортацию.",
				[Localization.CantAcceptWhileTeleporting] = "<color=red> »</color> Ты не можешь принять запрос, пока телепортируешься сам.",
				[Localization.NoneRequestForAccept] = "<color=red> »</color> Нет запросов.",
				[Localization.Requested] = "<color=green> »</color> Отправлен запрос на телепортацию к {0}.",
				[Localization.IncomingRequest] = "<color=green> »</color> Входящий запрос на телепортацию от {0}, введи /tpa для принятия.",
				[Localization.YouAccepted] = "<color=green> »</color> Ты принял запрос на телепортацию от {0}. Он телепортируется через {1}s.",
				[Localization.Cancelled] = "<color=red> »</color> cancelled.",
			}, this);
		}

		#endregion

		#region Commands

		// todo сделать таймаут
		[Command("tpr"), UsedImplicitly]
		private void TeleportRequest(IPlayer caller, string cmd, string[] args)
		{
			var player = AsBase(caller);
			if (_teleporter.HasPendingTeleportion(player))
			{
				player.ChatMessage(GetString(Teleporter.Localization.AlreadyHasPendingTeleportation));
				return;
			}

			if (HasOutcomingRequest(player))
			{
				player.ChatMessage(GetString(Localization.HasRequest));
				return;
			}

			BasePlayer target;
			if (_teleporter.TryFindPlayer(player, args, out target) == false)
				return;
			
			MakeRequest(target, player);
		}

		[Command("tpa"), UsedImplicitly]
		private void AcceptTeleportRequest(IPlayer caller, string cmd, string[] args)
		{
			var player = AsBase(caller);
			if (_teleporter.HasPendingTeleportion(player))
			{
				player.ChatMessage(GetString(Localization.CantAcceptWhileTeleporting));
				return;
			}
			
			AcceptLast(player);
		}

		#endregion

		#region Methods

		private bool HasOutcomingRequest(BasePlayer player) => _hasOutcomingRequest.Contains(player);
		private bool HasIncomingRequest(BasePlayer player) => _requests.ContainsKey(player) && _requests[player].Count > 0;

		private void AcceptLast(BasePlayer receiver)
		{
			if (HasIncomingRequest(receiver) == false)
			{
				receiver.ChatMessage(GetString(Localization.NoneRequestForAccept));
				return;
			}

			var request = _requests[receiver].Last();
			request.Timer.Destroy();
			_hasOutcomingRequest.Remove(request.Player);
			var delay = 5f;
			_teleporter.ScheduleTeleport(request.Player, receiver, delay);
			receiver.ChatMessage(GetString(Localization.YouAccepted, request.Player.displayName, delay));
		}

		private void MakeRequest(BasePlayer receiver, BasePlayer requester)
		{
			if (HasOutcomingRequest(requester))
			{
				requester.ChatMessage(GetString(Localization.HasRequest));
				return;
			}

			_hasOutcomingRequest.Add(requester);
			if (_requests.ContainsKey(receiver) == false)
				_requests.Add(receiver, new HashSet<IncomingRequest>());

			var requests = _requests[receiver];
			
			var request = new IncomingRequest(requester, timer.Once(15f, () =>
			{
				_hasOutcomingRequest.Remove(requester);
				requests.RemoveWhere(r => r.Player == requester);
				Puts($"TIMEOUT: {requester.displayName}");
			}));

			requests.Add(request);

			requester.ChatMessage(GetString(Localization.Requested, receiver.displayName));
			receiver.ChatMessage(GetString(Localization.IncomingRequest, requester.displayName));
		}

		#endregion

		#region Classes

		private struct IncomingRequest
		{
			public BasePlayer Player;
			public Timer Timer;

			public IncomingRequest(BasePlayer player, Timer timer)
			{
				Player = player;
				Timer = timer;
			}
		}

		#endregion

		#region Extentions

		private string GetString(string key, params object[] args) => string.Format(lang.GetMessage(key, this), args);
		private static BasePlayer AsBase(IPlayer player) => player.Object as BasePlayer;

		#endregion
	}
}