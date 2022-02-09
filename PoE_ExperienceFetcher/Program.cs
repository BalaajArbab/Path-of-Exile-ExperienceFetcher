using System;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using PoE_ExperienceFetcher.Json_Objects;

namespace PoE_ExperienceFetcher
{

    class Program
    {

        #region API data
        static string league;
        static string ladderEndpoint;
        static string userAgent;
        #endregion


        #region Paths
        const string parent = "..\\..\\..\\";
        const string csvFilePathNew = parent + "Experience_Table.csv";
        const string csvFilePathOld = parent + "Experience_Table_Old.csv";
        const string level100CSVFilePath = parent + "Experience_Table_Level100.csv";
        const string callsFilePath = parent + "calls.txt";
        const string configPath = parent + "config.cfg";
        #endregion


        static HttpClient httpClient = new HttpClient();

        static int waitTime;
        static int calls;

        public static void Main(string[] args)
        {
            setConfig();
            ladderEndpoint = "/ladders/" + league + "?realm=pc&sort=xp&limit=40";

            httpClient.BaseAddress = new Uri("https://api.pathofexile.com");
            httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            string rateLimit;

            Stopwatch sw = new Stopwatch();
            sw.Start();


            Dictionary<string, List<string>> characters;
            List<string> dates;

            characters = readCSV(out dates);
            readCalls();

            int startCalls = calls;
            bool level100 = false;
            bool level100Snapshot = File.Exists(@"..\..\..\Experience_Table_Level100.csv") ? true : false;

            // Main Loop
            while (true) 
            {
                if (sw.ElapsedMilliseconds / 1000 >= waitTime || calls == startCalls)
                {
                    calls++;

                    if (calls % 10 == 0)
                    {
                        writeCSV(csvFilePathOld, characters, dates);
                    }
                    
                    // API Call - returns JSON
                    string json = getJson(out rateLimit);

                    DateTime callMadeAt = DateTime.Now;
                    long unixTime = ((DateTimeOffset)callMadeAt).ToUnixTimeSeconds();

                    dates.Add(unixTime.ToString());

                    if (json == "{}")
                    {
                        Thread.Sleep(waitTime);
                        continue;
                    }

                    setWaitTime(rateLimit);
                    Ladder ladder = readJson(json);

                    foreach (LadderEntry l in ladder.Entries)
                    {

                        if (l.Character.Level == 100) level100 = true;

                        if (!characters.ContainsKey(l.Character.Id))
                        {
                            addCharToDictionary(l, characters);       
                            
                        }

                        else
                        {
                            List<string> values = characters[l.Character.Id];

                            values[(int)CSVFileValueIndexing.Rank] = l.Rank.ToString();
                            values[(int)CSVFileValueIndexing.Level] = l.Character.Level.ToString();
                            values[(int)CSVFileValueIndexing.Class] = l.Character.Class;
                            values[(int)CSVFileValueIndexing.Experience] = l.Character.Experience.ToString();
                            values[(int)CSVFileValueIndexing.Dead] = l.Dead.ToString();

                            if (!l.Dead) values.Add(l.Character.Experience.ToString());

                            else
                            {
                                string deadExp = "DEAD " + l.Character.Experience.ToString();
                                values.Add(deadExp);
                            }
                        }

                    }

                    foreach (KeyValuePair<string, List<string>> character in characters)
                    {
                        if (!ladder.ContainsId(character.Key))
                        {
                            character.Value[(int)CSVFileValueIndexing.Rank] = "404";
                            character.Value.Add("N/A");

                        }
                    }

                    if (calls % 10 == 0 || calls == 1)
                    {
                        writeCSV(csvFilePathNew, characters, dates);
                        writeCalls();

                    }

                    if (level100 && !level100Snapshot)
                    {
                        writeCSV(level100CSVFilePath, characters, dates);
                        level100Snapshot = true;
                    }

                    Console.WriteLine("Ladder entries: " + ladder.Entries.Length + "\nCalls: " + calls);

                    sw.Restart();
                }
                else
                {
                    int sleepTime = waitTime - (int)(sw.ElapsedMilliseconds / 1000);
                    Console.WriteLine($"Sleeping for {sleepTime} at call {calls}. Current Wait Time between calls: {waitTime}\n");
                    Thread.Sleep(sleepTime * 1000);
                }
            }

            
        }

        private static void addCharToDictionary(LadderEntry l, Dictionary<string, List<string>> characters)
        {
            List<string> values = new List<string>();

            values.Add(l.Account.Name);
            values.Add(l.Character.Name);
            values.Add(l.Character.Class);
            values.Add(l.Rank.ToString());
            values.Add(l.Character.Level.ToString());
            values.Add(l.Character.Experience.ToString());
            values.Add(l.Dead.ToString());
            values.Add(l.Character.Experience.ToString());

            for (int i = 0; i < calls - 1; i++) values.Add("N/A");

            characters[l.Character.Id] = values;
        }
        private static void setWaitTime(string rateLimit)
        {
            string[] rates = rateLimit.Split(":");

            float secondsBetweenHits = int.Parse(rates[1]) / float.Parse(rates[0]);
            Math.Round(secondsBetweenHits, MidpointRounding.ToPositiveInfinity);

            if (secondsBetweenHits > waitTime) waitTime = (int)secondsBetweenHits;

            else if (secondsBetweenHits > 300) waitTime = (int)secondsBetweenHits;

            else if (waitTime > 300) waitTime = 300;
        }

