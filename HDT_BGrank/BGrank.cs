using HearthMirror;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace HDT_BGrank
{
    public class BGrank
    {
        public bool done = false;
        public Dictionary<string, string> oppDict = new Dictionary<string, string>();

        Dictionary<string, string> leaderBoard = new Dictionary<string, string>();
        List<string> oppNames = new List<string>();
        
        Mirror mirror = null;
        bool leaderBoardReady = false;
        bool namesReady = false;
        bool failToGetData = false;
        bool playerReady = false;

        public void Reset()
        {
            done = false;
            leaderBoardReady = false;
            namesReady = false;
            failToGetData = false;
            playerReady = false;
            oppNames.Clear();
            oppDict.Clear();
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
            else if(mirror == null)
            {
                mirror = new Mirror { ImageName = "Hearthstone" };
            }

            if (Core.Game.IsInMenu)
            {
                Reset();
            }
            else if(!done && Core.Game.IsBattlegroundsMatch)
            {
                if (failToGetData)
                {
                    done = true;
                }
                else if (leaderBoardReady)
                {
                    if (!namesReady) { GetOppNames(); }
                    else
                    {
                        foreach (string name in oppNames)
                        {
                            if (oppDict.ContainsKey(name)) { continue; }
                            if (leaderBoard.TryGetValue(name, out string value))
                            {
                                oppDict.Add(name, value);
                            }
                            else
                            {
                                oppDict.Add(name, "8000↓");
                            }
                        }
                        done = true;
                    }
                }
            }
        }

        public async Task GetLeaderBoard()
        {
            if (!Core.Game.IsBattlegroundsMatch || leaderBoardReady || failToGetData) { return; }
            string region = GetRegionStr();
            string url = $"https://www.d0nkey.top/leaderboard?leaderboardId=BG&region={region}&limit=50000";
            string[] lines;
            int num_tries = 0;
            string path = $"LeaderBoard_{region}.txt";
            while (num_tries < 3 && !leaderBoardReady)
            {
                try
                {
                    num_tries++;
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "User-Agent-Here");
                        string response = await client.GetStringAsync(url);
                        lines = response.Split('\n');

                        string line, name, rating;
                        int idx, idx2, i = 0;
                        while (true)
                        {
                            if (i == lines.Length) { break; }
                            line = lines[i];
                            i++;

                            idx = line.IndexOf("player-profile/");
                            if (idx > 0)
                            {
                                idx += 15;
                                idx2 = line.IndexOf("\"", idx);
                                name = (line.Substring(idx, idx2 - idx));
                                if (name == string.Empty) { continue; }
                                for (int j = 0; j < 2;)
                                {
                                    i++;
                                    line = lines[i];
                                    if (line.Contains("</td>")) j++;
                                }
                                idx = line.IndexOf(">");
                                idx2 = line.LastIndexOf("<");
                                rating = line.Substring(idx + 1, idx2 - idx - 1);
                                if (!leaderBoard.ContainsKey(name)) { leaderBoard.Add(name, rating); }
                            }
                        }
                    }
                    leaderBoardReady = true;
                    using (StreamWriter writer = new StreamWriter(path))
                    {
                        foreach (var player in leaderBoard)
                        {
                            writer.WriteLine(player.Key + " " + player.Value);
                        }
                    } 
                }
                catch (Exception)
                {
                    await Task.Delay(5000);
                }
            }
            if (!leaderBoardReady) 
            { 
                if(File.Exists(path))
                {
                    using (StreamReader reader = new StreamReader(path))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            string[] tmp = line.Split(' ');
                            oppDict.Add(tmp[0], tmp[1]);
                        }
                    }
                    leaderBoardReady = true;
                }
                else
                {
                    failToGetData = true;
                }
            }
        }

        public string GetRegionStr()
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

        public void GetOppNames()
        {
            if (!playerReady) { return; }

            string myName = Reflection.Client.GetMatchInfo().LocalPlayer.Name;
            var leaderboardMgr = mirror.Root?["PlayerLeaderboardManager"]?["s_instance"];
            var numberOfPlayerTiles = leaderboardMgr?["m_playerTiles"]?["_size"];
            var playerTiles = leaderboardMgr?["m_playerTiles"]?["_items"];

            for (int i = 0; i < numberOfPlayerTiles; i++)
            {
                var playerTile = playerTiles[i];
                // Info not available until the player mouses over the tile in the leaderboard, and there is no other way to get it
                string playerName = playerTile["m_mainCardActor"]?["m_playerNameText"]?["m_Text"];
                if (playerName == null || playerName == String.Empty || playerName == myName) { continue; }
                if (!oppNames.Contains(playerName)) { oppNames.Add(playerName); }
            }
            if (oppNames.Count != 0 && oppNames.Count == numberOfPlayerTiles - 1) { namesReady = true; }
        }
    }
}