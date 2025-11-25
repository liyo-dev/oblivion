using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CelestialCyclesSystem
{
    [CreateAssetMenu(fileName = "FullCycle", menuName = "Celestial Cycles/Day", order = 0)]
    public class CelestialCycleDay : ScriptableObject
    {
        public CelestialCyclePeriod morningSettings;
        public CelestialCyclePeriod noonSettings;
        public CelestialCyclePeriod eveningSettings;
        public CelestialCyclePeriod nightSettings;
    }
}
