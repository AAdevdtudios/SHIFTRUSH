namespace ShiftRush.Vehicles
{
    /// <summary>The car's nitro reserve. In races nitro only comes from SHIFT ramps, so regeneration is switched off.</summary>
    public interface INitroTank
    {
        /// <summary>0..1, or -1 when the car has no nitro.</summary>
        float Nitro01 { get; }
        void SetNitro(float fill01);
        void AddNitro(float fill01);
        void SetRegeneration(bool on);
        /// <summary>Off while the SHIFT timing bar is up: the nitro key (Shift) is the timing key then.</summary>
        void SetNitroInput(bool on);
    }
}
