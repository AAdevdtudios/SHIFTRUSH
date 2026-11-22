using ShiftRush.Core;

namespace ShiftRush.Vehicles
{
    /// <summary>What each car type promises, in the terms the rest of the game uses.</summary>
    public static class CarTypes
    {
        /// <summary>The drift character a type gets: muscle and tuner drifty, race car and heavy grippy, the rest balanced.</summary>
        public static DriftCharacter Drift(CarType t)
        {
            switch (t)
            {
                case CarType.Muscle:
                case CarType.Tuner: return DriftCharacter.Drifty;
                case CarType.RaceCar:
                case CarType.Heavy: return DriftCharacter.Grippy;
                default: return DriftCharacter.Balanced;
            }
        }

        /// <summary>"ALL-ROUNDER", "RACE CAR" and so on, for cards and labels.</summary>
        public static string Name(CarType t)
        {
            switch (t)
            {
                case CarType.AllRounder: return "ALL-ROUNDER";
                case CarType.RaceCar: return "RACE CAR";
                default: return t.ToString().ToUpperInvariant();
            }
        }
    }
}
