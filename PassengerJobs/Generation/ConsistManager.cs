using DV;
using DV.ThingTypes;
using Newtonsoft.Json;
using PassengerJobs.Extensions;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PassengerJobs.Generation
{
    public static class ConsistManager
    {
        // Wrapper class for the json.
        [Serializable]
        public class ConsistDataWrapper
        {
            [Serializable]
            public class Entry
            {
                public string CarType = string.Empty;
                public string[] Liveries = new string[0];
                public int MinAmount = 1;
                public int MaxAmount = 1;
            }

            public bool UseForLocal = true;
            public bool UseForExpress = true;
            public bool AllowCuttingShort = true;
            public Entry[] Entries = new Entry[0];
        }

        public class ConsistData
        {
            public class Entry
            {
                public TrainCarType_v2? CarType;
                public TrainCarLivery[] Liveries = new TrainCarLivery[0];
                public int MinAmount = 1;
                public int MaxAmount = 1;

                public bool IsValid => CarType != null || Liveries.Any();

                public Entry(ConsistDataWrapper.Entry data)
                {
                    if (!Globals.G.Types.TryGetCarType(data.CarType, out CarType))
                    {
                        var list = new List<TrainCarLivery>();

                        foreach (var item in data.Liveries)
                        {
                            if (Globals.G.Types.TryGetLivery(item, out var livery))
                            {
                                list.Add(livery);
                            }
                        }

                        if (list.Count == 0)
                        {
                            PJMain.Error("Consist entry had no valid cartype or liveries!");
                        }

                        Liveries = list.ToArray();
                    }

                    MinAmount = data.MinAmount;
                    MaxAmount = data.MaxAmount;
                }
            }

            public string Name;
            public bool UseForLocal;
            public bool UseForExpress;
            public bool AllowCuttingShort;
            public Entry[] Entries;

            public ConsistData(string name, ConsistDataWrapper data)
            {
                Name = name;
                UseForLocal = data.UseForLocal;
                UseForExpress = data.UseForExpress;
                AllowCuttingShort = data.AllowCuttingShort;
                Entries = data.Entries.Select(x => new Entry(x)).ToArray();
            }

            public List<TrainCarLivery> GetRandomConsist(double maxAllowedLength)
            {
                var list = new List<TrainCarLivery>(Entries.Length);

                foreach (var entry in Entries)
                {
                    if (!entry.IsValid) continue;

                    var count = UnityEngine.Random.Range(entry.MinAmount, entry.MaxAmount + 1);

                    for (int i = 0; i < count; i++)
                    {
                        var livery = entry.CarType != null ? entry.CarType.liveries.GetRandomElement() : entry.Liveries.GetRandomElement();

                        maxAllowedLength -= CarSpawner.Instance.carLiveryToCarLength[livery];

                        // We've exceeded the allowed length, so cut it short.
                        if (AllowCuttingShort && maxAllowedLength < 0) return list;

                        list.Add(livery);
                        maxAllowedLength -= CarSpawner.SEPARATION_BETWEEN_TRAIN_CARS;
                    }
                }

                return list;
            }
        }

        private const string ID_RED = "PassengerRed";
        private const string ID_BLUE = "PassengerBlue";
        private const string ID_GREEN = "PassengerGreen";

        private static readonly Dictionary<string, ConsistData> s_localConsists = new();
        private static readonly Dictionary<string, ConsistData> s_expressConsists = new();

        public static IEnumerable<TrainCarLivery> GetAllPassengerCars()
        {
            return CargoInjector.PassengerCargo.loadableCarTypes.SelectMany(info => info.carType.liveries);
        }

        public static IEnumerable<TrainCarLivery> GetFilteredPassengerCars(RouteType route, double maxTrainsetLength = double.PositiveInfinity)
        {
            var liveries = GetAllPassengerCars();

            // Skip any more checks if CCL isn't loaded.
            if (!CCLIntegration.Loaded) return liveries;

            // Get passenger carrying cars, then filter out things from CCL.
            var filtered = liveries.Where(livery => CCLIntegration.IsLiveryEnabled(livery, route) && TrainsetCheck(livery, maxTrainsetLength));

            if (CCLIntegration.IsCCLPrefered() && filtered.Count() > 3)
            {
                // If CCL is prefered and there are valid entries besides the vanilla ones, remove those.
                // If there isn't any custom car loaded, filtered will only be the original 3 so we don't go in here.
                return filtered.Where(x => x.id is not ID_RED and not ID_BLUE and not ID_GREEN);
            }

            return filtered;

            static bool TrainsetCheck(TrainCarLivery livery, double maxLength)
            {
                // If stuff from trainsets is allowed to spawn alone, no need to do trainset checks.
                if (PJMain.Settings.AllowCCLTrainsetAlone) return true;

                // If it is not part of a CCL set, then it's a lone livery, no need to check more.
                if (!CCLIntegration.TryGetTrainset(livery, out var trainset)) return true;

                // Finally check if the trainset is enabled (all liveries in it active), and if the
                // length of it fits within the requested limits.
                return CCLIntegration.IsTrainsetEnabled(trainset) &&
                    CarSpawner.Instance.GetTotalCarLiveriesLength(trainset.ToList(), true) <= maxLength;
            }
        }

        public static void LoadConsists()
        {
            PJMain.Log("Loading consists...");

            s_localConsists.Clear();
            s_expressConsists.Clear();

            var folder = Path.Combine(PJMain.ModEntry.Path, "Consists");

            foreach (var path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(path);

                if (s_localConsists.ContainsKey(name) || s_expressConsists.ContainsKey(name))
                {
                    PJMain.Error($"Duplicate consist with name '{name}'");
                    continue;
                }

                ConsistDataWrapper? data;

                try
                {
                    using StreamReader reader = File.OpenText(path);
                    data = JsonConvert.DeserializeObject<ConsistDataWrapper>(reader.ReadToEnd());
                }
                catch (Exception ex)
                {
                    PJMain.Error($"Error loading file '{name}':\n{ex}");
                    continue;
                }

                if (data == null)
                {
                    PJMain.Error($"Could not load consist from file '{name}'");
                    continue;
                }

                if (!data.UseForLocal && !data.UseForExpress)
                {
                    PJMain.Warning($"Consist '{name}' is not used on local or express routes");
                    continue;
                }

                var consist = new ConsistData(name, data);

                if (data.UseForLocal)
                {
                    s_localConsists.Add(name, consist);
                }

                if (data.UseForExpress)
                {
                    s_expressConsists.Add(name, consist);
                }

                PJMain.Log($"Loaded consist '{name}'");
            }

            //PrintTemplateConsist();
        }

        public static bool HasConsists(RouteType route) => route switch
        {
            RouteType.Express => s_expressConsists.Count > 0,
            RouteType.Local => s_localConsists.Count > 0,
            _ => false,
        };

        public static bool GetConsist(RouteType routeType, double maxAllowedLength, out List<TrainCarLivery> liveries)
        {
            Dictionary<string, ConsistData> dict;

            switch (routeType)
            {
                case RouteType.Express:
                    dict = s_expressConsists;
                    break;
                case RouteType.Local:
                    dict = s_localConsists;
                    break;
                default:
                    liveries = new List<TrainCarLivery>();
                    return false;
            }

            // It's checked before but eh.
            if (dict.Count == 0)
            {
                liveries = new List<TrainCarLivery>();
                return false;
            }

            for (int i = 0; i < 5; i++)
            {
                var consist = dict.PickOneValue();
                liveries = consist!.Value.Value.GetRandomConsist(maxAllowedLength);

                if (CarSpawner.Instance.GetTotalCarLiveriesLength(liveries) <= maxAllowedLength)
                {
                    return true;
                }
            }

            liveries = new List<TrainCarLivery>();
            return false;
        }

        private static void PrintTemplateConsist()
        {
            var data = new ConsistDataWrapper
            {
                Entries = new ConsistDataWrapper.Entry[]
                {
                    new()
                    {
                        Liveries = new string[]
                        {
                            ID_RED,
                            ID_BLUE
                        }
                    }
                }
            };

            PJMain.Log(JsonConvert.SerializeObject(data));
        }
    }
}
