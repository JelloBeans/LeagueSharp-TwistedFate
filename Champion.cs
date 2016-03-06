using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXChallenger.Args;
using SFXChallenger.Enumerations;
using SFXChallenger.Library;
using SFXChallenger.Library.Extensions.NET;
using SFXChallenger.Library.Logger;
using SFXChallenger.Managers;
using SharpDX;
using MinionManager = SFXChallenger.Library.MinionManager;
using MinionOrderTypes = SFXChallenger.Library.MinionOrderTypes;
using MinionTeam = SFXChallenger.Library.MinionTeam;
using MinionTypes = SFXChallenger.Library.MinionTypes;
using Utils = LeagueSharp.Common.Utils;

namespace TwistedFate
{
    internal class Champion
    {
        /// <summary>
        /// The q angle
        /// </summary>
        private const float QAngle = 28 * (float)Math.PI / 180;

        /// <summary>
        /// The w red radius
        /// </summary>
        private const float WRedRadius = 200f;

        /// <summary>
        /// The w target
        /// </summary>
        private Obj_AI_Hero _wTarget;

        /// <summary>
        /// The w target end time
        /// </summary>
        private float _wTargetEndTime;

        /// <summary>
        /// The e
        /// </summary>
        protected Spell E;

        /// <summary>
        /// The q
        /// </summary>
        protected Spell Q;

        /// <summary>
        /// The r
        /// </summary>
        protected Spell R;

        /// <summary>
        /// The w
        /// </summary>
        protected Spell W;

        /// <summary>
        /// The player
        /// </summary>
        protected readonly Obj_AI_Hero Player = ObjectManager.Player;

        /// <summary>
        /// Gets the orbwalker.
        /// </summary>
        /// <value>
        /// The orbwalker.
        /// </value>
        public Orbwalking.Orbwalker Orbwalker { get; private set; }

        /// <summary>
        /// Gets the menu.
        /// </summary>
        /// <value>
        /// The menu.
        /// </value>
        public Menu Menu { get; private set; }

