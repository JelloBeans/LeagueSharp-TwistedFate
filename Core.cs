using System;
using LeagueSharp;
using LeagueSharp.Common;
using SFXChallenger.Library.Logger;

namespace TwistedFate
{
    internal class Core
    {
        /// <summary>
        /// The tick interval
        /// </summary>
        private const int TickInterval = 50;

        /// <summary>
        /// The champion
        /// </summary>
        private static Champion _champion;

        /// <summary>
        /// The started flag
        /// </summary>
        private static bool _started;

        /// <summary>
        /// The last tick
        /// </summary>
        private static int _lastTick;

        /// <summary>
        /// Initializes the specified champion.
        /// </summary>
        /// <param name="champion">The champion.</param>
        public static void Init(Champion champion)
        {
            _champion = champion;
        }

        /// <summary>
        /// Handles the update event.
        /// </summary>
        /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
        private static void Game_OnUpdate(EventArgs args)
        {
            if (_champion == null)
            {
                return;
            }

            try
            {
                if (Environment.TickCount - _lastTick < TickInterval)
                {
                    return;
                }

                _lastTick = Environment.TickCount;

                if (ObjectManager.Player.IsDead || ObjectManager.Player.HasBuff("Recall"))
                {
                    return;
                }

                try
                {
                    _champion.PreUpdate();

                    _champion.Killsteal();

                    switch (_champion.Orbwalker.ActiveMode)
                    {
                        case Orbwalking.OrbwalkingMode.Combo:
                            _champion.Combo();
                            break;
                        case Orbwalking.OrbwalkingMode.LaneClear:
                            _champion.LaneClear();
                            break;
                        case Orbwalking.OrbwalkingMode.LastHit:
                            _champion.LastHit();
                            break;
                        case Orbwalking.OrbwalkingMode.Mixed:
                            _champion.Harass();
                            break;
                        case Orbwalking.OrbwalkingMode.CustomMode:
                            _champion.Flee();
                            break;
                    }

                    _champion.PostUpdate();
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public static void Start()
        {
            if (_started)
            {
                return;
            }

            _champion?.SetupSpells();
            _champion?.SetupMenu();
            _champion?.HookEvents();

            Game.OnUpdate += Game_OnUpdate;
            CustomEvents.Game.OnGameEnd += Game_OnGameEnd;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_Exit;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_Exit;

            _started = true;
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public static void Stop()
        {
            if (!_started)
            {
                return;
            }

            Game.OnUpdate -= Game_OnUpdate;
            CustomEvents.Game.OnGameEnd -= Game_OnGameEnd;
            AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_Exit;
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_Exit;

            _started = false;
        }

        /// <summary>
        /// Handles the Exit event of the CurrentDomain control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private static void CurrentDomain_Exit(object sender, EventArgs e)
        {
            Stop();
        }

        /// <summary>
        /// Handles the game end event
        /// </summary>
        /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
        private static void Game_OnGameEnd(EventArgs args)
        {
            Stop();
        }
    }
}
