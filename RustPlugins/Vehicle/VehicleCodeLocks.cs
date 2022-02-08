using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Markup;
using Facepunch;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using Network;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins.VehicleCodeLocksPlugin;
using Rust.Modular;
using UnityEngine;
using VLB;
using Extentions = Oxide.Plugins.VehicleCodeLocksPlugin.Extentions;

namespace Oxide.Plugins
{
	[Info("Vehicle Code Locks", "pashamaladec", "0.1")]
	public class VehicleCodeLocks : CovalencePlugin
	{
		private readonly string _saveFileName = "VehicleAuthorizations";
		private Dictionary<ulong, HashSet<Authorization>> _authorizations;
		private readonly HashSet<ModularCar> _lockedCars = new HashSet<ModularCar>();
		private readonly int _carKeyItemId = 946662961;

		#region Plugin

		[UsedImplicitly]
		private void Loaded()
		{
			Extentions.Init(players);
			_authorizations = LoadData();

			foreach (var car in ModularCar.allCarsList)
			{
				if (car.carLock.HasALock)
				{
					if(HasCodeLock(car) == false)
						PlaceCodeLockPrefab(car);

					_lockedCars.Add(car);
				}
			}
		}

		private void SaveData()
		{
			var data = new Dictionary<ulong, HashSet<SerializedAuthorization>>();
			foreach (var user in _authorizations)
			{
				data.Add(user.Key, new HashSet<SerializedAuthorization>());
				foreach (var auth in user.Value)
					data[user.Key].Add(auth.Serialize());
			}

			Interface.Oxide.DataFileSystem.WriteObject(_saveFileName, data);
		}

		private Dictionary<ulong, HashSet<Authorization>> LoadData()
		{
			var data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, HashSet<SerializedAuthorization>>>(_saveFileName);
			var auths = new Dictionary<ulong, HashSet<Authorization>>();

			if (data == null)
				return auths;

			Puts($"Loading {data.Count} saved user auths");
			
			foreach (var user in data)
			{
				auths.Add(user.Key, new HashSet<Authorization>());
				foreach (var auth in user.Value)
				{
					var deserialized = auth.Deserialize();

					if (deserialized != null)
						auths[user.Key].Add(auth.Deserialize());
					else
						Puts($"Error while parse {auth.LockId} {auth.CodeLockNetId} for user {user.Key}");
				}
			}

			return auths;
		}

		[UsedImplicitly]
		private void OnServerSave()
		{
			SaveData();
			Puts($"Saved with {_authorizations.Count} auths");
		}

		#endregion

		#region Localization

