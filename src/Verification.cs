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
            Check(Rules.Weapons.Length == 9 && Rules.Arenas.Length == 3, "nine weapons three arenas");
            var m = New(0, 0); var p = m.State.Players[0];
            Check(Wire.Valid(m.State), "initial snapshot");
            Check(Rules.StarterCount == 3 && Rules.Weapons.Take(3).Select(w => w.Name).SequenceEqual(new[] { "左轮", "USP", "格洛克" }), "three pistol starter choices");
            Check(p.Weapon == 0 && p.Magazine == 6 && p.Reserve == -1 && p.Bombs == 3, "custom starter has infinite reserve and finite magazine");
            Check(Rules.Weapons[0].Reload == 3 && Rules.Weapons[1].Magazine == 12 && Rules.Weapons[1].Reload == 2 &&
                Rules.Weapons[2].Magazine == 20 && Rules.Weapons[2].Automatic && Rules.Weapons[2].Push < Rules.Weapons[1].Push && Rules.Weapons[1].Push < Rules.Weapons[0].Push,
                "requested pistol magazines reloads damage and automatic fire");
            Check(Rules.Weapons[6].Magazine == 60 && Rules.Weapons[6].Ammo == 60 && Rules.Weapons[7].Explosive && Rules.Weapons[8].Melee, "Gatling 60 rounds, grenade launcher and melee sword configured");
            Check(Rules.Weapons[7].Magazine == 12 && Rules.Weapons[7].Ammo == 12, "grenade launcher has one finite twelve-round magazine");
            Check(Rules.Weapons[3].Push > 100 && Rules.Weapons[3].Pellets > 5, "shotgun impact and pellet count increased");
            Check(Enumerable.Range(0, 200).All(i => { int w = m.RandomAirdropWeapon(); return w >= Rules.StarterCount && w != 6 && w != 8; }), "normal airdrops exclude starter and special weapons");
            Check(Enumerable.Range(0, 100).All(i => { int w = m.RandomBigAirdropWeapon(); return w == 6 || w == 8; }), "large airdrops contain only Gatling or prison sword");
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
            m = New(4, 0); var enemy = m.State.Players[1]; p = m.State.Players[0]; p.X = 450; enemy.X = 490;
            m.Submit(0, Frame(m, 0, 0, 0, true, enemy.X, enemy.Y - 25)); m.Step();
            Check(enemy.Impact > 0 && enemy.Vx > 300 && enemy.Vy < 0, "fast swept bullet hit and directional knockback");
            int healthLives = enemy.Lives; Check(healthLives == 3, "hits affect knockback not stock count");
            m = New(0, 0); p = m.State.Players[0]; enemy = m.State.Players[1]; p.X = 450; enemy.X = 490; enemy.Invulnerable = 1;
            m.Submit(0, Frame(m, 0, 0, 0, true, enemy.X, enemy.Y - 25)); m.Step(); Check(enemy.Impact == 0, "spawn protection");
            m = New(0, 0); p = m.State.Players[0];
            m.Submit(0, Frame(m, 0, 1, 0, false, p.X + 100, p.Y - 200)); m.Step();
            Check(p.Bombs == 2 && m.State.Grenades.Count == 1, "mouse aimed grenade");
            for (int i = 0; i < 90; i++) m.Step(); Check(m.State.Grenades.Count == 0, "grenade fuse expires");
            m = New(0, 0); p = m.State.Players[0];
            m.State.Grenades.Add(new Grenade { Owner = 0, X = p.X, Y = p.Y - 25, Fuse = .01 }); m.Step();
            Check(p.Impact > 0 && p.Vy < 0, "grenade can hit own player");
            m = New(0, 0); p = m.State.Players[0];
            m.State.Grenades.Add(new Grenade { Owner = 0, X = 500, Y = 490, Vy = 150, Fuse = 1 });
            m.Step(); m.Step(); Check(m.State.Grenades[0].Vy < 0, "grenade platform bounce");
            for (int weapon = 0; weapon < Rules.Weapons.Length; weapon++) {
                m = New(0, 0); p = m.State.Players[0];
                m.State.Crates.Add(new Crate { Id = 100, Weapon = weapon, X = p.X, Y = p.Y - 20, Landed = true, Large = weapon == 6 || weapon == 8 }); m.Step();
                int expectedReserve = weapon < Rules.StarterCount ? -1 : Math.Max(0, Rules.Weapons[weapon].Ammo - Rules.Weapons[weapon].Magazine);
                Check(p.Weapon == weapon && p.Magazine == Rules.Weapons[weapon].Magazine && p.Reserve == expectedReserve && m.State.Crates.Count == 0, "crate grants each weapon and ammo");
                string pickup = String.IsNullOrEmpty(Rules.Weapons[weapon].Pickup) ? Rules.Weapons[weapon].Name : Rules.Weapons[weapon].Pickup;
                Check(p.Popup == pickup && p.PopupTime == 1.65, "Chinese pickup label");
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
            // Semi-auto pistols require a fresh click; Glock can keep firing while held.
            m = New(0, 0); p = m.State.Players[0]; m.Submit(0, Frame(m, 0, 0, 0, true, p.X, 100));
            for (int i = 0; i < 25; i++) m.Step(); Check(p.Magazine == 5, "revolver does not auto-fire while held");
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
                p.Magazine = Rules.Weapons[pistol].Magazine; p.Reserve = -1; enemy.Invulnerable = 2; m.State.NextCrate = 1000;
                var bot = new Bot(700 + pistol);
                for (int t = 0; t < 1500; t++) { enemy.Invulnerable = 2; m.Submit(1, bot.Think(m.State, 1)); m.Step(); }
                while (p.ReloadTime > 0) { enemy.Invulnerable = 2; m.Submit(1, bot.Think(m.State, 1)); m.Step(); }
                Check(p.Lives > 0 && p.Reserve == -1 && p.Magazine > 0 && p.ReloadTime == 0, "practice bot reloads infinite-reserve pistol " + pistol);
            }
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
            for (int kind = 0; kind < 3; kind++) {
                m = New(0, 0); p = m.State.Players[0]; m.State.Powerups.Add(new Powerup { Id = 500 + kind, Kind = kind, X = p.X, Y = p.Y - 20, Landed = true }); m.Step();
                Check(m.State.Powerups.Count == 0 && (kind == 0 ? p.ShieldTime > 5.9 : kind == 1 ? p.JetOwned && p.JetFuel == 3 : p.InvisibleTime > 4.9), "powerup pickup kind " + kind);
                Check(p.Popup.Contains(kind == 0 ? "护盾" : kind == 1 ? "火箭飞行器" : "隐身装置"), "powerup Chinese popup kind " + kind);
            }
            m = New(0, 0); m.State.NextPower = 0; m.Step();
            Check(m.State.Powerups.Count == 1 && m.State.NextPower >= 8 && m.State.NextPower <= 10, "random shield, flight device or invisibility every eight to ten seconds");
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
            for (int run = 0; run < 6; run++) {
                m = new Match(new MatchOptions { Map = run % 3, Starter = run % 3, Lives = 3 }, new MatchOptions { Starter = (run + 2) % 3, Lives = 3 }, 81 + run);
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
                Pump(() => host.IsPlaying && guest.IsPlaying, 5000, "real TCP two WPF peers connect");
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
                Check(host.Engine.Submit(1, new InputFrame { Session = host.Current.Session, Seq = 10000, Jump = 2, Bomb = 1, AimX = player.X + 90, AimY = player.Y - 25, Fire = true }), "sword attack input accepted by host simulation");
                try { Pump(() => host.Current.Players[0].Lives == targetLives - 1 && guest.Current.Players[0].Lives == host.Current.Players[0].Lives && guest.Current.Shatters.Any(s => s.Owner == 0), 3000, "sword instant kill and shatter synchronize over LAN"); }
                catch (Exception ex) { var f = host.Current.Players[0]; throw new InvalidOperationException(ex.Message + " [sword=" + player.Weapon + "/" + player.WeaponTime + ", cooldown=" + player.Cooldown + ", targetLives=" + f.Lives + "/" + targetLives + ", targetRespawn=" + f.Respawn + ", targetInv=" + f.Invulnerable + ", delta=" + (f.X - player.X) + "/" + (f.Y - player.Y) + ", shards=" + host.Current.Shatters.Count + ", last=" + host.Current.Log.Last() + "]", ex); }
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
            Core(folder); Network();
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
            popup.Players[0].Popup = "背水一战"; popup.Players[0].PopupTime = 1.3; popup.Players[0].Lives = 1;
            Render(w, Path.Combine(folder, "final-stand.png"));
            var offscreen = Fixture(); offscreen.Players[0].X = 1080; offscreen.Players[0].Y = 700; offscreen.Players[1].X = -70; offscreen.Players[1].Y = 250;
            w.SetPreview(offscreen); w.Field.State = offscreen; w.Field.Seat = 0; Render(w, Path.Combine(folder, "offscreen-arrows.png"));
            File.WriteAllText(Path.Combine(folder, "verification.json"), Rules.Serializer().Serialize(new {
                Passed = true, Assertions = checks, Weapons = 9, PistolStarters = 3, NormalDropsExcludeStarterAndSpecialWeapons = true, LargeDropsContainGatlingAndSword = true, Maps = 3, MouseAiming = true, DoubleJump = true, ParachuteCrates = true,
                MagazineReloads = true, ReloadProgressArc = true, BotPistolReloads = true, GrenadeLauncherFallback = true, ShotgunBoostedImpact = true, ShieldAndRocketFlight = true, JetFuel = true, InvisibilityBothViews = true, Invisibility = true, GrenadeLauncher = true, Gatling60And15SecondFatal = true, PrisonSwordInstantKill = true, ShatterAnimation = true, FinalStandPopup = true,
                ChineseFloatingFadingPickup = true, CustomStarter = true, TcpTwoWpfPeers = true, AuthoritativePhysics = true,
                HeadImpactPercent = true, OffscreenArrows = true, DisconnectStops = true, ActualWpfRenders = true, PhysicalTwoComputerTest = false, PhysicalMouseTest = false
            }));
            File.WriteAllText(Path.Combine(folder, "verification.txt"), "PASS: " + checks + " assertions.\r\n" +
                "Six full AI battles across all maps and pistol starters; movement, double-jump, drop-through, mouse-direction shots, swept collisions, knockback, thrown and launched grenades, grenade-launcher fallback, player and bot reloads, 60-round/15-second Gatling, timed prison sword instant kill, shatter animation, normal and large airdrop pools, shotgun boost, final-stand popup, shield, unlimited-duration rocket flight with finite fuel, invisibility pickup and perspective rendering, Chinese floating fade, respawn and victory.\r\n" +
                "Two WPF peers over actual TCP loopback; separate pistol loadouts, input, mouse aiming, firing, double jump, grenade, large-crate Gatling and sword kill animation, rocket-flight pickup and thrust, invisibility state synchronization, version rejection and disconnect.\r\n" +
                "Actual WPF renders at 96 DPI, three themes and two fixed sizes, popup visible/fading/gone frames, reload progress arc, shatter animation, overhead impact percent and offscreen direction arrows.\r\n" +
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