        /// <summary>
        /// Setups the menu.
        /// </summary>
        public void SetupMenu()
        {
            try
            {
                Menu = new Menu(Global.Name, "tf", true);

                Orbwalker = new Orbwalking.Orbwalker(Menu.AddSubMenu(new Menu("Orbwalker", $"{Menu.Name}.orb")));
                Orbwalker.RegisterCustomMode($"{Menu.Name}.orb.flee", "Flee", '8');

                SetupComboMenu();

                SetupHarassMenu();

                SetupLaneClearMenu();

                var fleeMenu = Menu.AddSubMenu(new Menu("Flee", $"{Menu.Name}.flee"));
                fleeMenu.AddItem(new MenuItem($"{fleeMenu.Name}.w", "Use Gold Card").SetValue(true));

                SetupMiscMenu();

                SetupManualMenu();

                Q.Range = Menu.Item($"{Menu.Name}.miscellaneous.q-range").GetValue<Slider>().Value;
                W.Range = Menu.Item($"{Menu.Name}.miscellaneous.w-range").GetValue<Slider>().Value;
                Cards.Delay = Menu.Item($"{Menu.Name}.miscellaneous.w-delay").GetValue<Slider>().Value;

                Menu.AddToMainMenu();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Setups the manual menu.
        /// </summary>
        private void SetupManualMenu()
        {
            var manualMenu = Menu.AddSubMenu(new Menu("Manual", $"{Menu.Name}.manual"));
            manualMenu.AddItem(new MenuItem($"{manualMenu.Name}.blue", "Hotkey Blue").SetValue(new KeyBind('T', KeyBindType.Press)));
            manualMenu.AddItem(new MenuItem($"{manualMenu.Name}.red", "Hotkey Red").SetValue(new KeyBind('Y', KeyBindType.Press)));
            manualMenu.AddItem(new MenuItem($"{manualMenu.Name}.gold", "Hotkey Gold").SetValue(new KeyBind('U', KeyBindType.Press)));
        }

        /// <summary>
        /// Setups the misc menu.
        /// </summary>
        private void SetupMiscMenu()
        {
            var miscMenu = Menu.AddSubMenu(new Menu("Misc", Menu.Name + ".miscellaneous"));

            var qImmobileMenu = miscMenu.AddSubMenu(new Menu("Q Immobile", $"{miscMenu.Name}.q-immobile"));
            HeroListManager.AddToMenu(
                qImmobileMenu,
                new HeroListManagerArgs("q-immobile")
                {
                    IsWhitelist = false,
                    Allies = false,
                    Enemies = true,
                    DefaultValue = false
                });
            BestTargetOnlyManager.AddToMenu(qImmobileMenu, "q-immobile", true);

            miscMenu.AddItem(new MenuItem($"{miscMenu.Name}.q-range", "Q Range")
                .SetValue(new Slider((int)Q.Range, 500, (int)Q.Range))
            ).ValueChanged += (sender, args) => { Q.Range = args.GetNewValue<Slider>().Value; };

            miscMenu.AddItem(new MenuItem($"{miscMenu.Name}.w-range", "Card Pick Distance")
                .SetValue(new Slider((int)W.Range, 500, 800))
            ).ValueChanged += (sender, args) => { W.Range = args.GetNewValue<Slider>().Value; };

            miscMenu.AddItem(new MenuItem($"{miscMenu.Name}.w-delay", "Card Pick Delay")
                .SetValue(new Slider(150, 0, 400))
            ).ValueChanged += (sender, args) => { Cards.Delay = args.GetNewValue<Slider>().Value; };

            miscMenu.AddItem(new MenuItem($"{miscMenu.Name}.mode", "W Mode")
                .SetValue(new StringList(new[] { "Burst", "Team" }))
            );

            miscMenu.AddItem(new MenuItem($"{miscMenu.Name}.r-card", "Pick Card on R").SetValue(true));
        }

        /// <summary>
        /// Setups the lane clear menu.
        /// </summary>
        private void SetupLaneClearMenu()
        {
            var laneclearMenu = Menu.AddSubMenu(new Menu("Lane Clear", $"{Menu.Name}.lane-clear"));
            ResourceManager.AddToMenu(laneclearMenu, new ResourceManagerArgs("lane-clear", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Advanced = true,
                    MaxValue = 101,
                    LevelRanges = new SortedList<int, int> {{1, 6}, {6, 12}, {12, 18}},
                    DefaultValues = new List<int> {50, 50, 50},
                    IgnoreJungleOption = true
                });
            ResourceManager.AddToMenu(laneclearMenu, new ResourceManagerArgs("lane-clear-blue", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Prefix = "Blue",
                    Advanced = true,
                    MaxValue = 101,
                    LevelRanges = new SortedList<int, int> {{1, 6}, {6, 12}, {12, 18}},
                    DefaultValues = new List<int> {60, 60, 60},
                    IgnoreJungleOption = true
                });
            laneclearMenu.AddItem(new MenuItem($"{laneclearMenu.Name}.q-min", "Q Min.").SetValue(new Slider(4, 1, 5)));
            laneclearMenu.AddItem(new MenuItem($"{laneclearMenu.Name}.q", "Use Q").SetValue(true));
            laneclearMenu.AddItem(new MenuItem($"{laneclearMenu.Name}.w", "Use W").SetValue(true));
        }

        /// <summary>
        /// Setups the harass menu.
        /// </summary>
        private void SetupHarassMenu()
        {
            var harassMenu = Menu.AddSubMenu(new Menu("Harass", $"{Menu.Name}.harass"));
            HitchanceManager.AddToMenu(harassMenu.AddSubMenu(new Menu("Hitchance", $"{harassMenu.Name}.hitchance")), "harass",
                new Dictionary<string, HitChance> {{"Q", HitChance.High}});
            ResourceManager.AddToMenu(harassMenu, new ResourceManagerArgs("harass", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    DefaultValue = 30
                });
            ResourceManager.AddToMenu(harassMenu, new ResourceManagerArgs("harass-blue", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Prefix = "W Blue",
                    DefaultValue = 50
                });
            harassMenu.AddItem(new MenuItem($"{harassMenu.Name}.w-card", "Pick Card").SetValue(
                    new StringList(new[] {"Auto", "Gold", "Red", "Blue"}, 3)));
            harassMenu.AddItem(new MenuItem($"{harassMenu.Name}.q", "Use Q").SetValue(true));
            harassMenu.AddItem(new MenuItem($"{harassMenu.Name}.w", "Use W").SetValue(true));
        }

        /// <summary>
        /// Setups the combo menu.
        /// </summary>
        private void SetupComboMenu()
        {
            var comboMenu = Menu.AddSubMenu(new Menu("Combo", $"{Menu.Name}.combo"));
            HitchanceManager.AddToMenu(comboMenu.AddSubMenu(new Menu("Hitchance", $"{comboMenu.Name}.hitchance")), "combo",
                new Dictionary<string, HitChance> {{"Q", HitChance.High}});
            comboMenu.AddItem(
                new MenuItem(comboMenu.Name + ".gold-percent", "Pick Gold Health <= %").SetValue(new Slider(20, 5, 75)));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".red-min", "Pick Red Targets >=").SetValue(new Slider(3, 1, 5)));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", "Use Q").SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".w", "Use W").SetValue(true));
        }

