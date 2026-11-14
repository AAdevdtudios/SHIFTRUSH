namespace ShiftRush.Vehicles
{
    /// <summary>Lets code drive a car instead of the player (rival AI). The vehicle package applies the inputs.</summary>
    public interface IDriverInput
    {
        /// <summary>steer -1..1, throttle/brake/handbrake 0..1. Call every physics step while driving.</summary>
        void SetDriverInput(float steer, float throttle, float brake, float handbrake, bool nitro);
        void ReleaseDriverInput();
    }
}
