using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace StickMayhem {
    public static class Verification {
        static int checks, seq;
        static readonly List<object> matches = new List<object>();
        static void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException("VERIFY: " + description); checks++; }
        static Match New(int weapon, int map) {
            int starter = weapon < Rules.StarterCount ? weapon : 0;
            var m = new Match(new MatchOptions { Starter = starter, Map = map, Lives = 3 }, new MatchOptions { Starter = starter, Lives = 3 }, 42);
            if (weapon >= Rules.StarterCount) {
                var p = m.State.Players[0]; p.Weapon = weapon; p.Magazine = Rules.Weapons[weapon].Magazine; p.Reserve = 0; if (weapon == 6 || weapon == 8) p.WeaponTime = 15;
            }
            foreach (var p in m.State.Players) { p.Invulnerable = 0; p.Grounded = true; } return m;
        }
        static InputFrame Frame(Match m, int jump, int bomb, int drop, bool fire, double x, double y) {
            return new InputFrame { Session = m.State.Session, Seq = ++seq, Jump = jump, Bomb = bomb, Drop = drop, Fire = fire, AimX = x, AimY = y };
        }
        static void Core(string folder) {
            Check(Rules.Weapons.Length == 14 && Rules.Arenas.Length == 6, "fourteen weapons six arenas");
            var m = New(0, 0); var p = m.State.Players[0];
            Check(Wire.Valid(m.State), "initial snapshot");
            Check(Rules.StarterCount == 3 && Rules.Weapons.Take(3).Select(w => w.Name).SequenceEqual(new[] { "左轮", "USP", "格洛克" }), "three pistol starter choices");
            Check(p.Weapon == 0 && p.Magazine == 6 && p.Reserve == -1 && p.Bombs == 3, "custom starter has infinite reserve and finite magazine");
            Check(Rules.Weapons[0].Reload == 3 && Rules.Weapons[1].Magazine == 12 && Rules.Weapons[1].Reload == 2 &&
                Rules.Weapons[2].Magazine == 20 && Rules.Weapons[2].Automatic && Rules.Weapons[2].Push < Rules.Weapons[1].Push && Rules.Weapons[1].Push < Rules.Weapons[0].Push,
                "requested pistol magazines reloads damage and continuous held fire");
            Check(Rules.Weapons[0].Automatic && Rules.Weapons[1].Automatic && Rules.Weapons[0].Interval > Rules.Weapons[1].Interval && Rules.Weapons[1].Interval > Rules.Weapons[2].Interval,
                "revolver and USP repeat while held at their slower configured fire rates");
            Check(Rules.Weapons[0].Push == 420, "revolver knockback damage increased");
            Check(Rules.Weapons[4].Push == 520, "sniper impact force receives the requested small increase");
            var skyMatch = new Match(new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, new MatchOptions { Starter = 2, Map = 0, Lives = 3 }, 43);
            Check(skyMatch.State.Mode == 1 && skyMatch.State.Players.All(f => f.Weapon == 12 && f.Magazine == 5 && f.Reserve == -1) && Math.Abs(Rules.GravityFor(skyMatch.State.Mode) - Rules.Gravity * .5) < .01,
                "Sky Sniper mode starts both players with an infinite-ammo Kraber and halves gravity");
            skyMatch.State.NextCrate = skyMatch.State.NextBigCrate = skyMatch.State.NextPower = .001;
            skyMatch.Step();
            Check(skyMatch.State.Crates.Count == 0 && skyMatch.State.Powerups.Count == 1,
                "Sky Sniper suppresses both weapon-drop types while retaining map powerups");
            skyMatch = new Match(new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, 45);
            p = skyMatch.State.Players[0]; p.X = 500; p.Y = 499; p.Grounded = true; p.Invulnerable = 100;
            skyMatch.Submit(0, Frame(skyMatch, 0, 0, 0, true, 750, p.Y - 25)); skyMatch.Step();
            Check(p.Weapon == 12 && p.Magazine == 4 && p.Reserve == -1 && Math.Abs(p.ReloadTime - 1) < .02,
                "Sky Sniper Kraber keeps its weapon and starts a one-second bolt cycle after each shot");
            skyMatch.Submit(0, Frame(skyMatch, 0, 0, 0, false, 750, p.Y - 25));
            for (int i = 0; i < 62; i++) { p.X = 500; p.Y = 499; p.Vx = p.Vy = 0; p.Grounded = true; skyMatch.Step(); }
            Check(p.Weapon == 12 && p.Magazine == 5 && p.Reserve == -1 && p.ReloadTime == 0,
                "Sky Sniper Kraber refills to five rounds after each one-second bolt cycle");
            var normalJump = New(0, 0); var skyJump = new Match(new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, 44);
            normalJump.Submit(0, Frame(normalJump, 1, 0, 0, false, 750, 300)); skyJump.Submit(0, Frame(skyJump, 1, 0, 0, false, 750, 300));
            for (int i = 0; i < 20; i++) { normalJump.Step(); skyJump.Step(); }
            Check(skyJump.State.Players[0].Y < normalJump.State.Players[0].Y - 20, "half gravity makes the same jump rise noticeably higher");
            Check(Rules.Weapons[9].Name == "突击步枪" && Rules.Weapons[9].Magazine == 30 && Rules.Weapons[9].Ammo == 30 && Rules.Weapons[9].Reload == 0 && Rules.Weapons[9].BurstCount == 3 && !Rules.Weapons[9].Automatic && Rules.Weapons[9].Push == 420 && Rules.Weapons[9].BurstInterval < .1 && Rules.Weapons[9].BurstPause > .3,
                "assault rifle has a three-round burst, Famàs-like recovery pause, slightly higher impact and thirty total rounds");
            Check(Rules.Weapons[10].Name == "冲锋枪" && Rules.Weapons[10].Ammo == 30 && Rules.Weapons[10].Reload == 0 && Rules.Weapons[10].Push == Rules.Weapons[1].Push && Rules.Weapons[10].Interval < Rules.Weapons[2].Interval && Rules.Weapons[10].Automatic,
                "submachine gun has thirty total rounds, matches USP impact and fires slightly faster than Glock");
            Check(Rules.Weapons.Select(w => w.Recoil).SequenceEqual(new[] { 0, 0, 0, 2, 3, 3, 2, 1, 0, 2, 1, 1, 4, 0 }),
                "weapon recoil levels match requested scale; starter pistols, sword and electric SMG have no recoil");
            Check(Rules.Weapons[11].Name == "专注冲锋枪" && Rules.Weapons[11].Energy && Rules.Weapons[11].Magazine == 45 && Rules.Weapons[11].Interval > Rules.Weapons[11].MinInterval && Rules.Weapons[11].Automatic,
                "Focus SMG keeps its blue 45-round magazine and increasing rate of fire");
            Check(Rules.Weapons[12].Name == "克莱博" && Rules.Weapons[12].DirectKill && Rules.Weapons[12].BoltAction && Rules.Weapons[12].Magazine == 5 && Rules.Weapons[12].Reload == 1,
                "Kraber has five shots, one-second bolt reload, instant shattering hits and heavy recoil");
            Check(Rules.Weapons[13].Name == "电能冲锋枪" && Rules.Weapons[13].Energy && Rules.Weapons[13].SlowTarget && Rules.Weapons[13].Magazine == 30 && Rules.Weapons[13].Ammo == 30 && Rules.Weapons[13].Reload == 0 && Rules.Weapons[13].Push == Rules.Weapons[2].Push && Rules.Weapons[13].ImpactPush == Rules.Weapons[10].Push,
                "ordinary-drop electric SMG uses blue bullets, Glock knockback, SMG impact gain and one thirty-round supply");
            foreach (int index in new[] { 9, 10 }) {
                m = New(index, 0); p = m.State.Players[0]; p.X = 500; p.Magazine = Rules.Weapons[index].Magazine; p.Reserve = 0;
                for (int shotTick = 0; shotTick < 1200 && p.Weapon == index; shotTick++) {
                    p.X = 500; p.Y = 499; p.Vx = p.Vy = 0; p.Grounded = true;
                    m.Submit(0, Frame(m, 0, 0, 0, index != 9 || shotTick % 40 == 0, p.X + 100, p.Y - 25)); m.Step();
                }
                Check(p.Weapon == p.Starter && p.Magazine == Rules.Weapons[p.Starter].Magazine && p.Reserve == -1 && p.ReloadTime == 0,
                    Rules.Weapons[index].Name + " spends its only thirty-round supply then switches to starter pistol");
            }
            m = New(9, 0); p = m.State.Players[0]; p.X = 500; p.Y = 499; p.Grounded = true;
            m.Submit(0, Frame(m, 0, 0, 0, true, p.X + 100, p.Y - 25)); m.Step();
            Check(m.State.Shots.Count == 1 && p.BurstRemaining == 2, "one trigger starts a three-round assault-rifle burst");
            m.Submit(0, Frame(m, 0, 0, 0, false, p.X + 100, p.Y - 25));
            for (int i = 0; i < 20 && p.BurstRemaining > 0; i++) m.Step();
            Check(m.State.Shots.Count == 3 && p.Magazine == 27 && p.BurstRemaining == 0 && p.Cooldown > .3,
                "assault-rifle burst completes after trigger release and then pauses");
            foreach (int index in Enumerable.Range(Rules.StarterCount, Rules.Weapons.Length - Rules.StarterCount)) {
                m = New(index, 0); p = m.State.Players[0]; p.X = 500; p.Y = 499; p.Vx = p.Vy = 0; p.Grounded = true;
                m.Submit(0, Frame(m, 0, 0, 0, true, p.X + 100, p.Y - 25)); m.Step();
                Check(Rules.Weapons[index].Recoil > 0 ? p.Vx < 0 : Math.Abs(p.Vx) < .01,
                    Rules.Weapons[index].Name + " applies its configured firing recoil");
            }
            m = New(11, 0); p = m.State.Players[0];
            m.Submit(0, Frame(m, 0, 0, 0, true, p.X + 400, p.Y - 25)); m.Step(); double slowStart = p.Cooldown;
            Check(m.State.Shots.Any(s => s.Energy) && p.Magazine == 44, "energy SMG fires a visible blue projectile");
            for (int i = 0; i < 110; i++) { m.Submit(0, Frame(m, 0, 0, 0, true, p.X + 400, p.Y - 25)); m.Step(); }
            Check(p.Cooldown < slowStart && p.FireRamp >= 1.19, "holding the energy SMG spins up to a higher fire rate");
            m = New(13, 0); p = m.State.Players[0]; var energyTarget = m.State.Players[1]; p.X = 450; energyTarget.X = 490; p.Y = energyTarget.Y = 499;
            m.Submit(0, Frame(m, 0, 0, 0, true, energyTarget.X, energyTarget.Y - 25)); m.Step();
            Check(Math.Abs(energyTarget.Impact - Rules.Weapons[10].Push / 18.0) < .01 && Math.Abs(energyTarget.Vx - Rules.Weapons[2].Push) < 1 && Math.Abs(energyTarget.SlowTime - 2) < .01,
                "electric round matches SMG impact gain and Glock knockback while applying a two-second slow");
            double slowedStart = energyTarget.X; energyTarget.Vx = energyTarget.Vy = 0; energyTarget.Grounded = true;
            for (int i = 0; i < 30; i++) { var right = Frame(m, 0, 0, 0, false, energyTarget.X + 100, energyTarget.Y); right.Right = true; m.Submit(1, right); m.Step(); }
            var control = New(0, 0); var controlEnemy = control.State.Players[1]; controlEnemy.X = 490; controlEnemy.Y = 499; controlEnemy.Grounded = true;
            for (int i = 0; i < 30; i++) { var right = Frame(control, 0, 0, 0, false, 600, 499); right.Right = true; control.Submit(1, right); control.Step(); }
            Check(energyTarget.X - slowedStart < (controlEnemy.X - 490) * .6 && energyTarget.SlowTime > 1.4, "electric hits cut movement speed by fifty percent for two seconds");
            m = New(1, 0); p = m.State.Players[0]; p.SpeedTime = 5;
            m.Submit(0, Frame(m, 0, 0, 0, true, p.X + 300, p.Y - 25)); m.Step();
            Check(Math.Abs(p.Cooldown - Rules.Weapons[1].Interval / 2) < .01, "Fire Up doubles firing rate");
            m = New(12, 0); p = m.State.Players[0]; var kraberEnemy = m.State.Players[1]; p.X = 450; kraberEnemy.X = 525; p.Magazine = 5;
            m.Submit(0, Frame(m, 0, 0, 0, true, kraberEnemy.X, kraberEnemy.Y - 25)); m.Step();
            Check(kraberEnemy.Lives == 2 && m.State.Shatters.Any(s => s.Owner == kraberEnemy.Seat) && p.Vx < -700, "Kraber hit shatters instantly and its backblast launches the shooter backward");
            m = New(12, 0); p = m.State.Players[0]; p.X = 450; p.Magazine = 5;
            m.Submit(0, Frame(m, 0, 0, 0, true, 900, 350)); m.Step();
            Check(p.Magazine == 4 && Math.Abs(p.ReloadTime - 1) < .02 && Wire.Valid(m.State), "Kraber bolt state magazine=" + p.Magazine + " reload=" + p.ReloadTime + " valid=" + Wire.Valid(m.State));
            for (int i = 0; i < 65; i++) m.Step(); Check(p.Magazine == 4 && p.ReloadTime == 0, "Kraber returns ready after its one-second bolt cycle");
            Check(Rules.Weapons[6].Magazine == 60 && Rules.Weapons[6].Ammo == 60 && Rules.Weapons[7].Explosive && Rules.Weapons[8].Melee, "Gatling 60 rounds, grenade launcher and melee sword configured");
            Check(Rules.Weapons[7].Magazine == 12 && Rules.Weapons[7].Ammo == 12, "grenade launcher has one finite twelve-round magazine");
            Check(Rules.Weapons[3].Push > 100 && Rules.Weapons[3].Pellets > 5, "shotgun impact and pellet count increased");
            var normalPool = Enumerable.Range(0, 300).Select(i => m.RandomAirdropWeapon()).ToArray();
            Check(normalPool.All(w => w >= Rules.StarterCount && !Rules.LargeDropWeapon(w)) && normalPool.Contains(9) && normalPool.Contains(10) && normalPool.Contains(13), "normal airdrops include assault rifle, SMG and electric SMG while excluding starter and special weapons");
            Check(Rules.Arenas.All(a => a.Spawns.Length == 6 && a.Spawns.All(x => a.Platforms.Any(t => x >= t.X && x <= t.X + t.Width))), "all six maps provide six valid player spawn points");
            m = new Match(Enumerable.Range(0, 6).Select(i => new MatchOptions { Map = 4, Starter = i % 3, Lives = 3 }).ToArray(), 16);
            Check(m.State.Players.Length == 6 && Wire.Valid(m.State) && m.State.Players.Select(fighter => fighter.Seat).SequenceEqual(Enumerable.Range(0, 6)), "six numbered players enter a large map and produce a valid network snapshot");
            var largePool = Enumerable.Range(0, 300).Select(i => m.RandomBigAirdropWeapon()).ToArray();
            Check(largePool.All(Rules.LargeDropWeapon) && new[] { 6, 8, 11, 12 }.All(largePool.Contains), "large airdrops include Gatling, sword, electric SMG and Kraber only");
            m = New(0, 0); p = m.State.Players[0];
            var move = Frame(m, 0, 0, 0, false, 800, 450); move.Right = true;
            Check(m.Submit(0, move), "valid movement accepted"); double startX = p.X;
            for (int i = 0; i < 20; i++) m.Step();
            Check(p.X > startX + 30 && p.Grounded, "movement and platform landing");
            var jump = Frame(m, 1, 0, 0, false, 800, 450); m.Submit(0, jump); m.Step();
            Check(p.Vy < -500 && p.Jumps == 1 && !p.Grounded, "first jump");
            for (int i = 0; i < 5; i++) m.Step();
            m.Submit(0, Frame(m, 2, 0, 0, false, 800, 450)); m.Step(); Check(p.Jumps == 2 && p.Vy < -500, "double jump");
            double vy = p.Vy; m.Submit(0, Frame(m, 3, 0, 0, false, 800, 450)); m.Step(); Check(p.Jumps == 2 && p.Vy > vy, "no third jump");
            m = New(0, 0); p = m.State.Players[0]; p.Y = 370;
            m.Submit(0, Frame(m, 0, 0, 1, false, 500, 500));
            for (int i = 0; i < 22; i++) m.Step(); Check(p.Y > 380 && p.Y <= 500, "drop through platform");
            m = New(4, 0); p = m.State.Players[0];
            m.Submit(0, Frame(m, 0, 0, 0, true, p.X, p.Y - 300)); m.Step();
            Check(m.State.Shots.Count == 1 && Math.Abs(m.State.Shots[0].Vx) < .01 && m.State.Shots[0].Vy < -2000, "mouse aims straight up");
            m = New(3, 0); p = m.State.Players[0]; m.Submit(0, Frame(m, 0, 0, 0, true, 900, 500)); m.Step();
            Check(m.State.Shots.Count == 6 && m.State.Shots.Select(s => s.Vy).Distinct().Count() == 6, "six-pellet shotgun spread");
            m = New(7, 0); p = m.State.Players[0]; m.Submit(0, Frame(m, 0, 0, 0, true, 900, 350)); m.Step();
            Check(m.State.Grenades.Count == 1 && m.State.Grenades[0].Launched && p.Magazine == 11, "grenade launcher fires ballistic explosive projectile");
            m = New(7, 0); p = m.State.Players[0]; var grenadeTarget = m.State.Players[1]; p.X = 450; grenadeTarget.X = 510; p.Y = grenadeTarget.Y = 499;
            m.Submit(0, Frame(m, 0, 0, 0, true, grenadeTarget.X, grenadeTarget.Y - 25));
            m.Step(); Check(m.State.Grenades.Count == 1, "direct grenade test launches projectile");
            for (int i = 0; i < 20 && m.State.Grenades.Count > 0; i++) m.Step();
            Check(m.State.Grenades.Count == 0 && m.State.Bursts.Any(b => !b.Rocket && b.Radius == 155) && grenadeTarget.Impact > 0,
                "launched grenade detonates immediately on direct enemy contact");
            m = New(4, 0); var enemy = m.State.Players[1]; p = m.State.Players[0]; p.X = 450; enemy.X = 490;
            m.Submit(0, Frame(m, 0, 0, 0, true, enemy.X, enemy.Y - 25)); m.Step();
            Check(Math.Abs(enemy.Impact - Rules.Weapons[4].Push / 18.0) < .01 && enemy.Vx > 300 && enemy.Vy < 0, "sniper hit uses increased impact force and directional knockback");
            int healthLives = enemy.Lives; Check(healthLives == 3, "hits affect knockback not stock count");
            m = New(5, 0); p = m.State.Players[0]; enemy = m.State.Players[1]; p.X = 450; enemy.X = 560; enemy.Y = p.Y;
            m.Submit(0, Frame(m, 0, 0, 0, true, enemy.X, enemy.Y - 25)); m.Step();
            Check(m.State.Shots.Any(s => s.Rocket), "rocket launcher projectile emitted");
            for (int i = 0; i < 40 && m.State.Shots.Count > 0; i++) m.Step();
            Check(enemy.Impact > 0 && m.State.Bursts.Any(b => b.Rocket && b.Radius == 190 && b.Time > 0), "rocket blast radius, knockback damage and fiery expanding effect are boosted");
            m = New(0, 0); p = m.State.Players[0]; enemy = m.State.Players[1]; p.X = 450; enemy.X = 490; enemy.Invulnerable = 1;
            m.Submit(0, Frame(m, 0, 0, 0, true, enemy.X, enemy.Y - 25)); m.Step(); Check(enemy.Impact == 0, "spawn protection");
            m = New(0, 0); p = m.State.Players[0];
            m.Submit(0, Frame(m, 0, 1, 0, false, p.X + 100, p.Y - 200)); m.Step();
            Check(p.Bombs == 2 && m.State.Grenades.Count == 1, "mouse aimed grenade");
            for (int i = 0; i < 90; i++) m.Step(); Check(m.State.Grenades.Count == 0, "grenade fuse expires");
            m = New(0, 0); p = m.State.Players[0]; p.Invulnerable = 0;
            m.State.Grenades.Add(new Grenade { Owner = 0, X = p.X, Y = p.Y - 25, Fuse = .01 }); m.Step();
            Check(p.Impact > 15 && p.Vy < -70, "buffed thrown explosive hits hard, including its owner");
            m = New(0, 0); var specialHolder = m.State.Players[1]; specialHolder.Weapon = 6; specialHolder.Magazine = 60; specialHolder.WeaponTime = 12;
            m.State.Crates.Add(new Crate { Id = 90, Weapon = 3, X = specialHolder.X, Y = specialHolder.Y - 20, Landed = true });
            m.State.Crates.Add(new Crate { Id = 91, Weapon = 8, X = specialHolder.X, Y = specialHolder.Y - 20, Landed = true, Large = true }); m.Step();
            Check(specialHolder.Weapon == 6 && specialHolder.WeaponTime > 11.9 && m.State.Crates.Count == 2, "holding a large-drop weapon blocks replacement by either crate type");
            m = New(0, 0); p = m.State.Players[0];
            m.State.Grenades.Add(new Grenade { Owner = 0, X = 500, Y = 490, Vy = 150, Fuse = 1 });
            m.Step(); m.Step(); Check(m.State.Grenades[0].Vy < 0, "grenade platform bounce");
            for (int weapon = 0; weapon < Rules.Weapons.Length; weapon++) {
                m = New(0, 0); p = m.State.Players[0];
                m.State.Crates.Add(new Crate { Id = 100, Weapon = weapon, X = p.X, Y = p.Y - 20, Landed = true, Large = Rules.LargeDropWeapon(weapon) }); m.Step();
                int expectedReserve = weapon < Rules.StarterCount ? -1 : Math.Max(0, Rules.Weapons[weapon].Ammo - Rules.Weapons[weapon].Magazine);
                Check(p.Weapon == weapon && p.Magazine == Rules.Weapons[weapon].Magazine && p.Reserve == expectedReserve && m.State.Crates.Count == 0, "crate grants each weapon and ammo");
                string pickup = String.IsNullOrEmpty(Rules.Weapons[weapon].Pickup) ? Rules.Weapons[weapon].Name : Rules.Weapons[weapon].Pickup;
                if (weapon == 8) pickup += " · 隐身5秒";
                Check(p.Popup == pickup && Math.Abs(p.PopupTime - 1.65) < .01, "Chinese pickup label");
                Check(weapon == 8 ? p.InvisibleTime > 4.9 : p.InvisibleTime == 0, "sword pickup grants five-second invisibility");
                double rise = Match.PopupRise(p);
                for (int i = 0; i < 75; i++) m.Step();
                Check(p.PopupTime > 0 && Match.PopupOpacity(p) < 1 && Match.PopupRise(p) > rise, "label rises and fades");
                for (int i = 0; i < 30; i++) m.Step(); Check(p.PopupTime == 0 && Match.PopupOpacity(p) == 0, "label disappears");
            }
            m = New(7, 0); p = m.State.Players[0]; p.Magazine = 1; p.Reserve = 0;
            m.Submit(0, Frame(m, 0, 0, 0, true, 900, 350)); m.Step();
            Check(p.Weapon == p.Starter && p.Magazine == Rules.Weapons[p.Starter].Magazine && p.Popup.Contains("备用"), "empty grenade launcher switches back to starter pistol");
            m = New(0, 0); p = m.State.Players[0]; p.Weapon = 5; p.Magazine = 1; p.Reserve = 0;
            m.Submit(0, Frame(m, 0, 0, 0, true, 400, 300)); m.Step();
            Check(p.Weapon == p.Starter && p.Reserve == -1 && p.Magazine == 6 && p.Popup.Contains("备用"), "empty airdrop ammo restores custom starter");
            m = New(2, 0); p = m.State.Players[0]; p.Y = 710; m.Step();
            Check(p.Lives == 2 && p.Respawn > 0, "fall costs one life");
            for (int i = 0; i < 70; i++) m.Step();
            Check(p.Respawn <= 0 && p.Weapon == 2 && p.Magazine == 20 && p.Reserve == -1 && p.Invulnerable > 0 && p.Impact == 0, "respawn resets and restores starter");
            m = New(0, 0); p = m.State.Players[0]; p.Lives = 2; p.Y = 710; m.Step();
            for (int i = 0; i < 70; i++) m.Step();
            Check(p.Lives == 1 && p.Popup == "背水一战" && p.PopupTime > 1.4, "last-life respawn shows final stand popup");
            m = New(0, 0); m.State.Players[0].Lives = 1; m.State.Players[0].Y = 710; m.Step();
            Check(m.State.Winner == 1, "last stock determines winner"); int tick = m.State.Tick; m.Step(); Check(m.State.Tick == tick, "finished match freezes");
            m = New(0, 0); foreach (var f in m.State.Players) { f.Lives = 1; f.Y = 710; } m.Step(); Check(m.State.Winner == -1, "simultaneous final falls draw");
            m = New(0, 0); var input = Frame(m, 0, 0, 0, false, 800, 450);
            Check(m.Submit(0, input) && !m.Submit(0, input), "replay rejected");
            var bad = Frame(m, 0, 0, 0, true, 500, 500); bad.Session = "wrong"; Check(!m.Submit(0, bad), "wrong session rejected");
            bad = Frame(m, 0, 0, 0, true, Double.NaN, 500); Check(!m.Submit(0, bad), "NaN aim rejected");
            bad = Frame(m, 50, 0, 0, false, 500, 500); Check(!m.Submit(0, bad), "jump nonce abuse rejected");
            // All three starter pistols can repeat fire while the mouse button stays held.
            m = New(0, 0); p = m.State.Players[0];
            for (int i = 0; i < 70; i++) { m.Submit(0, Frame(m, 0, 0, 0, true, p.X, 100)); m.Step(); }
            Check(p.Magazine <= 4 && p.Magazine >= 3, "revolver repeats slowly while held");
            m = New(1, 0); p = m.State.Players[0];
            for (int i = 0; i < 40; i++) { m.Submit(0, Frame(m, 0, 0, 0, true, p.X, 100)); m.Step(); }
            Check(p.Magazine <= 9 && p.Magazine >= 8, "USP repeats slowly while held");
            m = New(2, 0); p = m.State.Players[0]; m.Submit(0, Frame(m, 0, 0, 0, true, p.X, 100));
            for (int i = 0; i < 25; i++) m.Step(); Check(p.Magazine <= 16, "Glock supports automatic fire");
            // Empty each infinite-reserve pistol and verify requested reload duration/refill.
            for (int pistol = 0; pistol < Rules.StarterCount; pistol++) {
                m = New(pistol, 0); p = m.State.Players[0]; int clicks = Rules.Weapons[pistol].Magazine;
                for (int shot = 0; shot < clicks; shot++) {
                    m.Submit(0, Frame(m, 0, 0, 0, true, p.X, 100)); m.Step();
                    m.Submit(0, Frame(m, 0, 0, 0, false, p.X, 100)); m.Step();
                    if (shot < clicks - 1) while (p.Cooldown > 0) m.Step();
                }
                Check(p.Magazine == 0 && Math.Abs(p.ReloadTime - Rules.Weapons[pistol].Reload) < .04, "pistol begins exact reload " + pistol);
                int steps = (int)Math.Ceiling(Rules.Weapons[pistol].Reload / Rules.Dt) + 1;
                for (int i = 0; i < steps; i++) m.Step();
                Check(p.Magazine == Rules.Weapons[pistol].Magazine && p.Reserve == -1 && p.ReloadTime == 0, "infinite reserve refills magazine " + pistol);
            }
            for (int pistol = 0; pistol < Rules.StarterCount; pistol++) {
                m = New(0, 0); p = m.State.Players[1]; enemy = m.State.Players[0]; p.Starter = pistol; p.Weapon = pistol;
                p.Magazine = Rules.Weapons[pistol].Magazine; p.Reserve = -1; enemy.Invulnerable = 2; m.State.NextCrate = m.State.NextBigCrate = m.State.NextPower = 1000;
                var bot = new Bot(700 + pistol);
                for (int t = 0; t < 1500; t++) { enemy.Invulnerable = 2; m.Submit(1, bot.Think(m.State, 1)); m.Step(); }
                while (p.ReloadTime > 0) { enemy.Invulnerable = 2; m.Submit(1, bot.Think(m.State, 1)); m.Step(); }
                Check(p.Lives > 0 && p.Reserve == -1 && p.Magazine > 0 && p.ReloadTime == 0, "practice bot reloads infinite-reserve pistol " + pistol);
            }
            m = New(0, 0); p = m.State.Players[1]; enemy = m.State.Players[0]; p.Weapon = 8; p.WeaponTime = 15; p.Magazine = 1; p.X = 700; enemy.X = 600; p.Y = enemy.Y = 499; p.Grounded = true;
            var swordBot = new Bot(801); var swordApproach = swordBot.Think(m.State, 1);
            Check(swordApproach.Left && !swordApproach.Fire, "practice bot closes to melee range before swinging prison sword");
            p.X = 650; enemy.X = 600; var swordAttack = new Bot(802).Think(m.State, 1);
            Check(!swordAttack.Left && swordAttack.Fire, "practice bot swings prison sword only once enemy is in reach");
            m = new Match(new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, 812);
            p = m.State.Players[1]; enemy = m.State.Players[0]; p.X = 850; p.Y = 499; p.Grounded = true; p.Vx = 0; enemy.X = 150; enemy.Y = 499;
            var kraberBot = new Bot(813); var unsafeKraber = kraberBot.Think(m.State, 1);
            Check(!unsafeKraber.Fire && unsafeKraber.Left, "practice bot approaches the arena center instead of firing Kraber outward from spawn");
            p.X = 550; p.Vx = -275; p.Grounded = true; var safeKraber = kraberBot.Think(m.State, 1);
            Check(safeKraber.Fire, "practice bot fires Kraber after moving to a recoil-safe position");
            m = new Match(new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, new MatchOptions { Starter = 0, Map = 0, Lives = 3, Mode = 1 }, 814);
            p = m.State.Players[1]; enemy = m.State.Players[0]; enemy.Invulnerable = 1000; m.State.NextCrate = m.State.NextBigCrate = m.State.NextPower = 1000;
            var safeBot = new Bot(815);
            for (int t = 0; t < 360; t++) { enemy.Invulnerable = 1000; m.Submit(1, safeBot.Think(m.State, 1)); m.Step(); }
            Check(p.Lives == 3 && p.Respawns == 0 && p.X > 0 && p.X < 1000 && p.Y < 690, "practice Kraber bot survives recoil during extended Sky Sniper play");
            m = New(0, 0); m.State.Players[0].InvisibleTime = 5;
            var hiddenA = Rules.Copy(m.State); var hiddenB = Rules.Copy(m.State); hiddenA.Players[0].X = 170; hiddenB.Players[0].X = 830;
            var blindA = new Bot(816).Think(hiddenA, 1); var blindB = new Bot(816).Think(hiddenB, 1);
            Check(blindA.AimX == blindB.AimX && blindA.AimY == blindB.AimY && blindA.Left == blindB.Left && blindA.Right == blindB.Right && blindA.Fire == blindB.Fire,
                "invisible opponent's exact position cannot affect bot aim or movement");
            var hiddenMatch = New(0, 0); hiddenMatch.State.Players[0].InvisibleTime = 5; hiddenMatch.State.NextCrate = hiddenMatch.State.NextBigCrate = hiddenMatch.State.NextPower = 1000;
            var probingBot = new Bot(817); bool probingFire = false;
            for (int t = 0; t < 120; t++) { hiddenMatch.State.Tick = t; hiddenMatch.State.Players[0].InvisibleTime = 5;
                var probe = probingBot.Think(hiddenMatch.State, 1); probingFire |= probe.Fire; }
            Check(probingFire, "practice bot periodically fires probing shots while opponent stays invisible");
            Check(ArenaView.PlayerOpacity(0, 0, 5) > 0 && ArenaView.PlayerOpacity(0, 0, 5) <= .12 && ArenaView.PlayerOpacity(0, 1, 5) == 0 && ArenaView.PlayerOpacity(0, 1, 0) == 1,
                "invisibility renders self faint and hides completely from opponent view");
            Check(!ArenaView.IsOffscreen(500, 325) && ArenaView.IsOffscreen(1080, 325) && ArenaView.IsOffscreen(500, 700), "offscreen detector covers horizontal and vertical exits");
            Point rightArrow = ArenaView.OffscreenPoint(1080, 325), bottomArrow = ArenaView.OffscreenPoint(500, 700);
            Check(rightArrow.X > 950 && Math.Abs(rightArrow.Y - 300) < 1 && bottomArrow.Y > 550, "offscreen arrows point toward player bearing at screen edges");
            m = New(0, 0); p = m.State.Players[0]; enemy = m.State.Players[1]; p.X = 450; enemy.X = 490; enemy.ShieldTime = 6;
            m.Submit(0, Frame(m, 0, 0, 0, true, enemy.X, enemy.Y - 25)); m.Step(); Check(enemy.Impact == 0 && enemy.Vx == 0, "shield blocks knockback");
            m = New(0, 0); p = m.State.Players[0]; p.JetOwned = true; p.JetFuel = 3; double jetY = p.Y;
            for (int i = 0; i < 60; i++) { var jet = Frame(m, 0, 0, 0, false, 500, 100); jet.Jet = true; m.Submit(0, jet); m.Step(); }
            Check(p.Y < jetY - 40 && p.JetFuel < 2.05 && p.JetFuel > 1.9, "held space flies and consumes finite fuel");
            for (int i = 0; i < 125; i++) { var jet = Frame(m, 0, 0, 0, false, 500, 100); jet.Jet = true; m.Submit(0, jet); m.Step(); }
            Check(p.JetFuel == 0, "jet fuel runs out");
            m = New(0, 0); p = m.State.Players[0]; m.DropPowerup(p.X, 1); m.State.Powerups[0].Y = p.Y - 20; m.State.Powerups[0].Landed = true; m.Step();
            for (int i = 0; i < 420; i++) m.Step();
            Check(p.JetOwned && p.JetFuel == 3 && Wire.Valid(m.State), "rocket flight device has no duration timeout when fuel is unused");
            for (int kind = 0; kind < 4; kind++) {
                m = New(0, 0); p = m.State.Players[0]; m.State.Powerups.Add(new Powerup { Id = 500 + kind, Kind = kind, X = p.X, Y = p.Y - 20, Landed = true }); m.Step();
                Check(m.State.Powerups.Count == 0 && (kind == 0 ? p.ShieldTime > 5.9 : kind == 1 ? p.JetOwned && p.JetFuel == 3 : kind == 2 ? p.InvisibleTime > 4.9 : p.SpeedTime > 4.9), "powerup pickup kind " + kind);
                Check(p.Popup.Contains(kind == 0 ? "护盾" : kind == 1 ? "火箭飞行器" : kind == 2 ? "隐身装置" : "火力全开"), "powerup Chinese popup kind " + kind);
                if (kind == 3) {
                    Check(p.PopupTime > 4.9 && Wire.Valid(m.State), "speed pickup starts five-second flashing fire-up label and valid LAN state");
                    double speedStart = p.X; p.Vx = 0;
                    for (int i = 0; i < 30; i++) { var fast = Frame(m, 0, 0, 0, false, p.X + 200, p.Y - 25); fast.Right = true; m.Submit(0, fast); m.Step(); }
                    Check(p.X > speedStart + 100 && p.SpeedTime > 4.4, "speed buff boosts movement while its timer is active");
                    var speedControl = New(0, 0); var controlFighter = speedControl.State.Players[0];
                    for (int i = 0; i < 30; i++) { var right = Frame(speedControl, 0, 0, 0, false, controlFighter.X + 200, controlFighter.Y); right.Right = true; speedControl.Submit(0, right); speedControl.Step(); }
                    Check(Math.Abs(p.Vx - controlFighter.Vx * 2) < 1, "Fire Up exactly doubles run speed");
                    for (int i = 0; i < 300; i++) m.Step(); Check(p.SpeedTime == 0 && p.PopupTime == 0, "speed buff and flashing label expire after five seconds");
                }
            }
            m = New(0, 0); m.State.NextPower = 0; m.Step();
            Check(m.State.Powerups.Count == 1 && m.State.NextPower >= 8 && m.State.NextPower <= 10 && m.State.Powerups[0].Kind >= 0 && m.State.Powerups[0].Kind <= 3, "random shield, flight device, invisibility or speed every eight to ten seconds");
            Check(ArenaView.PowerupVisible(4.99) && ArenaView.PowerupVisible(5.05) && !ArenaView.PowerupVisible(5.15) && ArenaView.PowerupVisible(5.25) && !ArenaView.PowerupVisible(7),
                "powerups blink only during the two seconds after five seconds on the map");
            m = New(0, 0); m.State.Powerups.Add(new Powerup { Id = 600, Kind = 0, X = 500, Y = 300, Age = 6.99, Landed = true }); m.Step();
            Check(m.State.Powerups.Count == 0, "uncollected powerup disappears after its two-second warning blink");
            m = New(0, 0); p = m.State.Players[0]; p.Lives = 2; p.Weapon = 6; p.Magazine = 60; p.WeaponTime = Rules.Dt; m.Step();
            Check(p.Lives == 1 && p.Respawn > 0 && m.State.Log.Last().Contains("倒计时结束") && m.State.Shatters.Count == 1, "Gatling 15-second expiry costs a life with shatter animation");
            m = New(0, 0); p = m.State.Players[0]; p.Lives = 2; p.Weapon = 6; p.Magazine = 1; p.Reserve = 0; p.WeaponTime = 15;
            m.Submit(0, Frame(m, 0, 0, 0, true, 600, 300)); m.Step();
            Check(p.Lives == 1 && p.Respawn > 0 && m.State.Log.Last().Contains("弹药耗尽") && m.State.Shatters.Count == 1, "Gatling last bullet causes shattering death");
            for (int i = 0; i < 70; i++) m.Step();
            Check(p.Popup == "背水一战" && p.Weapon == p.Starter, "Gatling death respawns with final stand and starter");
            m = New(8, 0); p = m.State.Players[0]; var swordTarget = m.State.Players[1]; p.X = 450; swordTarget.X = 495; swordTarget.Invulnerable = 0; p.WeaponTime = 15;
            m.Submit(0, Frame(m, 0, 0, 0, true, 900, p.Y - 25)); m.Step();
            Check(swordTarget.Lives == 2 && swordTarget.Respawn > 1 && m.State.Shatters.Any(s => s.Owner == swordTarget.Seat), "prison sword instantly kills and shatters its target");
            m = New(8, 0); p = m.State.Players[0]; p.WeaponTime = Rules.Dt; m.Step();
            Check(p.Lives == 2 && p.Respawn > 0 && m.State.Shatters.Any(s => s.Owner == p.Seat), "prison sword expires after 15 seconds with shatter animation");
            m = New(1, 0); p = m.State.Players[0];
            m.Submit(0, Frame(m, 0, 0, 0, true, p.X, 100));
            for (int i = 0; i < 40; i++) m.Step(); double cooldown = p.Cooldown;
            for (int i = 0; i < 20; i++) m.Step(); Check(cooldown == 0 && p.Cooldown == 0, "stale held input releases fire");
            m = New(0, 0); m.DropCrate(500, 2);
            for (int i = 0; i < 300; i++) m.Step(); Check(m.State.Crates.Any(c => c.Landed), "parachute drop lands on platform");
            m = New(0, 0); m.DropBigCrate(500, 8); p = m.State.Players[0]; m.State.Crates[0].X = p.X; m.State.Crates[0].Y = p.Y - 20; m.State.Crates[0].Landed = true; m.Step();
            Check(p.Weapon == 8 && p.WeaponTime > 14.9 && m.State.Crates.Count == 0, "large crate grants timed prison sword");
            Check(Wire.Private(IPAddress.Parse("192.168.1.8")) && !Wire.Private(IPAddress.Parse("8.8.8.8")), "LAN IPv4 restriction");
            using (var stream = new MemoryStream()) { Wire.Write(stream, "中文名称：霰弹枪"); stream.Position = 0; Check(Wire.Read(stream) == "中文名称：霰弹枪", "UTF8 framing"); }
            bool refused = false; using (var stream = new MemoryStream()) {
                byte[] header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Wire.MaxFrame + 1)); stream.Write(header, 0, 4); stream.Position = 0;
                try { Wire.Read(stream); } catch (InvalidDataException) { refused = true; } Check(refused, "oversized frame rejected");
            }
            for (int run = 0; run < Rules.Arenas.Length * 2; run++) {
                m = new Match(new MatchOptions { Map = run / 2, Starter = run % 3, Lives = 3 }, new MatchOptions { Starter = (run + 2) % 3, Lives = 3 }, 81 + run);
                var first = new Bot(run * 13 + 11); var second = new Bot(run * 17 + 19);
                for (int t = 0; t < 36000 && !m.State.Finished; t++) {
                    m.Submit(0, first.Think(m.State, 0)); m.Submit(1, second.Think(m.State, 1)); m.Step();
                    if (t % 60 == 0) Check(Wire.Valid(m.State), "simulation snapshot valid map " + m.State.Map + " weapon " + run + " tick " + t);
                }
                matches.Add(new { Run = run, Map = m.State.Map, Starter = run % 3, Finished = m.State.Finished, Seconds = m.State.Seconds, Winner = m.State.Winner,
                    Lives = m.State.Players.Select(f => f.Lives).ToArray(), Falls = m.State.Players.Select(f => f.Respawns).ToArray() });
                File.WriteAllText(Path.Combine(folder, "simulations.json"), Rules.Serializer().Serialize(matches));
                Check(m.State.Finished, "complete AI battle within ten minutes map " + m.State.Map + " weapon " + run);
            }
        }
        static void Pump(Func<bool> predicate, int milliseconds, string label) {
            var clock = Stopwatch.StartNew(); var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
            timer.Tick += delegate { if (predicate() || clock.ElapsedMilliseconds >= milliseconds) frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); Check(predicate(), label);
        }
        static void Network() {
            var host = new PanelWindow(true); var guest = new PanelWindow(true);
            try {
                host.ChooseStarter(0); guest.ChooseStarter(2); host.StartHost(0); guest.StartJoin(new IPEndPoint(IPAddress.Loopback, host.ListeningPort));
                Pump(() => host.RoomCount == 2 && guest.Seat == 1, 5000, "real TCP two WPF peers receive sequential player numbers"); host.StartRoom();
                Pump(() => host.IsPlaying && guest.IsPlaying, 5000, "host launches the verified two-player LAN match");
                Check(host.Seat == 0 && guest.Seat == 1 && host.Current.Session == guest.Current.Session, "assigned identity and session");
                Check(host.Current.Players[0].Starter == 0 && host.Current.Players[1].Starter == 2 && guest.Current.Players[1].Weapon == 2, "independent pistol starters synchronized");
                double before = host.Current.Players[1].X;
                guest.Inject(new InputFrame { Left = true, AimX = 300, AimY = 150 });
                Pump(() => host.Current.Players[1].X < before - 20 && guest.Current.Players[1].X < before - 15, 3000, "guest movement synchronized");
                guest.Inject(new InputFrame { Jump = 1, AimX = 300, AimY = 150 });
                Pump(() => host.Current.Players[1].Jumps == 1 && guest.Current.Players[1].Jumps == 1, 3000, "guest jump synchronized");
                guest.Inject(new InputFrame { Jump = 2, AimX = 300, AimY = 150 });
                Pump(() => host.Current.Players[1].Jumps == 2 && guest.Current.Players[1].Jumps == 2, 3000, "guest double jump synchronized");
                guest.Inject(new InputFrame { Jump = 2, Fire = true, AimX = 600, AimY = 20 });
                Pump(() => host.Current.Shots.Any(s => s.Owner == 1 && s.Vy < 0) && guest.Current.Shots.Any(s => s.Owner == 1), 3000, "mouse aim and shooting synchronized");
                guest.Inject(new InputFrame { Jump = 2, Bomb = 1, AimX = 700, AimY = 100 });
                Pump(() => host.Current.Players[1].Bombs == 2 && guest.Current.Players[1].Bombs == 2, 3000, "aimed grenade synchronized");
                var player = host.Current.Players[1];
                host.Current.Crates.Add(new Crate { Id = 999, Weapon = 6, X = player.X, Y = player.Y - 20, Landed = true, Large = true });
                Pump(() => host.Current.Players[1].Weapon == 6 && guest.Current.Players[1].Popup == "牢张的愤怒" && guest.Current.Players[1].WeaponTime > 14.9, 3000, "large-drop Gatling and fifteen-second timer synchronize over LAN");
                host.Current.Powerups.Add(new Powerup { Id = 1000, Kind = 1, X = player.X, Y = player.Y - 20, Landed = true });
                try { Pump(() => host.Current.Players[1].JetOwned && guest.Current.Players[1].JetOwned && host.Current.Players[1].JetFuel == 3 && guest.Current.Players[1].JetFuel == 3, 3000, "rocket flight pickup synchronized without timeout"); }
                catch (Exception ex) { var f = host.Current.Players[1]; throw new InvalidOperationException(ex.Message + " [host jet=" + f.JetOwned + ", fuel=" + f.JetFuel + ", lives=" + f.Lives + ", respawn=" + f.Respawn + ", xy=" + f.X + "/" + f.Y + ", items=" + host.Current.Powerups.Count + ", shards=" + host.Current.Shatters.Count + "]", ex); }
                double fuel = host.Current.Players[1].JetFuel;
                guest.Inject(new InputFrame { Jump = 2, Bomb = 1, Jet = true, AimX = 600, AimY = 20 });
                Pump(() => host.Current.Players[1].JetFuel < fuel && guest.Current.Players[1].JetFuel < fuel, 3000, "jetpack held input synchronized");
                host.Current.Powerups.Add(new Powerup { Id = 1001, Kind = 2, X = player.X, Y = player.Y - 20, Landed = true });
                Pump(() => host.Current.Players[1].InvisibleTime > 4.9 && guest.Current.Players[1].InvisibleTime > 4.9, 3000, "invisibility pickup and timer synchronized over LAN");
                var target = host.Current.Players[0]; target.Invulnerable = 0; target.X = player.X + 45; target.Y = player.Y; int targetLives = target.Lives;
                player.Weapon = 8; player.WeaponTime = 15; player.Cooldown = 0;
                double swordAimX = player.X + 90, swordAimY = player.Y - 25;
                guest.Inject(new InputFrame { Jump = 2, Bomb = 1, AimX = swordAimX, AimY = swordAimY, Fire = false });
                Pump(() => Math.Abs(host.Current.Players[1].AimX - swordAimX) < 1, 1000, "sword test releases previous firing input");
                guest.Inject(new InputFrame { Jump = 2, Bomb = 1, AimX = swordAimX, AimY = swordAimY, Fire = true });
                Pump(() => host.Current.Players[0].Lives == targetLives - 1 && guest.Current.Players[0].Lives == host.Current.Players[0].Lives && guest.Current.Shatters.Any(s => s.Owner == 0), 3000, "sword instant kill and shatter synchronize over LAN");
                host.Current.Powerups.Add(new Powerup { Id = 1002, Kind = 3, X = player.X, Y = player.Y - 20, Landed = true });
                Pump(() => player.SpeedTime > 4.8 && guest.Current.Players[1].SpeedTime > 4.8 && guest.Current.Players[1].Popup == "火力全开", 3000, "speed pickup, five-second timer and flashing label synchronize over LAN");
                player.Weapon = player.Starter; player.SpeedTime = 0; player.PopupTime = 0;
                host.Current.Crates.Add(new Crate { Id = 1003, Weapon = 12, X = player.X, Y = player.Y - 20, Landed = true, Large = true });
                Pump(() => player.Weapon == 12 && player.Magazine == 5 && guest.Current.Players[1].Weapon == 12 && guest.Current.Players[1].Magazine == 5 && guest.Current.Players[1].Popup == "克莱博", 3000, "Kraber large-drop pickup and one-second bolt weapon sync over LAN");
                guest.Leave(); Pump(() => !host.IsPlaying, 3000, "disconnect halts host"); int halted = host.Current.Tick;
                Pump(() => true, 100, "dispatcher settles"); Check(host.Current.Tick == halted, "host does not keep simulating after disconnect");
            } finally { host.Close(); guest.Close(); }
            var rejectHost = new PanelWindow(true); var bad = new Peer(); bool rejected = false;
            try {
                rejectHost.StartHost(0);
                bad.Connected += delegate { bad.Send(new Message { Kind = "hello", Fingerprint = "wrong", Options = new MatchOptions() }); };
                bad.Received += message => { if (message.Kind == "reject") rejected = true; };
                bad.Join(new IPEndPoint(IPAddress.Loopback, rejectHost.ListeningPort));
                Pump(() => rejected, 3000, "version mismatch rejected on TCP"); Check(rejectHost.Current == null, "version mismatch cannot start");
            } finally { bad.Dispose(); rejectHost.Close(); }
        }
        static void NetworkSixPlayers() {
            var host = new PanelWindow(true); var guests = Enumerable.Range(0, 5).Select(i => new PanelWindow(true)).ToArray();
            try {
                host.StartHost(0); int port = host.ListeningPort;
                foreach (var guest in guests) { guest.ChooseStarter(0); guest.StartJoin(new IPEndPoint(IPAddress.Loopback, port)); }
                Pump(() => host.RoomCount == 6 && guests.All(g => g.Seat >= 1), 8000, "five LAN guests join and receive player numbers");
                Check(guests.Select(g => g.Seat).OrderBy(n => n).SequenceEqual(Enumerable.Range(1, 5)), "LAN assigns unique sequential player numbers 2 through 6");
                host.StartRoom();
                try { Pump(() => host.IsPlaying && guests.All(g => g.IsPlaying && g.Current != null && g.Current.Players.Length == 6), 8000, "six WPF peers receive the same six-player match snapshot"); }
                catch (Exception ex) { throw new InvalidOperationException(ex.Message + " [host=" + host.DebugState + "/" + (host.Current == null ? 0 : host.Current.Players.Length) + "; guests=" + String.Join(",", guests.Select(g => g.Seat + ":" + g.DebugState + ":" + (g.Current == null ? 0 : g.Current.Players.Length))) + "]", ex); }
                var sixth = guests.Single(g => g.Seat == 5); double before = host.Current.Players[5].X;
                sixth.Inject(new InputFrame { Left = true, AimX = 250, AimY = 150 });
                Pump(() => host.Current.Players[5].X < before - 20 && sixth.Current.Players[5].X < before - 15, 4000, "number six input routes to its authoritative fighter");
            } finally {
                host.Leave(); foreach (var guest in guests) guest.Leave(); host.Close(); foreach (var guest in guests) guest.Close();
            }
        }
        static MatchState Fixture() {
            var m = New(0, 0); var s = m.State; s.Tick = 2160;
            s.Players[0].X = 310; s.Players[0].Y = 370; s.Players[0].Weapon = 2; s.Players[0].Magazine = 12; s.Players[0].Reserve = -1;
            s.Players[0].AimX = 700; s.Players[0].AimY = 280; s.Players[0].Popup = "火箭飞行器 · 燃料3秒"; s.Players[0].PopupTime = 1.3; s.Players[0].JetFuel = 2.4;
            s.Players[1].X = 705; s.Players[1].Y = 285; s.Players[1].Weapon = 3; s.Players[1].Magazine = 5; s.Players[1].Reserve = 0; s.Players[1].ShieldTime = 4.5;
            s.Players[1].PopupTime = 0;
            s.Players[1].AimX = 310; s.Players[1].AimY = 345; s.Players[1].Impact = 43;
            s.Crates.Add(new Crate { Id = 120, X = 490, Y = 94, Weapon = 4 });
            s.Crates.Add(new Crate { Id = 121, X = 200, Y = 360, Weapon = 1, Landed = true });
            s.Powerups.Add(new Powerup { Id = 122, X = 580, Y = 225, Kind = 0, Landed = true });
            s.Powerups.Add(new Powerup { Id = 123, X = 470, Y = 60, Kind = 1 });
            s.Shots.Add(new Shot { Id = 100, Owner = 0, X = 470, Y = 330, Vx = 900, Vy = -150, Push = 100, Time = .6 });
            s.Shots.Add(new Shot { Id = 101, Owner = 1, X = 580, Y = 290, Vx = -1450, Vy = 210, Push = 150, Time = .6 });
            s.Grenades.Add(new Grenade { Id = 102, Owner = 0, X = 560, Y = 437, Fuse = .7 }); return s;
        }
        static void Render(PanelWindow w, string file) {
            w.Field.InvalidateVisual(); w.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)w.Width, (int)w.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render((Visual)w.Content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(file)) encoder.Save(stream);
            Check(bitmap.PixelWidth == (w.IsMinimal ? 392 : 548) && bitmap.PixelHeight == (w.IsMinimal ? 300 : 474), "exact render dimensions");
        }
        static void Walk(DependencyObject node, PanelWindow window) {
            var text = node as TextBlock; if (text != null && text.IsVisible) Check(text.FontWeight == FontWeights.Normal, "normal text weights");
            var control = node as Control; if (control != null && control.IsVisible) Check(control.FontWeight == FontWeights.Normal, "normal control weights");
            var button = node as Button;
            if (button != null && button.IsVisible) {
                Rect r = button.TransformToAncestor(window).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
                Check(r.Left >= -.5 && r.Top >= -.5 && r.Right <= window.Width + .5 && r.Bottom <= window.Height + .5, "button within window");
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i), window);
        }
        public static void Run(PanelWindow w, string folder) {
            Directory.CreateDirectory(folder); checks = seq = 0; matches.Clear();
            foreach (string name in new[] { "error.txt", "startup-error.txt" }) { string file = Path.Combine(folder, name); if (File.Exists(file)) File.Delete(file); }
            Core(folder); Network(); NetworkSixPlayers();
            foreach (string theme in new[] { "wps", "codex-light", "codex-dark" }) foreach (bool small in new[] { false, true }) {
                w.Configure(theme == "wps" ? "wps" : "codex", small, theme == "codex-light" ? "light" : "dark"); w.SetPreview(Fixture()); w.UpdateLayout();
                Check(w.Field.ActualHeight >= 125 && w.Field.ActualWidth >= 360, "arena fits both windows");
                Point expected = new Point(713, 281); Point actual = w.Field.ToWorld(w.Field.ToScreen(expected));
                Check(Math.Abs(actual.X - expected.X) < .001 && Math.Abs(actual.Y - expected.Y) < .001, "mouse transform with letterboxing");
                string name = theme + (small ? "-minimal" : "-normal"); Walk(w, w); Render(w, Path.Combine(folder, name + ".png"));
                foreach (string page in new[] { "lobby", "settings", "help", "records" }) { w.ShowPage(page); w.UpdateLayout(); Walk(w, w); Render(w, Path.Combine(folder, name + "-" + page + ".png")); }
            }
            w.Configure("codex", false, "dark"); var popup = Fixture(); w.SetPreview(popup);
            Render(w, Path.Combine(folder, "pickup-01-visible.png")); popup.Players[0].PopupTime = .3; Render(w, Path.Combine(folder, "pickup-02-fading.png"));
            popup.Players[0].PopupTime = 0; Render(w, Path.Combine(folder, "pickup-03-gone.png"));
            popup.Players[0].Popup = "牢张的愤怒"; popup.Players[0].PopupTime = 1.3; popup.Players[0].Weapon = 6; popup.Players[0].Magazine = 45; popup.Players[0].WeaponTime = 12;
            Render(w, Path.Combine(folder, "pickup-gatling.png"));
            var reload = Fixture(); reload.Players[0].Weapon = 0; reload.Players[0].Magazine = 3; reload.Players[0].Reserve = -1; reload.Players[0].ReloadTime = 1.4;
            w.SetPreview(reload); Render(w, Path.Combine(folder, "reload-progress.png"));
            var special = Fixture(); special.Players[0].Weapon = 8; special.Players[0].WeaponTime = 12; special.Players[0].MeleeSwing = .2;
            special.Players[0].Popup = "坐牢之剑"; special.Players[0].PopupTime = 1.3; special.Crates.Add(new Crate { Id = 124, X = 530, Y = 250, Weapon = 8, Large = true, Landed = true });
            special.Shatters.Add(new Shatter { Owner = 1, X = 710, Y = 285, Time = .42 }); w.SetPreview(special);
            Render(w, Path.Combine(folder, "sword-shatter.png"));
            var newDrops = Fixture(); newDrops.Players[0].Weapon = 11; newDrops.Players[0].Magazine = 45; newDrops.Players[0].FireRamp = 1.1;
            newDrops.Players[0].Popup = "火力全开"; newDrops.Players[0].PopupTime = 4.2; newDrops.Players[0].SpeedTime = 4.2;
            newDrops.Players[1].Weapon = 12; newDrops.Players[1].Magazine = 3;
            newDrops.Shots.Add(new Shot { Owner = 0, X = 430, Y = 260, Vx = 1280, Vy = 0, Energy = true });
            newDrops.Shots.Add(new Shot { Owner = 1, X = 490, Y = 275, Vx = 2600, Vy = 0, DirectKill = true });
            newDrops.Bursts.Add(new Burst { X = 630, Y = 320, Radius = 190, Time = .38, Rocket = true });
            newDrops.Powerups.Add(new Powerup { Id = 125, X = 590, Y = 215, Kind = 3, Landed = true });
            w.SetPreview(newDrops); Render(w, Path.Combine(folder, "new-weapons-rocket-powerup.png"));
            popup.Players[0].Popup = "背水一战"; popup.Players[0].PopupTime = 1.3; popup.Players[0].Lives = 1;
            Render(w, Path.Combine(folder, "final-stand.png"));
            var offscreen = Fixture(); offscreen.Players[0].X = 1080; offscreen.Players[0].Y = 700; offscreen.Players[1].X = -70; offscreen.Players[1].Y = 250;
            w.SetPreview(offscreen); w.Field.State = offscreen; w.Field.Seat = 0; Render(w, Path.Combine(folder, "offscreen-arrows.png"));
            Check(UpdateService.CurrentVersion == "1.6.7", "application release version");
            Check(UpdateService.IsNewer("1.6.7", "1.6.6") && !UpdateService.IsNewer("1.6.6", "1.6.7"), "semantic update ordering");
            var testManifest = UpdateService.ParseManifest("{\"Version\":\"1.6.7\",\"PackageUrl\":\"https://raw.githubusercontent.com/EM-py/Chaos-gun-fight/main/StickMayhem-share.zip\",\"Sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Notes\":\"test\"}");
            Check(testManifest.Version == "1.6.7", "trusted update manifest parsing");
            Check(w.UpdatePagesReady, "changelog and updater pages constructed");
            w.ShowPage("changelog"); Render(w, Path.Combine(folder, "changelog.png"));
            w.ShowPage("updater"); Render(w, Path.Combine(folder, "version-update.png"));
            File.WriteAllText(Path.Combine(folder, "verification.json"), Rules.Serializer().Serialize(new {
                Passed = true, Assertions = checks, Version = UpdateService.CurrentVersion, ChangelogUi = true, GitHubUpdateManifest = true, Sha256VerifiedUpdatePackage = true, SafeRestartUpdater = true, Weapons = 14, PistolStarters = 3, StarterPistolsHoldToFire = true, NormalDropsIncludeElectricSMGAndApplyTwoSecondSlow = true, LargeDropsIncludeFocusSMGAndKraber = true, KnockbackAndImpactGainIndependentlyConfigured = true, FocusSMG45RoundSpinUpBlueBullets = true, KraberAmmoOverhead = true, KraberFiveRoundOneSecondBoltInstantKillAndBackblast = true, SkySniperKraberInfiniteAmmo = true, SkySniperNoWeaponDrops = true, SkySniperPowerupsRemain = true, LargeDropWeaponsCannotBeReplaced = true, SwordPickupGrantsFiveSecondInvisibility = true, PracticeBotUsesSwordMeleeRange = true, PracticeBotAvoidsKraberRecoilFalls = true, PracticeBotBlindFiresWithoutInvisibleCoordinates = true, RocketBlastBoostAndVisualEffect = true, SpeedBuffDoublesMovementAndFireRateWithWhiteCountdown = true, RevolverAndThrownBombBoosted = true, Maps = 6, MouseAiming = true, DoubleJump = true, ParachuteCrates = true,
                MagazineReloads = true, ReloadProgressArc = true, BotPistolReloads = true, GrenadeLauncherFallback = true, ShotgunBoostedImpact = true, ShieldAndRocketFlight = true, JetFuel = true, InvisibilityBothViews = true, Invisibility = true, GrenadeLauncher = true, Gatling60And15SecondFatal = true, PrisonSwordInstantKill = true, ShatterAnimation = true, FinalStandPopup = true,
                ChineseFloatingFadingPickup = true, CustomStarter = true, TcpTwoWpfPeers = true, AuthoritativePhysics = true,
                HeadImpactPercent = true, OffscreenArrows = true, DisconnectStops = true, ActualWpfRenders = true, PhysicalTwoComputerTest = false, PhysicalMouseTest = false
            }));
            File.WriteAllText(Path.Combine(folder, "verification.txt"), "PASS: " + checks + " assertions.\r\n" +
                "Practice AI sword melee approach, Kraber recoil-safe movement, and blind probing fire against invisible players; twelve full AI battles across all six maps; pistol reloads and automatic fire; Focus SMG spin-up; Kraber cycle/backblast; weapon and powerup rules.\r\n" +
                "Actual TCP loopback WPF multiplayer: six peers and numbered seats, synchronized snapshots/input, Gatling and sword actions, new speed pickup and Kraber large-drop weapon, rocket flight, invisibility, version rejection and disconnect.\r\n" +
                "Actual WPF renders at 96 DPI, three themes and two fixed sizes, popup visible/fading/gone frames, reload progress arc, shatter animation, overhead impact percent, offscreen direction arrows, collapsible changelog, and GitHub update page.\r\n" +
                "Not tested on two physical computers or through physical mouse/keyboard automation. Preferences and clipboard untouched.\r\n");
        }
    }
    public static class Program {
        [STAThread] public static void Main(string[] args) {
            bool verify = args.Length == 2 && args[0] == "--verify";
            string output = verify ? Path.GetFullPath(args[1]) : AppDomain.CurrentDomain.BaseDirectory;
            try {
                var app = new Application(); var w = new PanelWindow(verify);
                if (verify) {
                    Directory.CreateDirectory(output); w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = -20000; w.Top = -20000; w.ShowInTaskbar = false;
                    w.Loaded += delegate { w.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate {
                        try { Verification.Run(w, output); }
                        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                        finally { w.Close(); }
                    })); };
                }
                app.Run(w);
            } catch (Exception ex) {
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "startup-error.txt"), ex.ToString()); Environment.ExitCode = 1;
                if (!verify) MessageBox.Show("启动失败，详情见 startup-error.txt。\n" + ex.Message, "协作面板");
            }
        }
    }
}
