using MPlugin.Untruned.MPlugin.API;
using MPlugin.Untruned.MPlugin.Core;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MP.PlayerChatHealth
{
    [MPluginInfo(PluginName = "玩家聊天血量显示", ContactInformation = "QQ: 2472285384", AuthorName = "Matt")]
    public class Beta : MPlugin<Config>
    {
        [MPlugin(Type = EMPluginStateType.Load)]
        public void Init()
        {
            ChatManager.onServerSendingMessage += onServerSendingMessage;
        }

        private void onServerSendingMessage(ref string text, ref Color color, SteamPlayer fromPlayer, SteamPlayer toPlayer, EChatMode mode, ref string iconURL, ref bool useRichTextFormatting)
        {
            if (fromPlayer == null) return;
            MPlayer player = MPlayer.GetMPlayer(fromPlayer) ?? null;
            if (player == null) return;
            if (player != null && mode == EChatMode.GLOBAL && !text.Substring(0, 1).Equals("/"))
            {
                useRichTextFormatting = true;
                if (!text.Contains("[HP:")) text = $"<color={Configuration.Color}>[HP:{player.Player.life.health}] </color>" + text;
            }
        }


        [MPlugin(Type = EMPluginStateType.Unload)]
        public void Exit()
        {
            ChatManager.onServerSendingMessage -= onServerSendingMessage;
        }
    }

    public class Config : IMPluginConfig
    {
        public void LoadDefault()
        {
            Color = "#FF0000";
        }
        public string Color { get; set; }
    }
}
