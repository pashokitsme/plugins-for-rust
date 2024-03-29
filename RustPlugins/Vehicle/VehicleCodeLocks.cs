﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Facepunch;
using JetBrains.Annotations;
using Network;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins.VehicleCodeLocksPlugin;
using UnityEngine;
using UnityEngine.Android;
using Extentions = Oxide.Plugins.VehicleCodeLocksPlugin.Extentions;

namespace Oxide.Plugins
{
	[Info("VehicleCodeLocks", "pashamaladec", "1.0.0")]
	[Description("Replaces standard car keys with code locks")]
	public class VehicleCodeLocks : CovalencePlugin
	{
		private Dictionary<CodeLock, int> _codeLocks;

		private const int CAR_KEY_ITEM_ID = 946662961;
		private const int CODE_LOCK_ITEM_ID = 1159991980;
		private const string CODELOCK_PREFAB = "assets/prefabs/locks/keypad/lock.code.prefab";
		private const string CODELOCK_DEPLOY_EFFECT = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";

		#region Plugin

		[UsedImplicitly]
		private void Loaded()
		{
			var watch = new Stopwatch();
			watch.Start();

			Extentions.Init(players);
			_codeLocks = LoadData();
			watch.Stop();
			Puts($"Loaded {_codeLocks.Count} cars with code lock");
			Puts($"Load took {watch.ElapsedMilliseconds} ms");
		}

		private static Dictionary<CodeLock, int> LoadData() => ModularCar.allCarsList
			.Where(car => car.carLock.HasALock && HasCodeLock(car))
			.ToDictionary(GetAttachedCodeLock, value => value.carLock.LockID);

		[UsedImplicitly]
		private void OnServerSave()
		{
			var watch = new Stopwatch();
			watch.Start();
			players.All
				.Select(pl => pl.ToBase())
				.Where(pl => pl != null)
				.Select(pl => pl.inventory.containerMain.itemList)
				.ToList()
				.ForEach(itemList =>
					itemList.RemoveAll(item => item.info.itemid == CAR_KEY_ITEM_ID && _codeLocks.ContainsValue(item.instanceData.dataInt) == false));
			watch.Stop();
			Puts($"OnServerSave processing took {watch.ElapsedMilliseconds}ms");
		}

		#endregion
		
		#region Permissions

		private static class PermissionList
		{
			public const string Auths = "vehiclecodelocks.command.auths";
			public const string Auth = "vehiclecodelocks.command.auth";
			public const string Lock = "vehiclecodelocks.command.lock";
		}
		
		#endregion
		
		#region Localization

