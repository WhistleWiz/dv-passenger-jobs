using DV.ThingTypes;
using PassengerJobs.Injectors;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.Generation
{
    public static class ConsistManager
    {
        public static IEnumerable<TrainCarLivery> GetPassengerCars(double maxTrainsetLength = double.PositiveInfinity)
        {
            return CargoInjector.PassengerCargo.loadableCarTypes
                .SelectMany(info => info.carType.liveries)
                .Where(livery => CCLIntegration.IsLiveryEnabled(livery) && TrainsetCheck(livery, maxTrainsetLength));

            static bool TrainsetCheck(TrainCarLivery livery, double maxLength)
            {
                if (PJMain.Settings.AllowCCLTrainsetAlone) return true;

                if (!CCLIntegration.TryGetTrainset(livery, out var trainset)) return true;

                return CCLIntegration.IsTrainsetEnabled(trainset) &&
                    CarSpawner.Instance.GetTotalCarLiveriesLength(trainset.ToList(), true) <= maxLength;
            }
        }
    }
}
