using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using HearthMirror;
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

        private List<string> oppNames = null;
        private Dictionary<string, string> leaderBoard = null;

        private Mirror mirror;
        private HttpClient client;
        private LeaderBoardPanel leaderBoardPanel;

        public BGrank()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "User-Agent-Here");
            client.Timeout = TimeSpan.FromSeconds(15);
            mirror = new Mirror { ImageName = "Hearthstone" };
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
            oppNames = null;
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
                    _ = GetLeaderBoard();
                }

                if (done || !Core.Game.IsBattlegroundsHeroPickingDone) { return; }

                if (failToGetData)
                {
                    leaderBoardPanel.DisplayResult("\nFailed to get leaderboard\nCheck log for details");
                    ClearMemory();
                    done = true;
                }
                else if (leaderBoardReady)
                {
                    if (TryGetOppNames())
                    {
                        string region = GetRegionStr();
                        string resultText = string.Empty;
                        Dictionary<string, int> unsortDict = new Dictionary<string, int>();
                        foreach (string name in oppNames)
                        {
                            if (leaderBoard.TryGetValue(name, out string value))
                            {
                                if (int.TryParse(value, out int rate))
                                {
                                    unsortDict.Add(name, rate);
                                }
                                else
                                {
                                    FileLogger.Instance.Warn($"Parse MMR failed, set MMR as 0. player:'{name}' MMR:'{value}'");
                                    unsortDict.Add(name, 0);
                                }
                            }
                            else
                            {
                                unsortDict.Add(name, 0);
                            }
                        }
                        foreach (var opp in unsortDict.OrderBy(x => x.Value))
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
                        ClearMemory();
                        done = true;
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

            leaderBoard = new Dictionary<string, string>();
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
                _ => "UNKNOWN",
            };
        }

        private bool TryGetOppNames()
        {
            // The code below is from: https://github.com/Zero-to-Heroes/unity-spy-.net4.5
            try
            {
                string myName = Reflection.Client?.GetBattleTag()?.Name;
                if (string.IsNullOrWhiteSpace(myName)) { return false; }
                var leaderboardMgr = mirror.Root?["PlayerLeaderboardManager"]?["s_instance"];
                if (leaderboardMgr == null) { return false; }
                dynamic[] playerTiles = GetPlayerTiles(leaderboardMgr);
                int numberOfPlayerTiles = playerTiles?.Length ?? 0;
                if (numberOfPlayerTiles == 0) { return false; }
                List<string> tmpNames = new List<string>();

                for (int i = 0; i < numberOfPlayerTiles; i++)
                {
                    var playerTile = playerTiles[i];
                    // Info not available until the player mouses over the tile in the leaderboard, and there is no other way to get it
                    string playerName = playerTile["m_overlay"]?["m_heroActor"]?["m_playerNameText"]?["m_Text"];
                    if (string.IsNullOrWhiteSpace(playerName)) { return false; }
                    if (playerName != myName && !tmpNames.Contains(playerName)) { tmpNames.Add(playerName); }
                }

                // For those who use the BattleTag MOD
                for (int i = 0; i < tmpNames.Count; i++)
                {
                    int index = tmpNames[i].IndexOf('#');
                    if (index > 0)
                    {
                        tmpNames[i] = tmpNames[i].Substring(0, index);
                    }
                }
                oppNames = new List<string>(tmpNames);
                return true;
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
                return false;
            }
        }

        // The code below is from: https://github.com/Zero-to-Heroes/unity-spy-.net4.5
        private static dynamic[] GetPlayerTiles(dynamic leaderboardMgr)
        {
            try
            {
                return leaderboardMgr?["m_playerTiles"]?["_items"];
            }
            catch (Exception)
            {
                var result = new List<dynamic>();
                var teams = leaderboardMgr["m_teams"]?["_items"];

                for (uint i = 0; i < teams.size(); i++)
                {
                    var team = teams[i];
                    if (team == null) { continue; }
                    var tiles = team["m_playerLeaderboardCards"]?["_items"];
                    for (uint j = 0; j < tiles.size(); j++)
                    {
                        result.Add(tiles[j]);
                    }
                }

                return result.ToArray();
            }
        }
    }
}