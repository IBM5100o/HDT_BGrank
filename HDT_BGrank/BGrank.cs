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
        public bool done = false;
        public bool failToGetData = false;
        public Dictionary<string, string> oppDict = null;

        private bool isReset = true;
        private bool namesReady = false;
        private bool playerReady = false;
        private bool leaderBoardReady = false;

        private Mirror mirror = null;
        private List<string> oppNames = null;
        private Dictionary<string, string> leaderBoard = null;
        private readonly HttpClient client;

        public BGrank()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "User-Agent-Here");
        }

        private void Reset()
        {
            done = false;
            namesReady = false;
            playerReady = false;
            failToGetData = false;
            leaderBoardReady = false;
            ClearMemory();
        }

        public void ClearMemory()
        {
            mirror = null;
            oppDict = null;
            oppNames = null;
            leaderBoard = null;
        }

        public void OnGameStart()
        {
            GetLeaderBoard();
        }

        public void OnTurnStart(ActivePlayer player)
        {
            GetLeaderBoard();
            playerReady = true;
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
            else if (!done && Core.Game.IsBattlegroundsMatch)
            {
                if (isReset)
                {
                    mirror = new Mirror { ImageName = "Hearthstone" };
                    isReset = false;
                }
                if (failToGetData) { done = true; }
                else if (!namesReady) { GetOppNames(); }
                else if (leaderBoardReady)
                {
                    Dictionary<string, int> unsortDict = new Dictionary<string, int>();
                    oppDict = new Dictionary<string, string>();
                    foreach (string name in oppNames)
                    {
                        if (leaderBoard.TryGetValue(name, out string value))
                        {
                            unsortDict.Add(name, int.Parse(value));
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
                            oppDict.Add(opp.Key, "8000↓");
                        }
                        else
                        {
                            oppDict.Add(opp.Key, opp.Value.ToString());
                        }
                    }
                    done = true;
                }
            }
        }

        private async Task GetLeaderBoard()
        {
            if (!Core.Game.IsBattlegroundsMatch || leaderBoardReady || failToGetData) { return; }

            // Get the leaderboard information from web, see https://github.com/IBM5100o/BGrank_bot
            leaderBoard = new Dictionary<string, string>();
            string region = GetRegionStr();
            string path, url;
            int num_tries = 0;
            int max_tries = 3;

            if (Core.Game.IsBattlegroundsSoloMatch)
            {
                path = $"LeaderBoard_{region}.txt";
                url = $"https://bgrank.fly.dev/{region}/";
            }
            else
            {
                path = $"LeaderBoard_{region}_duo.txt";
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

                    string[] lines = response.Split('\n');
                    for (int i = 0; i < lines.Length-1; i++)
                    {
                        string line = lines[i];
                        if (i > 0)
                        {
                            line = line.Substring(6);
                        }
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
                                writer.WriteLine(player.Key + " " + player.Value);
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
                default:
                    return "AP";
            }
        }

        private void GetOppNames()
        {
            if (!playerReady) { return; }

            // The code below is from: https://github.com/Zero-to-Heroes/unity-spy-.net4.5
            try
            {
                string myName = Reflection.Client?.GetMatchInfo()?.LocalPlayer?.Name;
                if (string.IsNullOrEmpty(myName)) { return; }
                var leaderboardMgr = mirror.Root?["PlayerLeaderboardManager"]?["s_instance"];
                dynamic[] playerTiles = GetPlayerTiles(leaderboardMgr);
                var numberOfPlayerTiles = playerTiles?.Length ?? 0;
                if (numberOfPlayerTiles == 0) { return; }
                bool allPass = true;
                List<string> tmpNames = new List<string>();

                for (int i = 0; i < numberOfPlayerTiles; i++)
                {
                    var playerTile = playerTiles[i];
                    // Info not available until the player mouses over the tile in the leaderboard, and there is no other way to get it
                    string playerName = playerTile["m_overlay"]?["m_heroActor"]?["m_playerNameText"]?["m_Text"];
                    if (string.IsNullOrEmpty(playerName)) 
                    {
                        allPass = false;
                        break;
                    }
                    if (playerName != myName && !tmpNames.Contains(playerName)) { tmpNames.Add(playerName); }
                }
                if (allPass)
                {
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
                    namesReady = true;
                }
            }
            catch (Exception ex) 
            {
                Log.Error(ex);
            }
        }

        // The code below is from: https://github.com/Zero-to-Heroes/unity-spy-.net4.5
        private static dynamic[] GetPlayerTiles(dynamic leaderboardMgr)
        {
            var result = new List<dynamic>();
            var teams = leaderboardMgr["m_teams"]?["_items"];
            foreach (var team in teams)
            {
                if (team == null)
                {
                    continue;
                }

                var tiles = team["m_playerLeaderboardCards"]?["_items"];
                foreach (var tile in tiles)
                {
                    result.Add(tile);
                }
            }
            return result.ToArray();
        }
    }
}