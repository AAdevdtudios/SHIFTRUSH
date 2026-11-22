namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Lets the held drift (<see cref="DriftMoves"/>) read the handbrake the player is pressing and take it away from the
    /// car controller while the drift is running, so the wheels don't lock and kill the slide.
    /// </summary>
    public interface IDriftInput
    {
        /// <summary>Handbrake as pressed (0..1), before any muting.</summary>
        float HandbrakeInput { get; }
        /// <summary>Steering as the player is pressing it (-1..1), before the car controller's speed limiter: at speed
        /// RCC Pro lets the wheels turn only a little, but a drift needs to know how hard the player is steering.</summary>
        float SteerIntent { get; }
        /// <summary>While true the car controller sees no handbrake.</summary>
        bool MuteHandbrake { get; set; }
    }
}
