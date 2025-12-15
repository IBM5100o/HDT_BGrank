using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using HearthMirror;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace HDT_BGrank
{
    public class BGrank
    {
        public bool failToGetData = false;

        private bool gameEndHandled = true;
        private bool playersReady = false;
        private bool leaderBoardReady = false;

        private readonly HttpClient client;
        private List<string> oppNames = new List<string>();
        private Dictionary<string, string> leaderBoard = new Dictionary<string, string>();
        private LeaderBoardPanel leaderBoardPanel;


        public BGrank(LeaderBoardPanel leaderBoardPanel)
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "User-Agent-Here");
            this.leaderBoardPanel = leaderBoardPanel;
        }

        public void OnUpdate()
        {
            if (Core.Game.IsInMenu)
            {
                HandleGameEnd();
            }
            else if (Core.Game.IsBattlegroundsMatch)
            {
                gameEndHandled = false;
                if (failToGetData)
                {
                    return;
                }
                else if (leaderBoardPanel.oppDict.Count < 8)
                {
                    var names = GetOpponentsNames() ?? new string[0];

                    foreach (var name in names)
                    {
                        leaderBoardPanel.TryAddOpponent(name, GetPlayerRank(name));
                    }
                }
            }
        }

        private void HandleGameEnd()
        {
            if (gameEndHandled)
            {
                return;
            }

            failToGetData = false;
            leaderBoardReady = false;

            leaderBoardPanel.Clear();
            oppNames.Clear();
            leaderBoard.Clear();

            gameEndHandled = true;
        }

        public void OnGameStart()
        {
            _ = GetLeaderBoard();
        }

        public void OnTurnStart(ActivePlayer player)
        {
            _ = GetLeaderBoard();
        }

        private int? GetPlayerRank(string playerName)
        {
            if (leaderBoard.TryGetValue(playerName, out var rankString) && int.TryParse(rankString, out var rank))
            {
                return rank;
            }
            else
            {
                return null;
            }
        }

        private async Task GetLeaderBoard()
        {
            if (!Core.Game.IsBattlegroundsMatch || leaderBoardReady || failToGetData) { return; }

            // Get the leaderboard information from web, see https://github.com/IBM5100o/BGrank_bot
            string region = GetRegionStr();
            string path, url;
            int num_tries = 0;
            int max_tries = 3;

            if (Core.Game.IsBattlegroundsSoloMatch)
            {
                path = Path.Combine(Config.AppDataPath, $"LeaderBoard_{region}.txt");
                url = $"https://bgrank.fly.dev/{region}/";
            }
            else
            {
                path = Path.Combine(Config.AppDataPath, $"LeaderBoard_{region}_duo.txt");
                url = $"https://bgrank.fly.dev/{region}_duo/";
            }

            while (num_tries < max_tries && !leaderBoardReady)
            {
                num_tries++;
                try
                {
                    Log.Info($"Try to get the leaderboard from {url} (try {num_tries}/{max_tries})");
                    string response = await client.GetStringAsync(url);
                    if (string.IsNullOrEmpty(response))
                    {
                        if (num_tries < max_tries) { await Task.Delay(10000); }
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
                            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(rating)) { continue; }
                            if (!leaderBoard.ContainsKey(name)) { leaderBoard.Add(name, rating); }
                        }
                    }

                    if (leaderBoard.Count != 0)
                    {
                        Log.Info("Success to get the leaderboard from web!");
                        leaderBoardReady = true;
                        using (StreamWriter writer = new StreamWriter(path))
                        {
                            foreach (var player in leaderBoard)
                            {
                                writer.WriteLine($"{player.Key} {player.Value}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    if (num_tries < max_tries) { await Task.Delay(10000); }
                }
            }

            if (!leaderBoardReady) 
            {
                Log.Info("Fail to get the leaderboard from web, try to get it from local...");
                if (File.Exists(path))
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(path))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                string[] tmp = line.Split(' ');
                                leaderBoard.Add(tmp[0], tmp[1]);
                            }
                        }
                        if (leaderBoard.Count != 0) 
                        {
                            Log.Info("Success to get the leaderboard from local!");
                            leaderBoardReady = true;
                        }
                        else { failToGetData = true; }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        failToGetData = true;
                    }
                }
                else
                {
                    failToGetData = true;
                }
            }
            if (failToGetData) { Log.Info("Fail to get the leaderboard from local, no data for this match"); }
        }

        private string GetRegionStr()
        {
            switch (Core.Game.CurrentRegion)
            {
                case Region.US:
                    return "US";
                case Region.EU:
                    return "EU";
                case Region.CHINA:
                    return "CN";
                default:
                    return "AP";
            }
        }

        private ICollection<string> GetOpponentsNames()
        {
            // The code below is from: https://github.com/Zero-to-Heroes/unity-spy-.net4.5
            try
            {
                string myName = Reflection.Client?.GetBattleTag()?.Name;
                if (string.IsNullOrEmpty(myName)) { return null; }
                Mirror mirror = new Mirror { ImageName = "Hearthstone" };
                var leaderboardMgr = mirror.Root?["PlayerLeaderboardManager"]?["s_instance"];
                if (leaderboardMgr == null) { return null; }
                dynamic[] playerTiles = GetPlayerTiles(leaderboardMgr);
                var numberOfPlayerTiles = playerTiles?.Length ?? 0;
                if (numberOfPlayerTiles == 0) { return null; }
                List<string> tmpNames = new List<string>();

                for (int i = 0; i < numberOfPlayerTiles; i++)
                {
                    var playerTile = playerTiles[i];
                    // Info not available until the player mouses over the tile in the leaderboard, and there is no other way to get it
                    string playerName = playerTile["m_overlay"]?["m_heroActor"]?["m_playerNameText"]?["m_Text"];
                    if (string.IsNullOrEmpty(playerName)) 
                    { 
                        continue; 
                    }
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

                return tmpNames;
            }
            catch (Exception ex) 
            {
                Log.Error(ex);
                return null;
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