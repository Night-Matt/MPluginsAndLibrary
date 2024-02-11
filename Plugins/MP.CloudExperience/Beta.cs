using MLib.MySQL;
using MPlugin.Untruned.MPlugin.API;
using MPlugin.Untruned.MPlugin.Core;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using static MLib.MySQL.MSQL;

namespace MP.CloudExperience
{
    [MPluginInfo(PluginName = "云经验", AuthorName = "Matt", ContactInformation = "QQ: 2472285384")]
    public class Beta : MPlugin<Config>
    {
        private MSQL SQL { get; set; }
        private Dictionary<CSteamID, PlayerExperienceModel> PlayerInfos { get; set; } = new Dictionary<CSteamID, PlayerExperienceModel> { };
        [MPlugin(Type = EMPluginStateType.Load)]
        public void Init()
        {
            SQL = new MSQL(Configuration.MySQLAddress, Configuration.MySQLUserName, Configuration.MySQLPassword, Configuration.MySQLDatabase, Configuration.MySQLPort.ToString());
            InitMySQL();
            PlayerSkills.OnExperienceChanged_Global += PlayerSkills_OnExperienceChanged_Global;
            Provider.onServerConnected += OnServerConnected;
            Provider.onServerDisconnected += OnServerDisconnected;
        }

        private async void OnServerDisconnected(CSteamID steamID)
        {
            await SQL.UpdateDataAsync(PlayerInfos[steamID]);
        }

        private async void OnServerConnected(CSteamID steamID)
        {
            MPlayer player = MPlayer.GetMPlayer(steamID);
            if (!PlayerInfos.ContainsKey(steamID)) PlayerInfos.Add(steamID, new PlayerExperienceModel
            {
                SteamId = player.SteamId.ToString(),
                PlayerName = player.DisPlayName,
                Experience = player.Player.skills.experience
            });
            if (await SQL.CheckPrimaryKeyExistsAsync(PlayerInfos[player.SteamId]))
            {
                var result = await SQL.QueryAsync<PlayerExperienceModel>(new List<QueryCondition>
                {
                    new QueryCondition
                    {
                        ColumnName="SteamId",
                        Operator=EQueryOperator.equal,
                        Value=player.SteamId.ToString()
                    }
                });

                PlayerInfos[player.SteamId] = result?[0] ?? new PlayerExperienceModel
                {
                    SteamId = player.SteamId.ToString(),
                    PlayerName = player.DisPlayName,
                    Experience = player.Player.skills.experience
                };
                if (!PlayerInfos[player.SteamId].PlayerName.Equals(player.DisPlayName)) 
                    PlayerInfos[player.SteamId].PlayerName = player.DisPlayName;
                MSay.Say($"正在同步经验...  {PlayerInfos[player.SteamId].PlayerName} 所拥有经验值: {PlayerInfos[player.SteamId].Experience}", player.SteamId, Color.yellow);
                player.Player.skills.ServerSetExperience(PlayerInfos[player.SteamId].Experience);
            }
            else
            {
                await SQL.AddDataAsync(PlayerInfos[player.SteamId]);
            }
        }

        async void InitMySQL()
        {
            await SQL.InitTableAsync<PlayerExperienceModel>();
        }

        private void PlayerSkills_OnExperienceChanged_Global(PlayerSkills skill, uint oldExp)
        {
            MPlayer player = MPlayer.GetMPlayer(skill.player.channel.owner.playerID.steamID);
            PlayerInfos[player.SteamId].Experience = skill.experience;
        }

        [MPlugin(Type = EMPluginStateType.Unload)]
        public void Exit()
        {
            PlayerSkills.OnExperienceChanged_Global -= PlayerSkills_OnExperienceChanged_Global;
            Provider.onServerConnected -= OnServerConnected;
            Provider.onServerDisconnected -= OnServerDisconnected;
        }
        [MTable(TableName = "mp_cloud_experience")]
        class PlayerExperienceModel
        {
            /// <summary>
            /// 玩家的steamId
            /// </summary>
            [MColumn(isPrimaryKey = true, DataType = "varchar", IsNotNull = true, DataLength = "64")]
            public string SteamId { get; set; }

            /// <summary>
            /// 玩家的名称
            /// </summary>
            [MColumn(isPrimaryKey = false, DataType = "varchar", IsNotNull = true, DataLength = "255")]
            public string PlayerName { get; set; }

            [MColumn(isPrimaryKey = false, DataType = "varchar", IsNotNull = true, DataLength = "255")]
            public uint Experience { get; set; }
        }
    }