		private static class Localization
		{
			public const string NoLock = "NoLock";
			public const string AlreadyHasLock = "AlreadyHasLock";
			public const string VehicleDied = "VehicleDied";
			public const string ShouldLookAtCar = "ShouldLookAtCar";
			public const string Locked = "Locked";
			public const string Unlocked = "Unlocked";
			public const string AlreadyAuthorized = "AlreadyAuthorized";
			public const string CantCraftKey = "CantCraftKey";
			public const string NoAuths = "NoAuths";
			public const string Authorized = "Authorized";
			public const string Deauthorized = "Deauthorized";
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[Localization.NoLock] = "This vehicle don't have a lock",
				[Localization.AlreadyHasLock] = "This vehicle already has a lock",
				[Localization.VehicleDied] = "This vehicle is died",
				[Localization.ShouldLookAtCar] = "You should look at car",
				[Localization.Locked] = "Vehicle locked. LockId is {0}",
				[Localization.Unlocked] = "Vehicle unlocked",
				[Localization.AlreadyAuthorized] = "Already authorized in {0}",
				[Localization.CantCraftKey] = "You can't craft car key",
				[Localization.NoAuths] = "No auths",
				[Localization.Authorized] = "You authorized in car {0}",
				[Localization.Deauthorized] = "You deauthorized from car {0}"
			}, this);
		}

		#endregion localization

		#region Commands

		[Command("v.auths"), Permission(PermissionList.Auths), UsedImplicitly]
		private void ShowAuths(IPlayer player, string cmd, string[] args)
		{
			PrintPlayerAuthList(player);
			PrintPlayerKeyList(player);
		}

		[Command("v.auth"), Permission(PermissionList.Auths), UsedImplicitly]
		private void ForceAuthorize(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();
			var entity = hit.GetEntity();
			ModularCar car;
			if (entity == null || entity.TryGetComponent(out car) == false)
			{
				player.Reply(GetString(Localization.ShouldLookAtCar));
				return;
			}

			if (car.carLock.HasALock == false)
			{
				player.Reply(GetString(Localization.NoLock));
				return;
			}

			var baseplayer = player.ToBase();
			Authorize(baseplayer, car);
			player.Reply(GetString(Localization.Authorized, car.carLock.LockID));
		}

		[Command("v.deauth"), Permission(PermissionList.Auth), UsedImplicitly]
		private void ForceDeauthorize(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();
			var entity = hit.GetEntity();
			ModularCar car;
			if (entity == null || entity.TryGetComponent(out car) == false)
			{
				player.Reply(GetString(Localization.ShouldLookAtCar));
				return;
			}

			if (car.carLock.HasALock == false)
			{
				player.Reply(GetString(Localization.NoLock));
				return;
			}

			var baseplayer = player.ToBase();
			Deauthorize(baseplayer, car.carLock.LockID);
			player.Reply(GetString(Localization.Deauthorized, car.carLock.LockID));
		}

		[Command("v.unlock"), Permission(PermissionList.Lock), UsedImplicitly]
		private void ForceUnlockCar(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();
			var entity = hit.GetEntity();
			ModularCar car;
			if (entity == null || entity.TryGetComponent(out car) == false)
			{
				player.Reply(GetString(Localization.ShouldLookAtCar));
				return;
			}

			if (HasCodeLock(car) == false)
				return;

			RemoveCodeLock(car);
			player.Reply(GetString(Localization.Unlocked));
		}

		[Command("v.lock"), Permission(PermissionList.Lock), UsedImplicitly]
		private void ForceLockCar(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();
			var entity = hit.GetEntity();
			ModularCar car;
			if (entity == null || entity.TryGetComponent(out car) == false)
			{
				player.Reply(GetString(Localization.ShouldLookAtCar));
				return;
			}

			if (HasCodeLock(car))
			{
				player.Reply(GetString(Localization.AlreadyHasLock));
				return;
			}

			AddCodeLock(car);
			player.Reply(GetString(Localization.Locked, car.carLock.LockID));
		}

		#endregion

		#region Hooks

		[UsedImplicitly]
		private object OnEntitySnapshot(ModularCar car, Connection connection)
		{
			if (HasCodeLock(car) && car.carLock.HasALock == false)
				RemoveCodeLock(car);
			return null;
		}

		[UsedImplicitly]
		private void OnEntityKill(ModularCar car)
		{
			var codeLock = GetAttachedCodeLock(car);
			if (codeLock == null)
				return;

			DeauthorizeAll(car.carLock.LockID);
			_codeLocks.Remove(codeLock);
		}

		[UsedImplicitly]
		private void OnEntityKill(CodeLock codeLock)
		{
			var car = GetAttachedCar(codeLock);
			if (car == null)
				return;

			if (codeLock.whitelistPlayers.Any())
				DeauthorizeAll(car.carLock.LockID);

			_codeLocks.Remove(codeLock);
			car.carLock.RemoveLock();
		}

		[UsedImplicitly]
		private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
		{
			if (code != codeLock.code)
				return null;

			var car = GetAttachedCar(codeLock);
			if (car == null)
				return null;
			
			Authorize(player, car);
			return null;
		}

		[UsedImplicitly]
		private void OnPlayerInput(BasePlayer player, InputState input)
		{
			var activeItem = player.GetActiveItem();
			if (activeItem == null || activeItem.info.itemid != CODE_LOCK_ITEM_ID)
				return;

			if (input.WasJustPressed(BUTTON.FIRE_PRIMARY) == false)
				return;

			var car = player.RaycastFromEyes().GetEntity() as ModularCar;
			if (car == null)
				return;

			if (car.IsDead())
			{
				player.ChatMessage(GetString(Localization.VehicleDied));
				return;
			}
			
			if (HasCodeLock(car))
			{
				player.ChatMessage(GetString(Localization.AlreadyHasLock));
				return;
			}
			
			AddCodeLock(car);
			activeItem.UseItem();
		}

		[UsedImplicitly]
		private object CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode)
		{
			if (_codeLocks.ContainsKey(codeLock) == false)
				return null;
			

			var car = GetAttachedCar(codeLock);
			if (car == null)
				return null;
			
			codeLock.whitelistPlayers
				.Select(userId => players.All.FirstOrDefault(pl => pl.ToBase().userID == userId).ToBase())
				.Where(target => target != null).ToList()
				.ForEach((target => Deauthorize(target, codeLock)));


			codeLock.whitelistPlayers.Clear();
			Authorize(player, car);

			if (isGuestCode)
				codeLock.guestCode = newCode;
			else
				codeLock.code = newCode;
			return null;
		}

		[UsedImplicitly]
		private void OnUserRespawned(IPlayer player)
		{
			var baseplayer = player.ToBase();
			var auths = FindPlayerAuths(baseplayer.userID);
			if (auths.Any() == false)
				return;

			foreach (var auth in auths)
				GiveKeyToPlayer(baseplayer, auth.Value);
		}

		[UsedImplicitly]
		private object OnPlayerDeath(BasePlayer player, HitInfo info)
		{
			player.inventory.containerMain.itemList.RemoveAll(item => item.info.itemid == CAR_KEY_ITEM_ID);
			player.inventory.containerMain.capacity = 24;
			return null;
		}

		[UsedImplicitly]
		private bool CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
		{
			if (bp.targetItem.itemid != 946662961)
				return true;

			itemCrafter.baseEntity.ChatMessage(GetString(Localization.CantCraftKey));
			return false;
		}

		#endregion

		#region Methods
		
		private void PrintPlayerAuthList(IPlayer player)
		{
			var baseplayer = player.ToBase();
			var auths = FindPlayerAuths(baseplayer.userID);
			player.Reply($"Current auths ({auths.Count()}):");
			if (auths.Any() == false)
			{
				player.Reply(GetString(Localization.NoAuths));
				return;
			}

			foreach (var pair in auths)
				player.Reply($"{pair.Value} : {pair.Key.code} (netId {pair.Key.net.ID})");
		}
		
		private void PrintPlayerKeyList(IPlayer player)
		{
			var baseplayer = player.ToBase();
			player.Reply("Keys (physical auths):");

			foreach (var key in baseplayer.inventory.containerMain.itemList.Where(item => item.info.itemid == CAR_KEY_ITEM_ID))
			{
				player.Reply(key.instanceData.dataInt.ToString());
			}
		}

		private List<KeyValuePair<CodeLock, int>> FindPlayerAuths(ulong userId) =>
			_codeLocks.Where(pair => pair.Key.whitelistPlayers.Contains(userId)).ToList();

		private void RemoveCodeLock(ModularCar car, int lockId = -1)
		{
			if (lockId == -1)
				lockId = car.carLock.LockID;

			DeauthorizeAll(lockId);
			var codeLock = car.GetComponentInChildren<CodeLock>();
			codeLock.Kill(BaseNetworkable.DestroyMode.Gib);
			car.carLock.RemoveLock();
			Puts($"Removed codelock for car(netID): {car.net.ID}");
		}

		private void Authorize(BasePlayer player, ModularCar car)
		{
			var codeLock = GetAttachedCodeLock(car);

			if (codeLock == null)
			{
				PrintError($"Tried to authorize {player}, but car haven't lock");
				player.ChatMessage(GetString(Localization.NoLock));
				return;
			}

			foreach (var ids in codeLock.whitelistPlayers)
			{
				player.ChatMessage(ids.ToString());
			}
			
			if (codeLock.whitelistPlayers.Contains(player.userID))
			{
				player.ChatMessage(GetString(Localization.AlreadyAuthorized, car.carLock.LockID));
				return;
			}

			codeLock.whitelistPlayers.Add(player.userID);
			GiveKeyToPlayer(player, car.carLock.LockID);
			Puts($"Authorized {player} in lock {car.carLock.LockID}");
		}

		private void Deauthorize(BasePlayer player, int lockId)
		{
			var pairs = _codeLocks.Where(pair => pair.Value == lockId).ToList();

			if (pairs.Any() == false)
			{
				PrintError($"Invalid lockId {lockId}");
				return;
			}

			var codeLock = pairs.FirstOrDefault().Key;
			if (codeLock == null)
			{
				PrintError($"Invalid code lock for lockId {lockId}");
				player.ChatMessage(GetString(Localization.NoLock));
				return;
			}

			Deauthorize(player, codeLock);
		}

		private void Deauthorize(BasePlayer player, CodeLock codeLock)
		{
			var car = GetAttachedCar(codeLock);
			if (car == null || car.carLock.HasALock == false)
			{
				PrintError($"Invalid code lock");
				player.ChatMessage(GetString(Localization.NoLock));
				return;
			}

			if (codeLock.whitelistPlayers.Contains(player.userID) == false)
			{
				PrintError($"{player} not authorized in {car.carLock.LockID}");
				return;
			}

			codeLock.whitelistPlayers.Remove(player.userID);
			var key = player.inventory.containerMain.itemList.FirstOrDefault(item =>
				item.info.itemid == CAR_KEY_ITEM_ID && item.instanceData.dataInt == car.carLock.LockID);
			if (key == null)
				return;

			player.inventory.containerMain.Remove(key);
			Puts($"Deauthorized {player} from lock {car.carLock.LockID}");
		}

		private void DeauthorizeAll(int lockId)
		{
			if (lockId == 0)
				return;

			var codeLock = _codeLocks.Where(pair => pair.Value == lockId).Select(pair2 => pair2.Key).ToList().FirstOrDefault();
			if (codeLock == null)
			{
				PrintError($"Invalid code lock for {lockId} *DeauthAll");
				return;
			}

			foreach (var userId in codeLock.whitelistPlayers)
			{
				var player = players.All.FirstOrDefault(pl => pl.ToBase().userID == userId).ToBase();
				if (player == null)
					continue;

				var key = player.inventory.containerMain.itemList.FirstOrDefault(item =>
					item.info.itemid == CAR_KEY_ITEM_ID && item.instanceData.dataInt == lockId);
				if (key == null)
					continue;

				player.inventory.containerMain.Remove(key);
				Puts($"Deauthorized {player} from lock {lockId}");
			}

			codeLock.whitelistPlayers.Clear();
		}

		private static bool HasCodeLock(ModularCar car) => GetAttachedCodeLock(car) != null;

		[CanBeNull]
		private static ModularCar GetAttachedCar(CodeLock codeLock) => codeLock.GetComponentInParent<ModularCar>();

		[CanBeNull]
		private static CodeLock GetAttachedCodeLock(ModularCar codeLock) => codeLock.GetComponentInChildren<CodeLock>();

		private void AddCodeLock(ModularCar car)
		{
			if (HasCodeLock(car))
				return;

			var target = car.GetFuelSystem().GetFuelContainer().transform;
			var position = target.transform.localPosition;
			position.x -= 0.4f;
			position.z -= 0.1f;
			var rotation = target.localRotation;
			rotation.y += 1f;
			var codeLock = GameManager.server.CreateEntity(CODELOCK_PREFAB, position, rotation) as CodeLock;

			if (codeLock == null)
				return;

			codeLock.SetParent(car);

			codeLock.Spawn();
			car.carLock.AddALock();
			_codeLocks.Add(codeLock, car.carLock.LockID);
			Effect.server.Run(CODELOCK_DEPLOY_EFFECT, codeLock.transform.position);
			codeLock.SetFlag(BaseEntity.Flags.Locked, false);
			Puts($"Created codelock for car(netID): {car.net.ID}");
		}

		private void GiveKeyToPlayer(BasePlayer player, int keyId)
		{
			var storage = player.inventory.containerMain;

			if (storage == null)
			{
				player.ChatMessage($"Storage is null");
				return;
			}

			if (storage.itemList.FirstOrDefault(x => x.info.itemid == CAR_KEY_ITEM_ID && x.instanceData.dataInt == keyId) != null)
			{
				PrintWarning($"Player alredy have key {keyId}");
				return;
			}

			var item = ItemManager.CreateByItemID(CAR_KEY_ITEM_ID);
			var data = Pool.Get<ProtoBuf.Item.InstanceData>();
			data.dataInt = keyId;
			data.ShouldPool = false;
			item.instanceData = data;
			player.inventory.containerMain.capacity++;

			item.MoveToContainer(storage, storage.capacity - 1, false);
		}

		private string GetString(string key, params object[] args) => string.Format(lang.GetMessage(key, this), args);

		#endregion
	}

	namespace VehicleCodeLocksPlugin
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

				return target ?? ifNull;
			}

			public static RaycastHit RaycastFromEyes(this IPlayer player)
			{
				return player.ToBase().RaycastFromEyes();
			}

			public static RaycastHit RaycastFromEyes(this BasePlayer player)
			{
				RaycastHit hit;
				Physics.Raycast(player.eyes.HeadRay(), out hit);
				return hit;
			}
		}
	}
}