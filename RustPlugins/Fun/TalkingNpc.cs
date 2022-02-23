using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using VLB;
using Debug = System.Diagnostics.Debug;

namespace Oxide.Plugins
{
	[Info("Talking NPC", "pashamaladec", "1.0.0")]
	public class TalkingNpc : CovalencePlugin
	{
		private const string NPC_PREFAB = "assets/prefabs/npc/bandit/missionproviders/missionprovider_stables_a.prefab";

		private HashSet<NPCTalking> _bots = new HashSet<NPCTalking>();
		
		#region Methods

		private ConversationData[] ConfigureConversationData()
		{
			var data = ScriptableObject.CreateInstance<ConversationData>();
			data.hideFlags = HideFlags.None;
			data.name = "123name";
			data.providerNameTranslated = new Translate.Phrase("1234", "phrase");
			data.shortname = "my_conversation";
			data.speeches = new[]
			{
				new ConversationData.SpeechNode
				{
					shortname = "intro",
					statementLocalized = new Translate.Phrase("localized", "eng"),
					responses = new[]
					{
						new ConversationData.ResponseNode
						{
							responseTextLocalized = new Translate.Phrase("response localized", "response eng"),
							conditions = new[]
							{
								new ConversationData.ConversationCondition
								{
									conditionType = ConversationData.ConversationCondition.ConditionType.NONE,
									conditionAmount = 0,
									inverse = false,
									failedSpeechNode = "fail"
								}
							},
							actionString = "n1",
							resultingSpeechNode = "intro"
						}
					},
					nodePosition = new Vector2(0, 0)
				}
			};

			return new[] {data};
		}

		private NPCTalking.NPCConversationResultAction[] ConfigureResults()
		{
			var data = new NPCTalking.NPCConversationResultAction
			{
				action = "n1",
				scrapCost = 0,
				broadcastMessage = "msg",
				broadcastRange = 0
			};

			return new[] {data};
		}

		private NPCTalking SpawnNpc(Vector3 position)
		{
			var entity = _manager.CreateEntity(NPC_PREFAB, position);
			entity.Spawn();
			return entity.GetOrAddComponent<NPCTalking>();
		}

		#endregion

		#region Commands

		[Command("talk")]
		private void SpawnTalkingNpc(IPlayer pl, string cmd, string[] args)
		{
			RaycastHit hit;
			var player = AsBase(pl);
			if (RaycastFromEyes(player, out hit) == false)
			{
				player.ChatMessage("Иди нахуй");
				return;
			}

			var npc = SpawnNpc(hit.point);
		}

		#endregion

		#region Hooks

		private object OnNpcConversationStart(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData)
		{
			conversationData.providerNameTranslated = new Translate.Phrase("_asfg", "test");
			npcTalking.conversations = new [] {conversationData};
			npcTalking.SendNetworkUpdateImmediate(true);
			
			return null;
		}

		#endregion

		#region Extensions

		private static GameManager _manager => GameManager.server;
		private static bool RaycastFromEyes(BasePlayer player, out RaycastHit hit) => Physics.Raycast(player.eyes.HeadRay(), out hit);
		private static BasePlayer AsBase(IPlayer player) => player.Object as BasePlayer;

		#endregion
	}
}