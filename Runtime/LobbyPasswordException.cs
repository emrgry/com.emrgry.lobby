using System;

namespace Emrgry.Lobby
{
    /// <summary>
    /// Thrown when joining a password-protected lobby fails because the password
    /// is missing or incorrect (rejected server-side by UGS).
    /// </summary>
    public sealed class LobbyPasswordException : Exception
    {
        public LobbyPasswordException(Exception inner)
            : base("Lobby password missing or incorrect.", inner)
        {
        }
    }
}
