using System;
using LeagueSharp.Common;
using SFXChallenger.Library;

namespace TwistedFate
{
    public class Bootstrap
    {

        /// <summary>
        /// The champion
        /// </summary>
        private static Champion _champion;

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public static void Init()
        {
            GameObjects.Initialize();

            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        /// <summary>
        /// The on game load event.
        /// </summary>
        /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
        private static void Game_OnGameLoad(EventArgs args)
        {
            _champion = new Champion();
            Core.Init(_champion);
            Core.Start();
        }
    }
}
