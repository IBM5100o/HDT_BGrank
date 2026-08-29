using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;

namespace HDT_BGrank
{
    public class BGrank
    {
        private bool isReset = true;
        private bool done = false;
        private bool failToGetData = false;
        private bool leaderBoardReady = false;
        private int nameErrors = 0;

        private Dictionary<string, int> oppDict = null;
        private Dictionary<string, string> leaderBoard = null;

        private Mirror mirror;
        private HttpClient client;
        private LeaderBoardPanel leaderBoardPanel;

        public BGrank()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "User-Agent-Here");
            client.Timeout = TimeSpan.FromSeconds(15);
            mirror = new Mirror();
            leaderBoardPanel = new LeaderBoardPanel();
        }

        public void Clean(bool save)
        {
            ClearMemory();
            client.Dispose();
            leaderBoardPanel.Clean(save);
            client = null;
            mirror = null;
            leaderBoardPanel = null;
        }

        private void Reset()
        {
            done = false;
            failToGetData = false;
            leaderBoardReady = false;
            nameErrors = 0;
            ClearMemory();
            leaderBoardPanel.Visibility = Visibility.Hidden;
        }

        private void ClearMemory()
        {
            oppDict = null;
            leaderBoard = null;
            mirror.Clean();
        }

        public void OnUpdate()
        {
            if (Core.Game.IsInMenu)
            {
                if (!isReset)
                {
                    Reset();
                    isReset = true;
                }
            }
            else if (Core.Game.IsBattlegroundsMatch)
            {
                if (isReset)
                {
                    Reset(); // Reset again, just in case
                    isReset = false;
                    oppDict = new Dictionary<string, int>();
                    leaderBoard = new Dictionary<string, string>();
                    _ = GetLeaderBoard();
                }

                if (done || Core.Game.GetTurnNumber() == 0) { return; }

                if (failToGetData)
                {
                    leaderBoardPanel.DisplayResult("\nFail to get data\nCheck log for details");
                    ClearMemory();
                    done = true;
                }
                else if (leaderBoardReady)
                {
                    var lobbyNames = GetLobbyNames();
                    int playerCount = lobbyNames?.Count() ?? 0;
                    if (playerCount == 0) { return; }
                    var tileNames = GetTileNames();
                    if (tileNames == null) { return; }
                    List<string> playerNames = new List<string>();

                    foreach (var name in lobbyNames)
                    {
                        if (name != string.Empty && !playerNames.Contains(name))
                        {
                            playerNames.Add(name);
                        }
                    }

                    foreach (var name in tileNames)
                    {
                        if (name != string.Empty && !playerNames.Contains(name))
                        {
                            playerNames.Add(name);
                        }
                    }

                    bool newNameCome = false;
                    foreach (var name in playerNames)
                    {
                        if (name != mirror.MyName && !oppDict.ContainsKey(name))
                        {
                            newNameCome = true;
                            if (leaderBoard.TryGetValue(name, out string value))
                            {
                                if (int.TryParse(value, out int rate))
                                {
                                    oppDict.Add(name, rate);
                                }
                                else
                                {
                                    FileLogger.Instance.Warn($"Parse MMR failed, set MMR as 0. player:'{name}' MMR:'{value}'");
                                    oppDict.Add(name, 0);
                                }
                            }
                            else
                            {
                                oppDict.Add(name, 0);
                            }
                        }
                    }

                    if (newNameCome)
                    {
                        string region = GetRegionStr();
                        string resultText = string.Empty;
                        foreach (var opp in oppDict.OrderBy(x => x.Value))
                        {
                            if (opp.Value == 0)
                            {
                                if (region == "CN")
                                {
                                    resultText += $"\n{opp.Key} -";
                                }
                                else
                                {
                                    resultText += $"\n{opp.Key} 8000↓";
                                }
                            }
                            else
                            {
                                resultText += $"\n{opp.Key} {opp.Value}";
                            }
                        }
                        leaderBoardPanel.DisplayResult(resultText);

                        if (!lobbyNames.Contains(string.Empty) || !tileNames.Contains(string.Empty) || playerNames.Count >= playerCount)
                        {
                            FileLogger.Instance.Info("All done, stop scanning player names and wait for next match.");
                            ClearMemory();
                            done = true;
                        }
                    }
                }
            }
        }

        private async Task GetLeaderBoard()
        {
            // Get the leaderboard information from web, see https://github.com/IBM5100o/BGrank_bot
            string region = GetRegionStr();
            if (region == "UNKNOWN")
            {
                FileLogger.Instance.Warn("Failed to get your server region, no data for this match");
                failToGetData = true;
                return;
            }

            string path, url;
            int num_tries = 0;
            int max_tries = 3;

            if (Core.Game.IsBattlegroundsSoloMatch)
            {
                path = Path.Combine(Config.AppDataPath, "BGrank", $"LeaderBoard_{region}.txt");
                url = $"https://bgrank.fly.dev/{region}/";
            }
            else
            {
                path = Path.Combine(Config.AppDataPath, "BGrank", $"LeaderBoard_{region}_duo.txt");
                url = $"https://bgrank.fly.dev/{region}_duo/";
            }

            while (num_tries < max_tries && !leaderBoardReady)
            {
                num_tries++;
                try
                {
                    FileLogger.Instance.Info($"Try to get the leaderboard from {url} (try {num_tries}/{max_tries})");
                    string response = await client.GetStringAsync(url);
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        if (num_tries < max_tries) { await Task.Delay(5000); }
                        continue;
                    }

                    string[] lines = response.Split(new string[] { "\n<br />" }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        string[] tmp = line.Split(' ');
                        if (tmp.Length == 2)
                        {
                            string name = tmp[0];
                            string rating = tmp[1];
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rating)) { continue; }
                            if (!leaderBoard.ContainsKey(name)) { leaderBoard.Add(name, rating); }
                        }
                    }
                    if (leaderBoard.Count != 0)
                    {
                        FileLogger.Instance.Info($"Success to get the leaderboard from {url}");
                        leaderBoardReady = true;
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Error($"Failed to get the leaderboard from {url}", ex);
                    if (num_tries < max_tries) { await Task.Delay(5000); }
                }
            }

            if (leaderBoardReady)
            {
                try
                {
                    using StreamWriter writer = new StreamWriter(path);
                    foreach (var player in leaderBoard)
                    {
                        writer.WriteLine($"{player.Key} {player.Value}");
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Error("Failed to save the leaderboard to local", ex);
                }
            }
            else
            {
                FileLogger.Instance.Info("Failed to get the leaderboard from web, try to get it from local...");
                if (File.Exists(path))
                {
                    try
                    {
                        string line;
                        using StreamReader reader = new StreamReader(path);
                        while ((line = reader.ReadLine()) != null)
                        {
                            string[] tmp = line.Split(' ');
                            leaderBoard.Add(tmp[0], tmp[1]);
                        }
                        if (leaderBoard.Count != 0)
                        {
                            FileLogger.Instance.Info("Success to get the leaderboard from local!");
                            leaderBoardReady = true;
                        }
                        else { failToGetData = true; }
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Instance.Error("Failed to read the local file", ex);
                        failToGetData = true;
                    }
                }
                else
                {
                    FileLogger.Instance.Warn("Local file not exist");
                    failToGetData = true;
                }
            }
            if (failToGetData) { FileLogger.Instance.Warn("Failed to get the leaderboard, no data for this match"); }
        }

        private string GetRegionStr()
        {
            return Core.Game.CurrentRegion switch
            {
                Region.US => "US",
                Region.EU => "EU",
                Region.ASIA => "AP",
                Region.CHINA => "CN",
                _ => "UNKNOWN"
            };
        }

        private List<string> GetLobbyNames()
        {
            try
            {
                dynamic gameState = mirror.Root?["GameState"]?["s_instance"];
                if (gameState == null) { return null; }
                dynamic valueSlots = gameState["m_playerInfoMap"]?["valueSlots"];
                if (valueSlots == null) { return null; }

                List<string> resultNames = new List<string>();
                for (uint i = 0; i < valueSlots.size(); i++)
                {
                    dynamic valueSlot = valueSlots[i];
                    if (valueSlot == null) { continue; }
                    string playerName = valueSlot["m_name"] ?? string.Empty;
                    resultNames.Add(playerName);
                }
                return resultNames;
            }
            catch (Exception ex)
            {
                nameErrors++;
                if (nameErrors < 5)
                {
                    FileLogger.Instance.Error("Fail to get opponent name from lobby", ex);
                }
                else if (nameErrors == 5)
                {
                    FileLogger.Instance.Error("Fail to get opponent name from lobby, further errors will be suppressed", ex);
                }
                return null;
            }
        }

        private List<string> GetTileNames()
        {
            try
            {
                dynamic leaderboardMgr = mirror.Root?["PlayerLeaderboardManager"]?["s_instance"];
                if (leaderboardMgr == null) { return null; }
                dynamic[] playerTiles = GetPlayerTiles(leaderboardMgr);
                int numberOfPlayerTiles = playerTiles?.Length ?? 0;
                if (numberOfPlayerTiles == 0) { return null; }

                List<string> resultNames = new List<string>();
                for (int i = 0; i < numberOfPlayerTiles; i++)
                {
                    dynamic playerTile = playerTiles[i];
                    string playerName = playerTile?["m_overlay"]?["m_heroActor"]?["m_playerNameText"]?["m_Text"] ?? string.Empty;
                    resultNames.Add(playerName);
                }
                return resultNames;
            }
            catch (Exception ex)
            {
                nameErrors++;
                if (nameErrors < 5)
                {
                    FileLogger.Instance.Error("Fail to get opponent name", ex);
                }
                else if (nameErrors == 5)
                {
                    FileLogger.Instance.Error("Fail to get opponent name, further errors will be suppressed", ex);
                }
                return null;
            }
        }

        // The code below is from: https://github.com/Zero-to-Heroes/unity-spy-.net4.5
        private dynamic[] GetPlayerTiles(dynamic leaderboardMgr)
        {
            List<dynamic> result = new List<dynamic>();
            dynamic teams = leaderboardMgr["m_teams"]?["_items"];

            for (uint i = 0; i < teams.size(); i++)
            {
                dynamic team = teams[i];
                if (team == null) { continue; }
                dynamic tiles = team["m_playerLeaderboardCards"]?["_items"];
                for (uint j = 0; j < tiles.size(); j++)
                {
                    result.Add(tiles[j]);
                }
            }
            return result.ToArray();
        }
    }
}