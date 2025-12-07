using DV.ThingTypes;
using System;
using System.Reflection;
using UnityModManagerNet;

namespace PassengerJobs
{
    internal class CCLIntegration
    {
        private static bool _loaded = false;
        private static MethodInfo? _getTrainset;
        private static MethodInfo? _trainsetEnabled;
        private static MethodInfo? _liveryEnabled;

        public static bool Loaded
        {
            get
            {
                if (_loaded) return true;
                TryLoad();
                return _loaded;
            }
        }

        private static void TryLoad()
        {
            if (_loaded) return;

            var ccl = UnityModManager.FindMod("DVCustomCarLoader");

            if (ccl == null) return;

            var manager = ccl.Assembly.GetType("CCL.Importer.CarManager");
            _getTrainset = manager.GetMethod("GetTrainsetForLivery");
            _trainsetEnabled = manager.GetMethod("IsTrainsetEnabled");
            _liveryEnabled = manager.GetMethod("IsCarLiveryEnabled");
            _loaded = true;
        }

        public static bool TryGetTrainset(TrainCarLivery livery, out TrainCarLivery[] result)
        {
            if (!Loaded)
            {
                result = Array.Empty<TrainCarLivery>();
                return false;
            }

            result = GetTrainset(livery);
            return result.Length > 0;
        }

        private static TrainCarLivery[] GetTrainset(TrainCarLivery livery)
        {
            if (_getTrainset == null)
            {
                return Array.Empty<TrainCarLivery>();
            }

            return (TrainCarLivery[])_getTrainset.Invoke(null, new object[] { livery });
        }

        public static bool IsTrainsetEnabled(TrainCarLivery[] trainset)
        {
            if (!Loaded || _trainsetEnabled == null) return true;

            return (bool)_trainsetEnabled.Invoke(null, new object[] { trainset, true });
        }

        public static bool IsLiveryEnabled(TrainCarLivery livery)
        {
            if (!Loaded || _liveryEnabled == null) return true;

            return (bool)_liveryEnabled.Invoke(null, new object[] { livery });
        }
    }
}