    public class Config : IMPluginConfig
    {
        public void LoadDefault()
        {
            MySQLAddress = "localhost";
            MySQLUserName = "root";
            MySQLPassword = "password";
            MySQLDatabase = "unturned";
            MySQLPort = 3306;
        }
        public string MySQLAddress { get; set; }
        public string MySQLUserName { get; set; }
        public string MySQLPassword { get; set; }
        public string MySQLDatabase { get; set; }
        public int MySQLPort { get; set; }
    }


    public class CommandEpay : IMCommand
    {
        public string Name => "epay";

        public string Permission => "";

        public ECommandAllowType AllowType => ECommandAllowType.Both;

        public bool Excute(string[] parameters, MPlayer console)
        {
            if (parameters.Length >= 2)
            {
                if (PlayerTool.tryGetSteamID(parameters[0], out CSteamID steamId)
                    && uint.TryParse(parameters[1], out uint addExp))
                {
                    MPlayer player = MPlayer.GetMPlayer(steamId);
                    if (player.IsConsolePlayer)
                    {
                        player.Player.skills.ServerSetExperience(player.Player.skills.experience + addExp);
                    }
                    else
                    {
                        if (addExp > 0 && console.Player.skills.experience >= addExp)
                        {
                            player.Player.skills.ServerSetExperience(player.Player.skills.experience + addExp);
                            console.Player.skills.ServerSetExperience(console.Player.skills.experience - addExp);
                        }
                        else
                        {
                            MSay.Say($"转账经验值必须大于0 并且 拥有的经验值必须大于等于转账的经验值!", console.SteamId, Color.yellow);
                        }
                    }
                    if (console.IsConsolePlayer)
                    {
                        MLog.LogError($"成功给予玩家: {player.DisPlayName} {addExp}xp!");
                        return true;
                    }
                    else if (console.IsAdmin && addExp > 0)
                    {
                        MSay.Say($"成功向玩家: {player.DisPlayName} 转账 {addExp}xp!", console.SteamId, Color.yellow);
                        string org = "";
                        if (console.IsConsolePlayer) org = "控制台管理员";
                        else org = $"转账方: {console.DisPlayName}";
                        MSay.Say($"{org} 给予的经验值: {addExp}xp!", player.SteamId, Color.yellow);
                        return true;
                    }
                }
                else if (console.IsConsolePlayer)
                {
                    MLog.LogError($"出现未知错误!");
                }
                else if (console.IsAdmin)
                {
                    MSay.Say($"出现未知错误!", console.SteamId, Color.red);
                }
            }
            else if (console.IsConsolePlayer)
            {
                MLog.LogError($"指令格式:  /epay <玩家信息> <增加的经验值>");
            }
            else if (console.IsAdmin)
            {
                MSay.Say($"指令格式:  /epay <玩家信息> <转账的经验值>", console.SteamId, Color.red);
            }
            return false;
        }
    }


    public class CommandEapay : IMCommand
    {
        public string Name => "eapay";

        public string Permission => "";

        public ECommandAllowType AllowType => ECommandAllowType.Both;

        public bool Excute(string[] parameters, MPlayer console)
        {
            if (console.IsConsolePlayer || console.IsAdmin)
            {
                if (parameters.Length >= 2)
                {
                    if (PlayerTool.tryGetSteamID(parameters[0], out CSteamID steamId)
                        && uint.TryParse(parameters[1], out uint addExp))
                    {
                        MPlayer player = MPlayer.GetMPlayer(steamId);
                        player.Player.skills.ServerSetExperience(player.Player.skills.experience + addExp);
                        if (console.IsConsolePlayer)
                        {
                            MLog.LogError($"成功给予玩家: {player.DisPlayName} {addExp}xp!");
                        }
                        else if (console.IsAdmin)
                        {
                            MSay.Say($"成功给予玩家: {player.DisPlayName} {addExp}xp!", console.SteamId, Color.yellow);
                        }
                        string org = "";
                        if (console.IsConsolePlayer) org = "控制台管理员";
                        else org = $"管理员: {console.DisPlayName}";
                        MSay.Say($"来自: {org} 给予的经验值: {addExp}xp!", player.SteamId, Color.yellow);
                        return true;
                    }
                    else if (console.IsConsolePlayer)
                    {
                        MLog.LogError($"出现未知错误!");
                    }
                    else if (console.IsAdmin)
                    {
                        MSay.Say($"出现未知错误!", console.SteamId, Color.red);
                    }
                }
                else if (console.IsConsolePlayer)
                {
                    MLog.LogError($"指令格式:  /eapay <玩家信息> <增加的经验值>");
                }
                else if (console.IsAdmin)
                {
                    MSay.Say($"指令格式:  /eapay <玩家信息> <增加的经验值>", console.SteamId, Color.red);
                }
            }
            return false;
        }
    }

}
