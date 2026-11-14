namespace ShiftRush.Vehicles
{
    /// <summary>Dashboard readings for the race HUD's speedometer (optional: a car without it shows speed only).</summary>
    public interface IVehicleDash
    {
        /// <summary>Engine speed between idle (0) and redline (1).</summary>
        float Rpm01 { get; }
        /// <summary>"R", "N" or the forward gear number.</summary>
        string GearLabel { get; }
        /// <summary>Nitro left, 0..1, or -1 when the car has none.</summary>
        float Nitro01 { get; }
        bool NitroActive { get; }
    }
}
