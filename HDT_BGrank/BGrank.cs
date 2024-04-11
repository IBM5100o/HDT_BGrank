using System;
using System.IO;
using System.Linq;
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
        public bool done = false;
        public bool failToGetData = false;
        public Dictionary<string, string> oppDict = new Dictionary<string, string>();

        private bool namesReady = false;
        private bool playerReady = false;
        private bool leaderBoardReady = false;

        private Mirror mirror = null;
        private List<string> oppNames = new List<string>();
        private Dictionary<string, int> unsortDict = new Dictionary<string, int>();
        private Dictionary<string, string> leaderBoard = new Dictionary<string, string>();

        public void Reset()
        {
            done = false;
            failToGetData = false;
            namesReady = false;
            playerReady = false;
            leaderBoardReady = false;
            ClearMemory();
        }

        public void ClearMemory()
        {
            oppDict.Clear();
            oppNames.Clear();
            unsortDict.Clear();
            leaderBoard.Clear();
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
            if (!Core.Game.IsRunning)
            {
                if (mirror != null)
                {
                    mirror.Clean();
                    mirror = null;
                }
            }
            else if (mirror == null)
            {
                mirror = new Mirror { ImageName = "Hearthstone" };
            }

            if (Core.Game.IsInMenu)
            {
                Reset();
            }
            else if (!done && Core.Game.IsBattlegroundsMatch)
            {
                if (failToGetData) { done = true; }
                else if (!namesReady) { GetOppNames(); }
                else if (leaderBoardReady)
                {
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

            // Get the leaderboard information from: https://www.d0nkey.top
            string region = GetRegionStr();
            string path = $"LeaderBoard_{region}.txt";
            string url = $"https://www.d0nkey.top/leaderboard?leaderboardId=BG&region={region}&limit=100000";
            int num_tries = 0;
            int max_tries = 2;

            while (num_tries < max_tries && !leaderBoardReady)
            {
                num_tries++;
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "User-Agent-Here");
                        string response = await client.GetStringAsync(url);
                        if (string.IsNullOrEmpty(response)) 
                        {
                            if (num_tries < max_tries) { await Task.Delay(10000); }
                            continue; 
                        }

                        string[] lines = response.Split('\n');
                        string line, name, rating;
                        int idx, idx2, i = 0;
                        while (i < lines.Length)
                        {
                            line = lines[i];
                            i++;
                            idx = line.IndexOf("player-profile/");
                            if (idx > 0)
                            {
                                idx += 15;
                                idx2 = line.IndexOf("\"", idx);
                                name = line.Substring(idx, idx2 - idx);
                                if (string.IsNullOrEmpty(name) || leaderBoard.ContainsKey(name)) { continue; }
                                for (int j = 0; j < 2;)
                                {
                                    line = lines[i];
                                    i++;
                                    if (line.Contains("</td>")) j++;
                                }
                                idx = line.IndexOf(">");
                                idx2 = line.LastIndexOf("<");
                                rating = line.Substring(idx + 1, idx2 - idx - 1);
                                if (!string.IsNullOrEmpty(rating)) { leaderBoard.Add(name, rating); }
                            }
                        }
                    }
                    if (leaderBoard.Count != 0) 
                    {
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
                catch (Exception)
                {
                    if (num_tries < max_tries) { await Task.Delay(10000); }
                }
            }

            if (!leaderBoardReady) 
            { 
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
                        if (leaderBoard.Count != 0) { leaderBoardReady = true; }
                        else { failToGetData = true; }
                    }
                    catch (Exception)
                    {
                        failToGetData = true;
                    }
                }
                else
                {
                    failToGetData = true;
                }
            }
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

            // The code below is from: https://github.com/Zero-to-Heroes/unity-spy-.net4.5/tree/master
            string myName = Reflection.Client.GetMatchInfo().LocalPlayer.Name;
            var leaderboardMgr = mirror.Root?["PlayerLeaderboardManager"]?["s_instance"];
            var numberOfPlayerTiles = leaderboardMgr?["m_playerTiles"]?["_size"];
            var playerTiles = leaderboardMgr?["m_playerTiles"]?["_items"];

            for (int i = 0; i < numberOfPlayerTiles; i++)
            {
                var playerTile = playerTiles[i];
                // Info not available until the player mouses over the tile in the leaderboard, and there is no other way to get it
                string playerName = playerTile["m_mainCardActor"]?["m_playerNameText"]?["m_Text"];
                if (string.IsNullOrEmpty(playerName)) 
                {
                    oppNames.Clear();
                    break;
                }
                if (playerName != myName && !oppNames.Contains(playerName)) { oppNames.Add(playerName); }
            }
            if (oppNames.Count != 0) { namesReady = true; }
        }

    }
}