using MPlugin.Untruned.MPlugin.API;
using MPlugin.Untruned.MPlugin.Core;
using MPlugin.Untruned.MPlugin.Unturned;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEngine;

namespace MP.HandsNotDrop
{
    [MPluginInfo(PluginName = "手物品栏不掉落", ContactInformation = "QQ: 2472285384", AuthorName = "Matt")]
    public class Beta : MPlugin<Config>
    {
        /// <summary>
        /// 玩家手上物品栏的物品数据信息
        /// </summary>
        public Dictionary<CSteamID, List<ItemInfo>> plist = new Dictionary<CSteamID, List<ItemInfo>>();

        public readonly byte HandsPageIndex = 2;

        [MPlugin(Type = EMPluginStateType.Load)]
        public void Init()
        {
            MEvents.OnPlayerJoinServer += MEvents_OnPlayerJoinServer;
            MEvents.OnPlayerExitServer += MEvents_OnPlayerExitServer; ;
            PlayerLife.OnRevived_Global += OnRevived_Global;
            PlayerLife.OnPreDeath += PlayerLife_OnPreDeath;
        }

        private void MEvents_OnPlayerExitServer(MPlayer player)
        {
            plist.Remove(player.SteamId);
        }

        private void OnRevived_Global(PlayerLife life)
        {
            MPlayer player = MPlayer.GetMPlayer(life.player);
            foreach (var handsConfig in Configuration.PermissionsConfig)
            {
                if (MTool.Instance.HasPermission(player, handsConfig.Permission, out _) &&
                    (player.Player.inventory.items[HandsPageIndex].width * player.Player.inventory.items[HandsPageIndex].height) < (handsConfig.X * handsConfig.Y))
                {
                    player.Player.inventory.items[HandsPageIndex].resize(handsConfig.X, handsConfig.Y);
                    player.Player.inventory.save();
                }
            }
            foreach (ItemInfo itemInfo in this.plist[player.SteamId])
            {
                Item item = new Item(itemInfo.id, itemInfo.amount, itemInfo.quality, itemInfo.state);
                player.Player.inventory.tryAddItem(item, itemInfo.x, itemInfo.y, HandsPageIndex, itemInfo.rot);
                player.Player.inventory.save();
            }
        }

        private void PlayerLife_OnPreDeath(PlayerLife obj)
        {
            MPlayer player = MPlayer.GetMPlayer(obj.player);
            List<ItemInfo> list = new List<ItemInfo>();
            foreach (ItemJar itemJar in player.Player.inventory.items[HandsPageIndex].items)
            {
                list.Add(new ItemInfo
                {
                    id = itemJar.item.id,
                    durability = itemJar.item.durability,
                    quality = itemJar.item.quality,
                    rot = itemJar.rot,
                    state = itemJar.item.state,
                    x = itemJar.x,
                    y = itemJar.y,
                    amount = itemJar.item.amount
                });
            }
            plist[player.SteamId] = list;
            foreach (ItemInfo itemInfo in list)
            {
                player.Player.inventory.items[HandsPageIndex].removeItem(player.Player.inventory.items[HandsPageIndex].getIndex(itemInfo.x, itemInfo.y));
            }
        }

        private void MEvents_OnPlayerJoinServer(MPlayer player)
        {
            foreach (var handsConfig in Configuration.PermissionsConfig)
            {
                if (MTool.Instance.HasPermission(player, handsConfig.Permission, out _) &&
                    (player.Player.inventory.items[HandsPageIndex].width * player.Player.inventory.items[HandsPageIndex].height) < (handsConfig.X * handsConfig.Y))
                {
                    player.Player.inventory.items[HandsPageIndex].resize(handsConfig.X, handsConfig.Y);
                    player.Player.inventory.save();
                }
            }

            plist.Add(player.SteamId, new List<ItemInfo>());
            List<ItemInfo> list = new List<ItemInfo>();
            foreach (ItemJar itemJar in player.Player.inventory.items[HandsPageIndex].items)
            {
                list.Add(new ItemInfo
                {
                    id = itemJar.item.id,
                    durability = itemJar.item.durability,
                    quality = itemJar.item.quality,
                    rot = itemJar.rot,
                    state = itemJar.item.state,
                    x = itemJar.x,
                    y = itemJar.y,
                    amount = itemJar.item.amount
                });
            }
            plist[player.SteamId] = list;
        }

        [MPlugin(Type = EMPluginStateType.Unload)]
        public void Exit()
        {
            MEvents.OnPlayerJoinServer -= MEvents_OnPlayerJoinServer;
            PlayerLife.OnRevived_Global -= OnRevived_Global;
            PlayerLife.OnPreDeath -= PlayerLife_OnPreDeath;
        }
    }



    public class ItemInfo
    {
        public ushort id;

        public byte quality;

        public byte rot;

        public byte durability;

        public byte x;

        public byte y;

        public byte amount;

        public byte[] state;
    }



    public class Config : IMPluginConfig
    {
        public void LoadDefault()
        {
            PermissionsConfig = new List<PermissionPlayerHands>
            {
                new PermissionPlayerHands
                {
                    Permission="defaultHand",
                    X=5,
                    Y=5
                },
                new PermissionPlayerHands
                {
                    Permission="vipHand",
                    X=7,
                    Y=7
                }
            };
        }
        [XmlArray("物品栏权限组"), XmlArrayItem("权限设置")]
        public List<PermissionPlayerHands> PermissionsConfig { get; set; }
    }

    public class PermissionPlayerHands
    {
        [XmlElement("权限")]
        public string Permission { get; set; }

        [XmlElement("长度")]
        public byte X { get; set; }

        [XmlElement("宽度")]
        public byte Y { get; set; }
    }
}
