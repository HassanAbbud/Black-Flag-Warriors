namespace Ahoy.Player
{
    // Command pattern: a press becomes an intent with a timestamp, consumed when the game
    // is ready. Stale intents expire instead of firing late.
    public struct InputBuffer
    {
        float pressedAt;
        bool pending;

        public void Press(float now)
        {
            pressedAt = now;
            pending = true;
        }

        public bool IsPending(float now, float window) => pending && now - pressedAt <= window;

        public float PressedAt => pressedAt;

        public void Clear() => pending = false;
    }
}