        /// <summary>
        /// Setups the spells.
        /// </summary>
        public void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 1450f, TargetSelector.DamageType.Magical);
            Q.SetSkillshot(0.25f, 40f, 1000f, false, SkillshotType.SkillshotLine);

            W = new Spell(SpellSlot.W, 550f, TargetSelector.DamageType.Magical);
            W.SetSkillshot(0.5f, 100f, Player.BasicAttack.MissileSpeed, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E);

            R = new Spell(SpellSlot.R, 5500f);
        }

        /// <summary>
        /// Hooks the events.
        /// </summary>
        public void HookEvents()
        {
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            Drawing.OnEndScene += OnDrawingEndScene;
        }

        private void Orbwalking_BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
        }

        private void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            try
            {
                if (!sender.IsMe || !Menu.Item(Menu.Name + ".miscellaneous.r-card").GetValue<bool>())
                {
                    return;
                }
                if (args.SData.Name.Equals("gate", StringComparison.OrdinalIgnoreCase) && W.IsReady())
                {
                    if (Cards.Status != SelectStatus.Selected)
                    {
                        Cards.Select(CardColor.Gold);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Pre update.
        /// </summary>
        public void PreUpdate()
        {
            
        }

        /// <summary>
        /// Post update.
        /// </summary>
        public void PostUpdate()
        {
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear ||
                Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit)
            {
                if (Cards.Has(CardColor.Red))
                {
                    var range = Player.AttackRange + Player.BoundingRadius * 1.5f;
                    var minions = MinionManager.GetMinions(range, MinionTypes.All, MinionTeam.NotAlly);
                    var pred = MinionManager.GetBestCircularFarmLocation(
                        minions.Select(m => m.Position.To2D()).ToList(), 500, range);
                    var target = minions.OrderBy(m => m.Distance(pred.Position)).FirstOrDefault();
                    if (target != null)
                    {
                        Orbwalker.ForceTarget(target);
                    }
                }
            }
            if (!Cards.ShouldWait && Cards.Status != SelectStatus.Selecting && Cards.Status != SelectStatus.Selected)
            {
                Orbwalker.ForceTarget(null);
            }
            if (Cards.Status != SelectStatus.Selected)
            {
                if (Menu.Item(Menu.Name + ".manual.blue").GetValue<KeyBind>().Active)
                {
                    Cards.Select(CardColor.Blue);
                }
                if (Menu.Item(Menu.Name + ".manual.red").GetValue<KeyBind>().Active)
                {
                    Cards.Select(CardColor.Red);
                }
                if (Menu.Item(Menu.Name + ".manual.gold").GetValue<KeyBind>().Active)
                {
                    Cards.Select(CardColor.Gold);
                }
            }
            if (HeroListManager.Enabled("q-immobile") && Q.IsReady())
            {
                var target =
                    GameObjects.EnemyHeroes.FirstOrDefault(
                        t =>
                            t.IsValidTarget(Q.Range) && HeroListManager.Check("q-immobile", t) &&
                            BestTargetOnlyManager.Check("q-immobile", W, t) && IsImmobile(t));
                if (target != null)
                {
                    var best = BestQPosition(
                        target, GameObjects.EnemyHeroes.Select(e => e as Obj_AI_Base).ToList(), HitChance.High);
                    if (!best.Item2.Equals(Vector3.Zero) && best.Item1 >= 1)
                    {
                        Q.Cast(best.Item2);
                    }
                }
            }
        }

        /// <summary>
        /// Combo
        /// </summary>
        public void Combo()
        {
            try
            {
                var q = Menu.Item(Menu.Name + ".combo.q").GetValue<bool>();
                var w = Menu.Item(Menu.Name + ".combo.w").GetValue<bool>();

                if (w && W.IsReady())
                {
                    var target = TargetSelector.GetTarget(W.Range, W.DamageType, false);
                    if (target != null)
                    {
                        var best = GetBestCard(target, "combo");
                        if (best.Any())
                        {
                            Cards.Select(best);
                        }
                    }
                }
                if (q && Q.IsReady())
                {
                    var target = TargetSelector.GetTarget(Q.Range, Q.DamageType);
                    var goldCardTarget = _wTarget != null && _wTarget.IsValidTarget(Q.Range) && _wTargetEndTime > Game.Time;
                    if (goldCardTarget)
                    {
                        target = _wTarget;
                    }
                    if (target == null || target.Distance(Player) < Player.BoundingRadius && !IsImmobile(target))
                    {
                        return;
                    }
                    if (!goldCardTarget && (Cards.Has() || HasEBuff()) &&
                        GameObjects.EnemyHeroes.Any(e => Orbwalking.InAutoAttackRange(e) && e.IsValidTarget()) ||
                        Cards.Has(CardColor.Gold))
                    {
                        return;
                    }
                    if (goldCardTarget)
                    {
                        if (target.Distance(Player) > 250 && !IsImmobile(target))
                        {
                            return;
                        }
                        var best = BestQPosition(
                            target, GameObjects.EnemyHeroes.Select(e => e as Obj_AI_Base).ToList(), Q.GetHitChance("combo"));
                        if (!best.Item2.Equals(Vector3.Zero) && best.Item1 >= 1)
                        {
                            Q.Cast(best.Item2);
                            _wTarget = null;
                            _wTargetEndTime = 0;
                        }
                    }
                    else if (IsImmobile(target) || (W.Instance.CooldownExpires - Game.Time) >= 2 || W.Level == 0)
                    {
                        var best = BestQPosition(
                            target, GameObjects.EnemyHeroes.Select(e => e as Obj_AI_Base).ToList(), Q.GetHitChance("combo"));
                        if (!best.Item2.Equals(Vector3.Zero) && best.Item1 >= 1)
                        {
                            Q.Cast(best.Item2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Harass
        /// </summary>
        public void Harass()
        {
            try
            {
                var q = Menu.Item(Menu.Name + ".harass.q").GetValue<bool>();
                var w = Menu.Item(Menu.Name + ".harass.w").GetValue<bool>();

                if (w && W.IsReady())
                {
                    var target = TargetSelector.GetTarget(W.Range, W.DamageType, false);
                    if (target != null)
                    {
                        var best = GetBestCard(target, "harass");
                        if (best.Any())
                        {
                            Cards.Select(best);
                        }
                    }
                }
                if (ResourceManager.Check("harass") && q && Q.IsReady())
                {
                    var target = TargetSelector.GetTarget(Q.Range, Q.DamageType);
                    if (target != null)
                    {
                        {
                            var best = BestQPosition(
                                target, GameObjects.EnemyHeroes.Select(e => e as Obj_AI_Base).ToList(),
                                Q.GetHitChance("harass"));
                            if (!best.Item2.Equals(Vector3.Zero) && best.Item1 >= 1)
                            {
                                Q.Cast(best.Item2);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Lane Clear
        /// </summary>
        public void LaneClear()
        {
            try
            {
                var q = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady() && ResourceManager.Check("lane-clear");
                var qMin = Menu.Item(Menu.Name + ".lane-clear.q-min").GetValue<Slider>().Value;
                var w = Menu.Item(Menu.Name + ".lane-clear.w").GetValue<bool>() && W.IsReady();

                if (q)
                {
                    var minions = MinionManager.GetMinions(Q.Range * 1.2f);
                    var m = minions.OrderBy(x => x.Distance(Player)).FirstOrDefault();
                    if (m == null)
                    {
                        return;
                    }
                    var best = BestQPosition(null, minions, HitChance.High);
                    if (!best.Item2.Equals(Vector3.Zero) && best.Item1 >= qMin)
                    {
                        Q.Cast(best.Item2);
                    }
                }
                if (w)
                {
                    var minions = MinionManager.GetMinions(W.Range * 1.2f);
                    if (minions.Any())
                    {
                        Cards.Select(!ResourceManager.Check("lane-clear-blue") ? CardColor.Blue : CardColor.Red);
                    }
                    else if (GameObjects.EnemyTurrets.Any(t => t.IsValid && !t.IsDead && t.Health > 1 && t.Distance(Player) < W.Range))
                    {
                        Cards.Select(CardColor.Blue);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Jungle Clear
        /// </summary>
        public void JungleClear()
        {
            try
            {
                var q = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady() && (ResourceManager.Check("lane-clear") || ResourceManager.IgnoreJungle("lane-clear"));
                var w = Menu.Item(Menu.Name + ".lane-clear.w").GetValue<bool>() && W.IsReady();

                if (q)
                {
                    var minions = MinionManager.GetMinions(Q.Range * 1.2f, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                    var m = minions.OrderBy(x => x.Distance(Player)).FirstOrDefault();
                    if (m == null)
                    {
                        return;
                    }
                    var best = BestQPosition(null, minions, HitChance.High);
                    if (!best.Item2.Equals(Vector3.Zero) && best.Item1 >= 1)
                    {
                        Q.Cast(best.Item2);
                    }
                }
                if (w)
                {
                    var minions = MinionManager.GetMinions(W.Range * 1.2f, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                    if (minions.Any())
                    {
                        Cards.Select(ResourceManager.Check("lane-clear-blue") || ResourceManager.IgnoreJungle("lane-clear-blue")
                                ? CardColor.Red
                                : CardColor.Blue);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Flee
        /// </summary>
        public void Flee()
        {
            try
            {
                Orbwalker.SetAttack(false);

                Utility.DelayAction.Add(125, () => {
                    if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.CustomMode)
                    {
                        Orbwalker.SetAttack(true);
                    }
                });

                if (Menu.Item(Menu.Name + ".flee.w").GetValue<bool>())
                {
                    if (W.IsReady() || Cards.Status == SelectStatus.Ready)
                    {
                        var target = TargetSelector.GetTarget(W.Range, W.DamageType, false);
                        if (target != null)
                        {
                            var best = GetBestCard(target, "flee");
                            if (best.Any())
                            {
                                Cards.Select(best);
                                Orbwalker.ForceTarget(target);
                            }
                        }
                    }
                    if (Player.CanAttack && (Cards.Has(CardColor.Red) || Cards.Has(CardColor.Gold)))
                    {
                        var target =
                            GameObjects.EnemyHeroes.Where(e => Orbwalking.InAutoAttackRange(e) && e.IsValidTarget())
                                .OrderBy(e => e.Distance(Player))
                                .FirstOrDefault();
                        if (target != null)
                        {
                            Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Killsteal
        /// </summary>
        public void Killsteal()
        {
            try
            {

            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Last Hit
        /// </summary>
        public void LastHit()
        {
            try
            {

            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        /// <summary>
        /// Raises the <see cref="E:DrawingEndScene" /> event.
        /// </summary>
        /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
        public void OnDrawingEndScene(EventArgs args)
        {
            try
            {
                if ( R.Level > 0 && R.Instance.CooldownExpires - Game.Time < 3 && !Player.IsDead)
                {
                    // Draw minimap
                    Utility.DrawCircle(Player.Position, R.Range, System.Drawing.Color.White, 1, 30, true);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        #region Helpers/Cards etc.

        private Obj_AI_Base BestRedMinion()
        {
            var minions = MinionManager.GetMinions(float.MaxValue, MinionTypes.All, MinionTeam.NotAlly)
                    .Where(Orbwalking.InAutoAttackRange)
                    .ToList();
            var possibilities =
                ListExtensions.ProduceEnumeration(minions.Select(p => p.ServerPosition.To2D()).ToList())
                    .Where(p => p.Count > 0 && p.Count < 8)
                    .ToList();
            var hits = 0;
            var center = Vector2.Zero;
            var radius = float.MaxValue;
            foreach (var possibility in possibilities)
            {
                var mec = MEC.GetMec(possibility);
                if (mec.Radius < W.Width * 1.5f)
                {
                    if (possibility.Count > hits || possibility.Count == hits && mec.Radius < radius)
                    {
                        hits = possibility.Count;
                        radius = mec.Radius;
                        center = mec.Center;
                        if (hits == minions.Count)
                        {
                            break;
                        }
                    }
                }
            }

            if (hits > 0 && !center.Equals(Vector2.Zero))
            {
                return minions.OrderBy(m => m.Position.Distance(center.To3D())).FirstOrDefault();
            }

            return null;
        }

        private Tuple<int, Vector3> BestQPosition(Obj_AI_Base target, List<Obj_AI_Base> targets, HitChance hitChance)
        {
            var castPos = Vector3.Zero;
            var totalHits = 0;
            try
            {
                var enemies = targets.Where(e => e.IsValidTarget(Q.Range * 1.5f)).ToList();
                var enemyPositions = new List<Tuple<Obj_AI_Base, Vector3>>();
                var circle = new Geometry.Polygon.Circle(Player.Position, Player.BoundingRadius, 30).Points;

                foreach (var h in enemies)
                {
                    var ePred = Q.GetPrediction(h);
                    if (ePred.Hitchance >= hitChance)
                    {
                        circle.Add(Player.Position.Extend(ePred.UnitPosition, Player.BoundingRadius).To2D());
                        enemyPositions.Add(new Tuple<Obj_AI_Base, Vector3>(h, ePred.UnitPosition));
                    }
                }
                var targetPos = target?.Position ?? Vector3.Zero;
                if (target == null)
                {
                    var possibilities = ListExtensions.ProduceEnumeration(enemyPositions).Where(p => p.Count > 0).ToList();
                    var count = 0;
                    foreach (var possibility in possibilities)
                    {
                        var mec = MEC.GetMec(possibility.Select(p => p.Item2.To2D()).ToList());
                        if (mec.Radius < Q.Width && possibility.Count > count)
                        {
                            count = possibility.Count;
                            targetPos = mec.Center.To3D();
                        }
                    }
                }
                if (targetPos.Equals(Vector3.Zero))
                {
                    return new Tuple<int, Vector3>(totalHits, castPos);
                }
                circle = circle.OrderBy(c => c.Distance(targetPos)).ToList();
                if (!enemyPositions.Any())
                {
                    return new Tuple<int, Vector3>(totalHits, castPos);
                }

                foreach (var point in circle)
                {
                    var hits = 0;
                    var containsTarget = false;
                    var direction = Q.Range * (point.To3D() - Player.Position).Normalized().To2D();
                    var rect1 = new Geometry.Polygon.Rectangle(
                        Player.Position, Player.Position.Extend(Player.Position + direction.To3D(), Q.Range), Q.Width);
                    var rect2 = new Geometry.Polygon.Rectangle(
                        Player.Position,
                        Player.Position.Extend(Player.Position + direction.Rotated(QAngle).To3D(), Q.Range), Q.Width);
                    var rect3 = new Geometry.Polygon.Rectangle(
                        Player.Position,
                        Player.Position.Extend(Player.Position + direction.Rotated(-QAngle).To3D(), Q.Range), Q.Width);
                    foreach (var enemy in enemyPositions)
                    {
                        var bounding = new Geometry.Polygon.Circle(enemy.Item2, enemy.Item1.BoundingRadius * 0.85f);
                        if (bounding.Points.Any(p => rect1.IsInside(p) || rect2.IsInside(p) || rect3.IsInside(p)))
                        {
                            hits++;
                            if (target != null && enemy.Item1.NetworkId.Equals(target.NetworkId))
                            {
                                containsTarget = true;
                            }
                        }
                    }
                    if ((containsTarget || target == null) && hits > totalHits)
                    {
                        totalHits = hits;
                        castPos = Player.Position.Extend(point.To3D(), Q.Range);
                        if (totalHits >= enemies.Count)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return new Tuple<int, Vector3>(totalHits, castPos);
        }

        private bool IsWKillable(Obj_AI_Base target, int stage = 0)
        {
            return target != null && W.GetDamage(target, stage) - 5 > target.Health + target.HPRegenRate;
        }

        private bool HasEBuff()
        {
            return Player.HasBuff("cardmasterstackparticle");
        }

        private int GetEStacks()
        {
            return HasEBuff() ? 3 : Player.GetBuffCount("cardmasterstackholder");
        }

        private CardColor GetSelectedCardColor(int index)
        {
            switch (index)
            {
                case 0:
                    return CardColor.None;
                case 1:
                    return CardColor.Gold;
                case 2:
                    return CardColor.Red;
                case 3:
                    return CardColor.Blue;
            }
            return CardColor.None;
        }

        private static bool IsImmobile(Obj_AI_Base t)
        {
            return t.HasBuffOfType(BuffType.Stun) || t.HasBuffOfType(BuffType.Charm) || t.HasBuffOfType(BuffType.Snare) ||
                   t.HasBuffOfType(BuffType.Knockup) || t.HasBuffOfType(BuffType.Polymorph) ||
                   t.HasBuffOfType(BuffType.Fear) || t.HasBuffOfType(BuffType.Taunt) || t.IsStunned;
        }

        private static float GetImmobileTime(Obj_AI_Base target)
        {
            var buffs =
                target.Buffs.Where(
                    t =>
                        t.Type == BuffType.Charm || t.Type == BuffType.Snare || t.Type == BuffType.Knockback ||
                        t.Type == BuffType.Polymorph || t.Type == BuffType.Fear || t.Type == BuffType.Taunt ||
                        t.Type == BuffType.Stun).ToList();
            if (buffs.Any())
            {
                return buffs.Max(t => t.EndTime) - Game.Time;
            }
            return 0f;
        }

        private int GetWHits(Obj_AI_Base target, List<Obj_AI_Base> targets = null, CardColor color = CardColor.Gold)
        {
            try
            {
                if (targets != null && color == CardColor.Red)
                {
                    targets = targets.Where(t => t.IsValidTarget((W.Range + W.Width) * 1.5f)).ToList();
                    var pred = W.GetPrediction(target);
                    if (pred.Hitchance >= HitChance.Medium)
                    {
                        var circle = new Geometry.Polygon.Circle(pred.UnitPosition, target.BoundingRadius + WRedRadius);
                        return 1 + (from t in targets.Where(x => x.NetworkId != target.NetworkId)
                                    let pred2 = W.GetPrediction(t)
                                    where pred2.Hitchance >= HitChance.Medium
                                    select new Geometry.Polygon.Circle(pred2.UnitPosition, t.BoundingRadius * 0.9f)).Count(
                                circle2 => circle2.Points.Any(p => circle.IsInside(p)));
                    }
                }
                if (W.IsInRange(target))
                {
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0;
        }

        private List<CardColor> GetBestCard(Obj_AI_Hero target, string mode)
        {
            var cards = new List<CardColor>();
            if (target == null || !target.IsValid || target.IsDead)
            {
                return cards;
            }
            try
            {
                if (IsWKillable(target, 2))
                {
                    cards.Add(CardColor.Gold);
                }
                if (IsWKillable(target))
                {
                    cards.Add(CardColor.Blue);
                }
                if (IsWKillable(target, 1))
                {
                    cards.Add(CardColor.Red);
                }
                if (cards.Any())
                {
                    return cards;
                }
                var selectedCard =
                    GetSelectedCardColor(Menu.Item(Menu.Name + ".harass.w-card").GetValue<StringList>().SelectedIndex);
                var burst = Menu.Item(Menu.Name + ".miscellaneous.mode").GetValue<StringList>().SelectedIndex == 0;
                var red = 0;
                var blue = 0;
                var gold = 0;

                var shouldBlue = Player.Mana < W.ManaCost + Q.ManaCost &&
                                 Player.Mana + (25 + 25 * W.Level) > Q.ManaCost + W.ManaCost;

                if (!burst && (mode == "combo" || mode == "harass" && selectedCard == CardColor.None))
                {
                    if (Q.Level == 0)
                    {
                        return new List<CardColor> { CardColor.Blue };
                    }
                    gold++;
                    if (target.Distance(Player) > W.Range * 0.8f)
                    {
                        gold++;
                    }
                    if (mode == "combo" && (Player.ManaPercent < 10 || shouldBlue) ||
                        mode == "harass" && !ResourceManager.Check("harass-blue"))
                    {
                        return new List<CardColor> { CardColor.Blue };
                    }
                    var minRed = Menu.Item(Menu.Name + ".combo.red-min").GetValue<Slider>().Value;
                    var redHits = GetWHits(target, GameObjects.EnemyHeroes.Cast<Obj_AI_Base>().ToList(), CardColor.Red);
                    red += redHits;
                    if (red > blue && red > gold && redHits >= minRed)
                    {
                        cards.Add(CardColor.Red);
                        if (red == blue)
                        {
                            cards.Add(CardColor.Blue);
                        }
                        if (red == gold)
                        {
                            cards.Add(CardColor.Gold);
                        }
                    }
                    else if (gold > blue && gold > red)
                    {
                        cards.Add(CardColor.Gold);
                        if (gold == blue)
                        {
                            cards.Add(CardColor.Blue);
                        }
                        if (gold == red && redHits >= minRed)
                        {
                            cards.Add(CardColor.Red);
                        }
                    }
                    else if (blue > red && blue > gold)
                    {
                        cards.Add(CardColor.Blue);
                        if (blue == red && redHits >= minRed)
                        {
                            cards.Add(CardColor.Red);
                        }
                        if (blue == gold)
                        {
                            cards.Add(CardColor.Gold);
                        }
                    }
                }
                if (mode == "combo" && !cards.Any())
                {
                    if (Q.Level == 0)
                    {
                        return new List<CardColor> { CardColor.Blue };
                    }
                    var distance = target.Distance(Player);
                    var damage = Player.GetAutoAttackDamage(target, true) - target.HPRegenRate * 2f - 10;
                    if (HasEBuff())
                    {
                        damage += E.GetDamage(target);
                    }
                    if (Q.IsReady() && (GetImmobileTime(target) > 0.5f || distance < Q.Range / 4f))
                    {
                        damage += Q.GetDamage(target);
                    }
                    if (W.GetDamage(target, 2) + damage > target.Health)
                    {
                        cards.Add(CardColor.Gold);
                    }
                    if (distance < Orbwalking.GetRealAutoAttackRange(target) * 0.85f)
                    {
                        if (W.GetDamage(target) + damage > target.Health)
                        {
                            cards.Add(CardColor.Blue);
                        }
                        if (W.GetDamage(target, 1) + damage > target.Health)
                        {
                            cards.Add(CardColor.Red);
                        }
                    }

                    if (!cards.Any())
                    {
                        if (ObjectManager.Player.HealthPercent <=
                            Menu.Item(Menu.Name + ".combo.gold-percent").GetValue<Slider>().Value)
                        {
                            cards.Add(CardColor.Gold);
                        }
                        else if (Player.ManaPercent < 10 || shouldBlue)
                        {
                            cards.Add(CardColor.Blue);
                        }
                        else
                        {
                            var redHits = GetWHits(
                                target, GameObjects.EnemyHeroes.Cast<Obj_AI_Base>().ToList(), CardColor.Red);
                            if (redHits >= Menu.Item(Menu.Name + ".combo.red-min").GetValue<Slider>().Value)
                            {
                                cards.Add(CardColor.Red);
                            }
                        }
                    }
                    if (!cards.Any())
                    {
                        cards.Add(CardColor.Gold);
                    }
                }
                else if (mode == "harass" && !cards.Any())
                {
                    if (selectedCard == CardColor.None && burst)
                    {
                        cards.Add(target.Distance(Player) > W.Range * 0.8f ? CardColor.Gold : CardColor.Blue);
                    }
                    else
                    {
                        var card = !ResourceManager.Check("harass-blue") ? CardColor.Blue : selectedCard;
                        if (card != CardColor.None)
                        {
                            cards.Add(card);
                        }
                    }
                }
                else if (mode == "flee")
                {
                    cards.Add(
                        GetWHits(target, GameObjects.EnemyHeroes.Cast<Obj_AI_Base>().ToList(), CardColor.Red) >= 2
                            ? CardColor.Red
                            : CardColor.Gold);
                }
                if (!cards.Any())
                {
                    cards.Add(CardColor.Gold);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return cards;
        }

        internal enum CardColor
        {
            Red,

            Gold,

            Blue,

            None
        }

        internal enum SelectStatus
        {
            Selecting,

            Selected,

            Ready,

            Cooldown,

            None
        }

        public static class Cards
        {
            public static List<CardColor> ShouldSelect;
            public static CardColor LastCard;
            private static int _lastWSent;

            static Cards()
            {
                LastCard = CardColor.None;
                ShouldSelect = new List<CardColor>();
                Obj_AI_Base.OnProcessSpellCast += OnObjAiBaseProcessSpellCast;
                Game.OnUpdate += OnGameUpdate;
            }

            public static SelectStatus Status { get; set; }

            public static bool ShouldWait => Utils.TickCount - _lastWSent <= 200;

            public static int Delay { get; set; }

            public static bool Has(CardColor color)
            {
                return color == CardColor.Gold && ObjectManager.Player.HasBuff("goldcardpreattack") ||
                       color == CardColor.Red && ObjectManager.Player.HasBuff("redcardpreattack") ||
                       color == CardColor.Blue && ObjectManager.Player.HasBuff("bluecardpreattack");
            }

            public static bool Has()
            {
                return ObjectManager.Player.HasBuff("goldcardpreattack") ||
                       ObjectManager.Player.HasBuff("redcardpreattack") ||
                       ObjectManager.Player.HasBuff("bluecardpreattack");
            }

            public static void Select(CardColor card)
            {
                try
                {
                    Select(new List<CardColor> { card });
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }

            public static void Select(List<CardColor> cards)
            {
                try
                {
                    if (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).Name == "PickACard" &&
                        Status == SelectStatus.Ready)
                    {
                        ShouldSelect = cards;
                        if (ShouldSelect.Any())
                        {
                            if (!ShouldWait)
                            {
                                if (ObjectManager.Player.Spellbook.CastSpell(SpellSlot.W, ObjectManager.Player))
                                {
                                    _lastWSent = Utils.TickCount;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }

            private static void OnGameUpdate(EventArgs args)
            {
                try
                {
                    var spell = ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W);
                    var wState = ObjectManager.Player.Spellbook.CanUseSpell(SpellSlot.W);

                    if ((wState == SpellState.Ready && spell.Name == "PickACard" &&
                         (Status != SelectStatus.Selecting || !ShouldWait)) || ObjectManager.Player.IsDead)
                    {
                        Status = SelectStatus.Ready;
                    }
                    else if (wState == SpellState.Cooldown && spell.Name == "PickACard")
                    {
                        ShouldSelect.Clear();
                        Status = SelectStatus.Cooldown;
                    }
                    else if (wState == SpellState.Surpressed && !ObjectManager.Player.IsDead)
                    {
                        Status = SelectStatus.Selected;
                    }
                    if (
                        ShouldSelect.Any(
                            s =>
                                s == CardColor.Blue && spell.Name == "bluecardlock" ||
                                s == CardColor.Gold && spell.Name == "goldcardlock" ||
                                s == CardColor.Red && spell.Name == "redcardlock"))
                    {
                        Utility.DelayAction.Add(
                            (int)
                                ((Delay - Game.Ping / 2) *
                                 (Utils.TickCount - _lastWSent <= 200 ? 0.5f : 1f)),
                            delegate { ObjectManager.Player.Spellbook.CastSpell(SpellSlot.W, false); });
                    }
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }

            private static void OnObjAiBaseProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
            {
                try
                {
                    if (!sender.IsMe)
                    {
                        return;
                    }

                    if (args.SData.Name == "PickACard")
                    {
                        Status = SelectStatus.Selecting;
                    }
                    if (args.SData.Name == "goldcardlock")
                    {
                        LastCard = CardColor.Gold;
                        Status = SelectStatus.Selected;
                    }
                    else if (args.SData.Name == "bluecardlock")
                    {
                        LastCard = CardColor.Blue;
                        Status = SelectStatus.Selected;
                    }
                    else if (args.SData.Name == "redcardlock")
                    {
                        LastCard = CardColor.Red;
                        Status = SelectStatus.Selected;
                    }
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }
        }

        #endregion
    }
}