		private static class Localization
		{
			public static readonly string NoLock = Guid.NewGuid().ToString();
			public static readonly string Locked = Guid.NewGuid().ToString();
			public static readonly string Unlocked = Guid.NewGuid().ToString();
			public static readonly string AlreadyAuthorized = Guid.NewGuid().ToString();
			public static readonly string Authorized = Guid.NewGuid().ToString();
			public static readonly string Deauthorized = Guid.NewGuid().ToString();
			public static readonly string CantCraftKey = Guid.NewGuid().ToString();
			public static readonly string NoAuths = Guid.NewGuid().ToString();
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[Localization.NoLock] = "This vehicle don't have a lock",
				[Localization.Locked] = "Vehicle locked. LockId is {0}",
				[Localization.Unlocked] = "Vehicle unlocked",
				[Localization.AlreadyAuthorized] = "Already authorized in {0}",
				[Localization.Authorized] = "Authorized in car: {0}",
				[Localization.Deauthorized] = "Deauthorized from car: {0}",
				[Localization.CantCraftKey] = "You can't craft car key",
				[Localization.NoAuths] = "No auths"
			}, this);
		}

		#endregion localization

		#region Commands

		[Command("test"), UsedImplicitly]
		private void Test(IPlayer player, string cmd, string[] args)
		{
			GetMyAuths(player, cmd, args);
			GetKeys(player, cmd, args);
			player.Reply("Locked vehicles");
			foreach (var car in _lockedCars)
			{
				player.Reply(car.carLock.LockID.ToString());
			}
			player.Reply("Auth Id - CodeLock ");
			foreach (var user in _authorizations)
			{
				foreach (var auth in user.Value)
				{
					player.Reply($"{user.Key}: {auth.LockId} {auth.CodeLock.net.ID}");
				}
			}
		}
		
		[Command("auth"), UsedImplicitly]
		private void AuthorizeMe(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();

			ModularCar car;
			if (hit.GetEntity().TryGetComponent(out car) == false)
				return;

			if (car.carLock.HasALock == false)
			{
				player.Reply(GetString(Localization.NoLock));
				return;
			}

			var baseplayer = player.ToBase();
			Authorize(baseplayer, car);
		}

		[Command("deauth"), UsedImplicitly]
		private void DeauthorizeMe(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();

			ModularCar car;
			if (hit.GetEntity().TryGetComponent(out car) == false)
				return;

			if (car.carLock.HasALock == false)
			{
				player.Reply(GetString(Localization.NoLock));
				return;
			}

			var baseplayer = player.ToBase();
			Deauthorize(baseplayer, car.carLock.LockID);
		}

		[Command("auths"), UsedImplicitly]
		private void GetMyAuths(IPlayer player, string cmd, string[] args)
		{
			var baseplayer = player.ToBase();
			player.Reply("Current logical auths:");
			if (_authorizations.ContainsKey(baseplayer.userID) == false)
			{
				player.Reply(GetString(Localization.NoAuths));
				return;
			}

			foreach (var auth in _authorizations[baseplayer.userID])
			{
				player.Reply(auth.LockId.ToString());
			}
		}

		[Command("carid"), UsedImplicitly]
		private void GetCarId(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();

			ModularCar car;
			if (hit.GetEntity().TryGetComponent(out car) == false)
				return;

			if (car.carLock.HasALock == false)
			{
				player.Reply(GetString(Localization.NoLock));
				return;
			}

			player.Reply(car.carLock.LockID.ToString());
		}

		[Command("keys"), UsedImplicitly]
		private void GetKeys(IPlayer player, string cmd, string[] args)
		{
			var baseplayer = player.ToBase();
			player.Reply("Keys (physical auths):");

			foreach (var key in baseplayer.inventory.containerMain.itemList.Where(item => item.info.itemid == _carKeyItemId))
			{
				player.Reply(key.instanceData.dataInt.ToString());
			}
		}

		[Command("unlock"), UsedImplicitly]
		private void ForceUnlockCar(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();
			ModularCar car;
			if (hit.GetEntity().TryGetComponent(out car) == false)
				return;

			if (_lockedCars.Contains(car) == false)
				return;
			
			RemoveCodeLock(car);
			car.carLock.RemoveLock();
			player.Reply(GetString(Localization.Unlocked));
		}

		[Command("lock"), UsedImplicitly]
		private void ForceLockCar(IPlayer player, string cmd, string[] args)
		{
			var hit = player.RaycastFromEyes();
			ModularCar car;
			if (hit.GetEntity().TryGetComponent(out car) == false)
				return;

			car.carLock.AddALock();
			PlaceCodeLockPrefab(car);
			player.Reply(GetString(Localization.Locked, car.carLock.LockID));
		}

		#endregion

		#region Hooks

		[UsedImplicitly]
		private void OnEntityKill(BaseNetworkable entity)
		{
			var car = entity as ModularCar;
			if (car == null)
				return;
			
			if (_lockedCars.Contains(car))
				_lockedCars.Remove(car);
				
			DeauthorizeAll(car);
		}

		[UsedImplicitly]
		private object OnEntitySnapshot(BaseNetworkable entity, Connection connection)
		{
			var car = entity as ModularCar;
			if (car == null)
				return null;

			if (_lockedCars.Contains(car) && car.carLock.HasALock == false)
				RemoveCodeLock(car);
			
			return null;
		}
		
		[UsedImplicitly]
		private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
		{
			if (code != codeLock.code)
				return null;

			var parent = codeLock.parentEntity.Get(true);
			if (parent != null && parent is ModularCar)
				Authorize(player, (ModularCar) parent);

			return null;
		}
		
		[UsedImplicitly]
		private object CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode)
		{
			var parent = codeLock.parentEntity.Get(true);
			if (parent != null && parent is ModularCar)
			{
				var car = parent as ModularCar;
				var lockId = car.carLock.LockID;

				foreach (var auth in _authorizations)
				{
					var t = players.FindPlayer(auth.Key.ToString()).Object as BasePlayer;
					Deauthorize(t, lockId);
					car.carLock.AddALock();
				}

				Authorize(player, car);
			}

			return null;
		}

		[UsedImplicitly]
		private void OnUserRespawned(IPlayer player)
		{
			var baseplayer = player.ToBase();
			if (_authorizations.ContainsKey(baseplayer.userID))
			{
				foreach (var auth in _authorizations[baseplayer.userID])
				{
					GiveKeyToPlayer(baseplayer, auth.LockId);
				}
			}
		}

		[UsedImplicitly]
		private object OnPlayerDeath(BasePlayer player, HitInfo info)
		{
			player.inventory.containerMain.itemList.RemoveAll(item => item.info.itemid == _carKeyItemId);
			player.inventory.containerMain.capacity = 24;
			return null;
		}
		
		[UsedImplicitly]
		private bool CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
		{
			if (bp.targetItem.itemid == 946662961)
			{
				itemCrafter.baseEntity.ChatMessage(GetString(Localization.CantCraftKey));
				return false;
			}

			return true;
		}

		#endregion

		#region Methods

		private void Authorize(BasePlayer player, ModularCar car)
		{
			if (_authorizations.ContainsKey(player.userID))
			{
				if (_authorizations[player.userID].Select(x => x.LockId).Contains(car.carLock.LockID))
				{
					player.ChatMessage(GetString(Localization.AlreadyAuthorized, car.carLock.LockID));
					return;
				}
			}

			GiveKeyToPlayer(player, car.carLock.LockID);

			if (_authorizations.ContainsKey(player.userID) == false)
				_authorizations.Add(player.userID, new HashSet<Authorization>());

			var codeLock = car.GetComponentInChildren<CodeLock>(false);

			if (codeLock.whitelistPlayers.Contains(player.userID) == false)
				codeLock.whitelistPlayers.Add(player.userID);

			_authorizations[player.userID].Add(new Authorization(car.carLock.LockID, codeLock));
			player.ChatMessage(GetString(Localization.Authorized, car.carLock.LockID));
		}

		private void Deauthorize(BasePlayer player, int lockId)
		{
			if (_authorizations.ContainsKey(player.userID))
			{
				if (lockId == 0)
					return;

				if (_authorizations[player.userID].Select(x => x.LockId).Contains(lockId))
				{
					var auth = _authorizations[player.userID].First(x => x.LockId == lockId);
					player.inventory.containerMain.capacity--;
					_authorizations[player.userID].Remove(auth);

					auth.CodeLock.whitelistPlayers.Remove(player.userID);
					player.ChatMessage(GetString(Localization.Deauthorized, lockId));
				}
			}

			var key = player.inventory.containerMain.itemList.FirstOrDefault(item => item.info.itemid == _carKeyItemId && item.instanceData.dataInt == lockId);
			if (key == null)
				return;

			player.inventory.containerMain.Remove(key);
		}

		private void DeauthorizeAll(ModularCar car)
		{
			var authorized = _authorizations
				.Where(auth => auth.Value.Select(x => x.LockId).Contains(car.carLock.LockID))
				.Select(x => x.Key).ToList();

			foreach (var userId in authorized)
			{
				var player = players.All.FirstOrDefault(pl => pl.ToBase().userID == userId).ToBase();
				if(player == null)
					continue;
				
				Deauthorize(player, car.carLock.LockID);
			}
		}
		
		private void RemoveCodeLock(ModularCar car)
		{
			var codeLock = car.GetComponentInChildren<CodeLock>();
			DeauthorizeAll(car);
			codeLock.Kill(BaseNetworkable.DestroyMode.Gib);
			_lockedCars.Remove(car);
			Puts($"Removing lock {car.carLock.LockID}");
		}

		private bool HasCodeLock(ModularCar car) => car.GetComponentInChildren<CodeLock>() != null;

		private void PlaceCodeLockPrefab(ModularCar car)
		{
			if(_lockedCars.Contains(car))
				PrintError($"Already have lock");
			
			var target = car.GetFuelSystem().GetFuelContainer().transform;
			var position = target.transform.localPosition;
			position.x -= .4f;
			position.z -= .1f;
			var rotation = target.localRotation;
			rotation.y += 1f;
			var entity = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", position, rotation);

			var code = entity.GetComponent<CodeLock>();
			code.SetParent(car);
			code.code = "0000";
			code.hasCode = true;

			code.Spawn();
			Effect.server.Run("assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab", code.transform.position);
			code.SetFlag(BaseEntity.Flags.Locked, true);

			_lockedCars.Add(car);
		}

		private void GiveKeyToPlayer(BasePlayer player, int keyId)
		{
			var storage = player.inventory.containerMain;
			
			if (storage == null)
			{
				player.ChatMessage($"Storage is null");
				return;
			}

			if (storage.itemList.FirstOrDefault(x => x.instanceData != null && x.instanceData.dataInt == keyId) != null)
			{
				PrintWarning($"Player alredy have key {keyId}");
				return;
			}

			var item = ItemManager.CreateByItemID(_carKeyItemId);
			var data = Pool.Get<ProtoBuf.Item.InstanceData>();
			data.dataInt = keyId;
			data.ShouldPool = false;
			item.instanceData = data;
			player.inventory.containerMain.capacity++;

			item.MoveToContainer(storage, storage.capacity - 1, false);
		}

		private string GetString(string key, params object[] args)
		{
			return string.Format(lang.GetMessage(key, this), args);
		}

		#endregion

		#region Classes

		private class Authorization
		{
			public int LockId { get; }
			public CodeLock CodeLock { get; }

			public Authorization(int lockId, CodeLock codeLock)
			{
				LockId = lockId;
				CodeLock = codeLock;
			}

			public SerializedAuthorization Serialize() => new SerializedAuthorization(LockId, CodeLock.net.ID);
		}
		
		private class SerializedAuthorization
		{
			public int LockId { get; }
			public uint CodeLockNetId { get; }

			public SerializedAuthorization(int lockId, uint codeLockNetId)
			{
				LockId = lockId;
				CodeLockNetId = codeLockNetId;
			}

			public Authorization Deserialize()
			{
				var codeLock = BaseNetworkable.serverEntities.Find(CodeLockNetId);
				if (codeLock == null)
					return null;

				return new Authorization(LockId, (CodeLock) codeLock);
			}
		}

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