        #region File I/O
        private static void readCalls()
        {
            try
            {
                using (StreamReader sr = new StreamReader(callsFilePath))
                {
                    calls = int.Parse(sr.ReadLine());
                }
            }
            catch (Exception ex)
            {
                StreamWriter sw = new StreamWriter(callsFilePath);
                sw.WriteLine(0);
                sw.Close();
            }
        }

        private static void writeCalls()
        {
            using (StreamWriter sw = new StreamWriter(callsFilePath))
            {
                sw.WriteLine(calls);
            }
        }

        private static Dictionary<string, List<string>> readCSV(out List<string> dates)
        {
            dates = new List<string>();
            Dictionary<string, List<string>> characters = new();

            try
            {
                using (StreamReader csv = new StreamReader(csvFilePathNew))
                {
                    if (csv.Peek() > -1)
                    {
                        string[] firstLine = csv.ReadLine().Split(",");
                        dates = firstLine.Take(new Range(8, firstLine.Length)).ToList();
                    }
                                       
                    while (csv.Peek() > -1)
                    {
                        string[] line = csv.ReadLine().Split(",");

                        string id = line.First();

                        Range r = new Range(1, line.Length);
                        List<string> value = line.Take(r).ToList();

                        characters[id] = value;

                    }

                    return characters;
                }
            }
            catch (Exception ex)
            {
                StreamWriter sw = new StreamWriter(csvFilePathNew);
                sw.Write("");
                sw.Close();

                return characters;

            }

        }

        private static void writeCSV(string csvFilePath, Dictionary<string, List<string>> characters, List<string> dates)
        {

            using (StreamWriter csv = new StreamWriter(csvFilePath))
            {
                csv.Write("ID,AccountName,CharacterName,Class,Rank,Level,Experience,Dead,");

                foreach (string date in dates)
                {
                    if (date == dates[dates.Count - 1]) csv.Write(date);
                    else csv.Write(date + ",");
                }
                csv.WriteLine();

                List<KeyValuePair<string, List<string>>> entries = new List<KeyValuePair<string, List<string>>>();

                KeyValuePair<string, List<string>>[] arr = characters.ToArray<KeyValuePair<string, List<string>>>();


                // Sort in order of rank before writing to .CSV.
                for (int i = 0; i < arr.Length; i++)
                {
                    int smallest = int.MaxValue;
                    int index = -1;

                    for (int j = i; j < arr.Length; j++)
                    {

                        int rank = int.Parse(arr[j].Value[(int)CSVFileValueIndexing.Rank]);
                        if (rank < smallest)
                        {
                            smallest = rank;
                            index = j;

                                
                        }

                    }

                    entries.Add(arr[index]);

                    KeyValuePair<string, List<string>> temp = arr[i];
                    arr[i] = arr[index];
                    arr[index] = temp;                  
                    
                }

                foreach (KeyValuePair<string, List<string>> character in entries)
                {
                    csv.Write(character.Key + ",");

                    for (int i = 0; i < character.Value.Count; i++)
                    {
                        if (i == character.Value.Count - 1) csv.Write(character.Value[i]);
                        else csv.Write(character.Value[i] + ",");
                    }

                    csv.WriteLine();
                }

            }

        }
        #endregion

        #region Json Deserialize
        private static Ladder readJson(string json)
        {
            JsonSerializerOptions options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Ladder>(json, options);
        }
        #endregion

        #region HTTP Request
        private static string getJson(out string rateLimit)
        {

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, ladderEndpoint);

            HttpResponseMessage response = httpClient.Send(request);

            rateLimit = "";

            try
            {
                response.EnsureSuccessStatusCode();

                rateLimit = response.Headers.GetValues("X-Rate-Limit-Ip").ElementAt(0).Split(",")[0];
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);

                return "{}";
            }

            //Console.WriteLine(response);

            using (Stream s = response.Content.ReadAsStream())
            {
                StreamReader sr = new StreamReader(s);

                return sr.ReadToEnd();
            }

        }
        #endregion

        #region Config
        public static void setConfig()
        {
            using (StreamReader sr = new StreamReader(configPath))
            {
                while (sr.Peek() > -1)
                {
                    string line = sr.ReadLine();
                    line = line.Trim();

                    if (line == "" || line[0] == '#') continue;

                    string[] arr = line.Split("=");

                    arr[0] = arr[0].Trim();
                    arr[1] = arr[1].Trim();

                    switch (arr[0])
                    {
                        case "league":
                            league = arr[1];
                            break;
                        case "UserAgent":
                            userAgent = arr[1];
                            break;
                        case "WaitTime":
                            waitTime = int.Parse(arr[1]);
                            break;
                    }                       

                }
            }
        }
        #endregion




    }

    public enum CSVFileValueIndexing
    {
        AccountName,
        CharacterName,
        Class,
        Rank,
        Level,
        Experience,
        Dead
    }
}
