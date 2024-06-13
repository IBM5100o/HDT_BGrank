﻿using System;
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
        private bool isReset = true;

        private Mirror mirror = null;
        private List<string> oppNames = new List<string>();
        private Dictionary<string, int> unsortDict = new Dictionary<string, int>();
        private Dictionary<string, string> leaderBoard = new Dictionary<string, string>();

        private void Reset()
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
            oppDict = new Dictionary<string, string>();
            oppNames = new List<string>();
            unsortDict = new Dictionary<string, int>();
            leaderBoard = new Dictionary<string, string>();
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
                if (!isReset)
                {
                    Reset();
                    isReset = true;
                }
            }
            else if (!done && Core.Game.IsBattlegroundsMatch)
            {
                isReset = false;
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

            // Get the leaderboard information from: https://bgrank.fly.dev/
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

            // The code below is from: https://github.com/Zero-to-Heroes/unity-spy-.net4.5
            try
            {
                string myName = Reflection.Client.GetMatchInfo().LocalPlayer.Name;
                var leaderboardMgr = mirror.Root?["PlayerLeaderboardManager"]?["s_instance"];
                dynamic[] playerTiles = GetPlayerTiles(leaderboardMgr);
                var numberOfPlayerTiles = playerTiles?.Length ?? 0;

                for (int i = 0; i < numberOfPlayerTiles; i++)
                {
                    var playerTile = playerTiles[i];
                    // Info not available until the player mouses over the tile in the leaderboard, and there is no other way to get it
                    string playerName = playerTile["m_overlay"]?["m_heroActor"]?["m_playerNameText"]?["m_Text"];
                    if (string.IsNullOrEmpty(playerName)) { continue; }
                    if (playerName != myName && !oppNames.Contains(playerName)) { oppNames.Add(playerName); }
                }
                if (oppNames.Count == numberOfPlayerTiles-1) { namesReady = true; }
            }
            catch (Exception) { }
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