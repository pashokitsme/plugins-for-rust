using JetBrains.Annotations;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Dance", "pashamaladec", "0.0.1")]
    public class Dance : CovalencePlugin
    {
        [Command("dance"), UsedImplicitly]
        private void OnDanceCommand(IPlayer player, string command, string[] args)
        {
            var baseplayer = player.Object as BasePlayer;

            var gesture = ScriptableObject.CreateInstance<GestureConfig>();
            gesture.forceUnlock = true;
            gesture.gestureId = 478760625;
            baseplayer.Server_StartGesture(gesture);
        }
    }
    

}