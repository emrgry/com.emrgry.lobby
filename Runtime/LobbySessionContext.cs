namespace Emrgry.Lobby
{
    /// <summary>
    /// Process-wide cache of the most recently joined/created <see cref="LobbyInfo"/>.
    /// UI on a different scene than the create/join call site can read the current
    /// lobby code / id without holding a reference through scene loads.
    ///
    /// <para>Populated by <c>LobbyConnectionFlow.HostGameAsync</c> / <c>JoinGameByIdAsync</c>;
    /// cleared by <c>LeaveGameAsync</c>. Callers may also set it manually.</para>
    /// </summary>
    public static class LobbySessionContext
    {
        private static LobbyInfo _current;
        private static bool _hasValue;

        public static bool HasValue => _hasValue;
        public static LobbyInfo Current => _current;

        public static void Set(LobbyInfo info)
        {
            _current = info;
            _hasValue = true;
        }

        public static void Clear()
        {
            _current = default;
            _hasValue = false;
        }
    }
}
