using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace StickMayhem {
    public sealed class Weapon {
        public string Name, Pickup;
        public double Interval, MinInterval, RampDuration, Speed, Push, ImpactPush, Spread, Reload;
        public int Pellets, Ammo, Magazine;
        public bool Rocket, Automatic, Explosive, Melee, Energy, DirectKill, BoltAction, SlowTarget;
    }
    public sealed class Platform { public double X, Y, Width; }
    public sealed class Arena { public string Name; public Platform[] Platforms; public double[] Spawns; }
    public static class Rules {
        public const string Version = "StickMayhem/1.6.0";
        public const double Dt = 1.0 / 60, Width = 1000, Height = 600, Gravity = 1450;
        public const int StarterCount = 3;
        public static bool LargeDropWeapon(int weapon) { return weapon == 6 || weapon == 8 || weapon == 11 || weapon == 12; }
        public static readonly Weapon[] Weapons = {
            new Weapon { Name = "左轮", Interval = .52, Speed = 1420, Push = 420, Pellets = 1, Spread = 0, Magazine = 6, Ammo = 18, Reload = 3, Automatic = true },
            new Weapon { Name = "USP", Interval = .25, Speed = 1280, Push = 185, Pellets = 1, Spread = .018, Magazine = 12, Ammo = 36, Reload = 2, Automatic = true },
            new Weapon { Name = "格洛克", Interval = .11, Speed = 1180, Push = 92, Pellets = 1, Spread = .05, Magazine = 20, Ammo = 60, Reload = 1.5, Automatic = true },
            new Weapon { Name = "霰弹枪", Interval = .8, Speed = 900, Push = 180, Pellets = 6, Spread = .14, Magazine = 8, Ammo = 8 },
            new Weapon { Name = "狙击枪", Interval = 1.1, Speed = 2300, Push = 490, Pellets = 1, Spread = 0, Magazine = 6, Ammo = 6 },
            new Weapon { Name = "火箭筒", Interval = 1.2, Speed = 610, Push = 550, Pellets = 1, Spread = 0, Magazine = 5, Ammo = 5, Rocket = true }
            ,new Weapon { Name = "加特林", Pickup = "牢张的愤怒", Interval = .035, Speed = 1500, Push = 220, Pellets = 1, Spread = .065, Magazine = 60, Ammo = 60, Automatic = true }
            ,new Weapon { Name = "榴弹发射器", Interval = .72, Speed = 700, Push = 760, Pellets = 1, Spread = .025, Magazine = 12, Ammo = 12, Explosive = true }
            ,new Weapon { Name = "坐牢之剑", Interval = .48, Magazine = 1, Ammo = 1, Melee = true }
            ,new Weapon { Name = "突击步枪", Interval = .13, Speed = 1360, Push = 395, Pellets = 1, Spread = .025, Magazine = 30, Ammo = 60, Reload = 1.8, Automatic = true }
            ,new Weapon { Name = "冲锋枪", Interval = .095, Speed = 1160, Push = 185, Pellets = 1, Spread = .065, Magazine = 30, Ammo = 60, Reload = 1.5, Automatic = true }
            ,new Weapon { Name = "专注冲锋枪", Pickup = "专注冲锋枪", Interval = .28, MinInterval = .075, RampDuration = 1.2, Speed = 1280, Push = 150, Pellets = 1, Spread = .055, Magazine = 45, Ammo = 45, Automatic = true, Energy = true }
            ,new Weapon { Name = "克莱博", Pickup = "克莱博", Interval = .55, Speed = 2600, Push = 0, Pellets = 1, Spread = 0, Magazine = 5, Ammo = 5, Reload = 1, BoltAction = true, DirectKill = true }
            ,new Weapon { Name = "电能冲锋枪", Pickup = "电能冲锋枪", Interval = .12, Speed = 1280, Push = 92, ImpactPush = 185, Pellets = 1, Spread = .05, Magazine = 30, Ammo = 60, Reload = 1.5, Automatic = true, Energy = true, SlowTarget = true }
        };
        public static readonly Arena[] Arenas = {
            new Arena { Name = "分层平台", Spawns = new[] { 150.0, 850, 290, 710, 440, 560 }, Platforms = new[] {
                P(110, 500, 780), P(130, 370, 240), P(630, 370, 240), P(360, 240, 280), P(180, 135, 150), P(670, 135, 150) } },
            new Arena { Name = "双桥断层", Spawns = new[] { 150.0, 850, 250, 750, 350, 650 }, Platforms = new[] {
                P(70, 500, 350), P(580, 500, 350), P(310, 375, 380), P(60, 245, 245), P(695, 245, 245), P(355, 135, 290) } },
            new Arena { Name = "交错阶梯", Spawns = new[] { 160.0, 840, 300, 700, 440, 560 }, Platforms = new[] {
                P(130, 520, 740), P(65, 410, 190), P(745, 410, 190), P(270, 300, 180), P(550, 300, 180), P(365, 165, 270) } },
            new Arena { Name = "风车中庭", Spawns = new[] { 120.0, 880, 260, 740, 430, 570 }, Platforms = new[] {
                P(80, 510, 840), P(35, 385, 150), P(815, 385, 150), P(360, 385, 280), P(140, 260, 230), P(630, 260, 230), P(350, 145, 300) } },
            new Arena { Name = "断桥高台", Spawns = new[] { 120.0, 880, 260, 740, 430, 570 }, Platforms = new[] {
                P(75, 500, 850), P(75, 370, 300), P(625, 370, 300), P(350, 250, 300), P(180, 135, 180), P(640, 135, 180) } },
            new Arena { Name = "环形回廊", Spawns = new[] { 120.0, 880, 260, 740, 430, 570 }, Platforms = new[] {
                P(80, 510, 840), P(120, 410, 220), P(660, 410, 220), P(285, 315, 430), P(85, 215, 220), P(695, 215, 220), P(370, 115, 260) } }
        };
        static Platform P(double x, double y, double width) { return new Platform { X = x, Y = y, Width = width }; }
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 196608, RecursionLimit = 25 }; }
        public static T Copy<T>(T value) { return Serializer().Deserialize<T>(Serializer().Serialize(value)); }
        public static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
        public static double Clamp(double v, double min, double max) { return Math.Max(min, Math.Min(max, v)); }
        public static string Fingerprint() {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(
                Version + "/60Hz/mouse/doublejump/reload/powerups/knockback/invisibility/specialdrops/melee/shatter/" + Serializer().Serialize(Weapons) + Serializer().Serialize(Arenas))));
        }
    }
    public sealed class MatchOptions { public int Map, Lives = 5, Starter; }
    public sealed class InputFrame {
        public string Session;
        public int Seq, Jump, Bomb, Drop;
        public bool Left, Right, Fire, Jet;
        public double AimX = 500, AimY = 250;
    }
    public sealed class Fighter {
        public int Seat, Lives, Starter, Weapon, Magazine, Reserve, Jumps, Bombs = 3, Respawns;
        public double X, Y, Vx, Vy, AimX, AimY, Impact, Cooldown, ReloadTime, BombCooldown, Invulnerable, Respawn, DropTime, HitStun;
        public double ShieldTime, JetFuel, InvisibleTime, WeaponTime, MeleeSwing, SpeedTime, FireRamp, SlowTime;
        public bool Grounded, JetOwned;
        public string Popup;
        public double PopupTime;
    }
    public sealed class Shot {
        public int Id, Owner;
        public double X, Y, Vx, Vy, Push, ImpactPush, Time;
        public bool Rocket, Energy, DirectKill, SlowTarget;
    }
    public sealed class Grenade {
        public int Id, Owner;
        public double X, Y, Vx, Vy, Fuse = 1.3;
        public bool Launched;
    }
    public sealed class Crate {
        public int Id, Weapon;
        public double X, Y, Vy, Age;
        public bool Landed, Large;
    }
    public sealed class Shatter { public int Id, Owner; public double X, Y, Time = .85; }
    public sealed class Powerup {
        public int Id, Kind;
        public double X, Y, Vy, Age;
        public bool Landed;
    }
    public sealed class Burst { public double X, Y, Radius, Time = .48; public bool Rocket; }
    public sealed class MatchState {
        public string Session;
        public int Tick, Map, Winner = -2;
        public double NextCrate = 3, NextBigCrate = 18, NextPower = 8;
        public Fighter[] Players;
        public List<Shot> Shots = new List<Shot>();
        public List<Grenade> Grenades = new List<Grenade>();
        public List<Crate> Crates = new List<Crate>();
        public List<Powerup> Powerups = new List<Powerup>();
        public List<Burst> Bursts = new List<Burst>();
        public List<Shatter> Shatters = new List<Shatter>();
        public List<string> Log = new List<string>();
        public bool Finished { get { return Winner != -2; } }
        public double Seconds { get { return Tick * Rules.Dt; } }
    }
    public sealed class Match {
        public MatchState State;
        Random random;
        InputFrame[] inputs;
        int[] jump, bomb, drop, inputTicks;
        bool[] fireHeld;
        int nextId;
        public Match(MatchOptions host, MatchOptions guest, int seed) {
            Start(new[] { host, guest }, seed);
        }
        public Match(MatchOptions[] players, int seed) { Start(players, seed); }
        void Start(MatchOptions[] options, int seed) {
            if (options == null || options.Length < 2 || options.Length > 6 || options.Any(o => !ValidOptions(o))) throw new ArgumentException("开局选项无效。");
            random = new Random(seed);
            inputs = new InputFrame[options.Length]; jump = new int[options.Length]; bomb = new int[options.Length]; drop = new int[options.Length]; inputTicks = new int[options.Length]; fireHeld = new bool[options.Length];
            State = new MatchState { Session = Guid.NewGuid().ToString("N"), Map = options[0].Map, Players = new Fighter[options.Length] };
            for (int seat = 0; seat < options.Length; seat++) {
                State.Players[seat] = new Fighter { Seat = seat, Starter = options[seat].Starter, Lives = options[0].Lives };
                Spawn(State.Players[seat]);
            }
            Log("对局开始 · " + options.Length + " 人混战 · 掉出场地扣命");
        }
        public static bool ValidOptions(MatchOptions o) {
            return o != null && o.Map >= 0 && o.Map < Rules.Arenas.Length && o.Lives >= 3 && o.Lives <= 10 && o.Starter >= 0 && o.Starter < Rules.StarterCount;
        }
        public void Log(string text) {
            State.Log.Add(String.Format("{0:00}:{1:00}  {2}", (int)State.Seconds / 60, (int)State.Seconds % 60, text));
            if (State.Log.Count > 60) State.Log.RemoveAt(0);
        }
        public bool Submit(int seat, InputFrame value) {
            if (seat < 0 || seat >= State.Players.Length || State.Finished || value == null || value.Session != State.Session ||
                value.Seq <= 0 || value.Jump < 0 || value.Bomb < 0 || value.Drop < 0 ||
                !Rules.Finite(value.AimX) || !Rules.Finite(value.AimY) || value.AimX < -300 || value.AimX > 1300 || value.AimY < -250 || value.AimY > 850) return false;
            var old = inputs[seat];
            if (old != null && (value.Seq <= old.Seq || value.Jump < old.Jump || value.Bomb < old.Bomb || value.Drop < old.Drop ||
                value.Jump - old.Jump > 8 || value.Bomb - old.Bomb > 8 || value.Drop - old.Drop > 8)) return false;
            if (old == null && (value.Jump > 8 || value.Bomb > 8 || value.Drop > 8)) return false;
            inputs[seat] = new InputFrame { Session = value.Session, Seq = value.Seq, Jump = value.Jump, Bomb = value.Bomb, Drop = value.Drop,
                Left = value.Left, Right = value.Right, Fire = value.Fire, Jet = value.Jet, AimX = value.AimX, AimY = value.AimY };
            inputTicks[seat] = State.Tick; return true;
        }
        void Spawn(Fighter p) {
            p.X = Rules.Arenas[State.Map].Spawns[p.Seat];
            var start = Rules.Arenas[State.Map].Platforms.Where(t => p.X >= t.X && p.X <= t.X + t.Width).OrderByDescending(t => t.Y).First();
            p.Y = start.Y - 1; p.Vx = p.Vy = 0; p.Jumps = 0; p.Grounded = false; p.Impact = 0;
            p.Bombs = 3; EquipStarter(p); p.Cooldown = p.ReloadTime = 0; p.BombCooldown = 0;
            p.ShieldTime = p.JetFuel = p.InvisibleTime = p.WeaponTime = p.MeleeSwing = p.SpeedTime = p.FireRamp = p.SlowTime = 0; p.JetOwned = false;
            p.Invulnerable = 1.8; p.Respawn = 0; p.HitStun = p.DropTime = 0;
            p.AimX = p.Seat == 0 ? 760 : 240; p.AimY = p.Y - 26;
            Popup(p, p.Lives == 1 && p.Respawns > 0 ? "背水一战" : Rules.Weapons[p.Weapon].Name);
        }
        public static double PopupOpacity(Fighter p) { return Rules.Clamp(p.PopupTime / .65, 0, 1); }
        public static double PopupRise(Fighter p) { return Rules.Clamp((1.65 - p.PopupTime) * 23, 0, 38); }
        static void Popup(Fighter p, string name) { p.Popup = name; p.PopupTime = 1.65; }
        static void EquipStarter(Fighter p) {
            p.Weapon = p.Starter; p.Magazine = Rules.Weapons[p.Starter].Magazine; p.Reserve = -1;
        }
        void BeginReload(Fighter p) {
            var weapon = Rules.Weapons[p.Weapon];
            if (p.ReloadTime > 0 || p.Magazine >= weapon.Magazine) return;
            if (p.Reserve == 0) { EquipStarter(p); Popup(p, Rules.Weapons[p.Starter].Name + " · 备用"); return; }
            p.ReloadTime = weapon.Reload;
        }
        void FinishReload(Fighter p) {
            var weapon = Rules.Weapons[p.Weapon];
            if (weapon.BoltAction) return;
            if (p.Reserve < 0) p.Magazine = weapon.Magazine;
            else {
                int amount = Math.Min(weapon.Magazine - p.Magazine, p.Reserve);
                p.Magazine += amount; p.Reserve -= amount;
                if (p.Magazine == 0) { EquipStarter(p); Popup(p, Rules.Weapons[p.Starter].Name + " · 备用"); }
            }
        }
        void Direction(Fighter p, out double dx, out double dy) {
            dx = p.AimX - p.X; dy = p.AimY - (p.Y - 25);
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1) { dx = p.Seat == 0 ? 1 : -1; dy = 0; }
            else { dx /= length; dy /= length; }
        }
        void Fire(Fighter p) {
            var weapon = Rules.Weapons[p.Weapon];
            if (p.Cooldown > 0 || p.ReloadTime > 0) return;
            if (weapon.Melee) {
                p.MeleeSwing = .28; p.Cooldown = weapon.Interval;
                double meleeDx, meleeDy; Direction(p, out meleeDx, out meleeDy);
                var enemy = State.Players.Where(target => target.Seat != p.Seat && target.Lives > 0 && target.Respawn <= 0 && target.Invulnerable <= 0)
                    .Select(target => new { Player = target, Dx = target.X - p.X, Dy = target.Y - p.Y })
                    .Select(hit => new { hit.Player, Forward = hit.Dx * meleeDx + hit.Dy * meleeDy, Side = Math.Abs(hit.Dx * meleeDy - hit.Dy * meleeDx) })
                    .Where(hit => hit.Forward >= 0 && hit.Forward <= 88 && hit.Side <= 21 + hit.Forward * .18).OrderBy(hit => hit.Forward).FirstOrDefault();
                if (enemy != null) DirectDeath(enemy.Player, "被坐牢之剑击中", true);
                return;
            }
            if (weapon.Explosive ? State.Grenades.Count >= 8 : State.Shots.Count >= 160) return;
            if (p.Magazine <= 0) { BeginReload(p); return; }
            double dx, dy; Direction(p, out dx, out dy); double angle = Math.Atan2(dy, dx);
            for (int n = 0; n < weapon.Pellets && !weapon.Explosive; n++) {
                double offset = weapon.Pellets > 1 ? (n - (weapon.Pellets - 1) / 2.0) * weapon.Spread : (random.NextDouble() - .5) * weapon.Spread;
                double sx = Math.Cos(angle + offset), sy = Math.Sin(angle + offset);
                State.Shots.Add(new Shot { Id = ++nextId, Owner = p.Seat, X = p.X + dx * 23, Y = p.Y - 25 + dy * 23,
                    Vx = sx * weapon.Speed, Vy = sy * weapon.Speed, Push = weapon.Push, Time = weapon.Rocket || weapon.DirectKill ? 2.2 : 1.1,
                    ImpactPush = weapon.ImpactPush, Rocket = weapon.Rocket, Energy = weapon.Energy, DirectKill = weapon.DirectKill, SlowTarget = weapon.SlowTarget });
            }
            if (weapon.Explosive) State.Grenades.Add(new Grenade { Id = ++nextId, Owner = p.Seat,
                X = p.X + dx * 23, Y = p.Y - 25 + dy * 23, Vx = dx * weapon.Speed + p.Vx * .25,
                Vy = dy * weapon.Speed, Fuse = 1.25, Launched = true });
            double shotInterval = weapon.MinInterval > 0 && weapon.RampDuration > 0 ? weapon.Interval - (weapon.Interval - weapon.MinInterval) * Rules.Clamp(p.FireRamp / weapon.RampDuration, 0, 1) : weapon.Interval;
            p.Cooldown = shotInterval * (p.SpeedTime > 0 ? .5 : 1);
            if (weapon.DirectKill) {
                p.Vx = Rules.Clamp(p.Vx - dx * 790, -1050, 1050); p.Vy = Rules.Clamp(p.Vy - dy * 790, -900, 950);
                p.Grounded = false; p.HitStun = .16;
            }
            p.Magazine--;
            if (p.Magazine == 0) {
                if (p.Weapon == 6) DirectDeath(p, "牢张的愤怒弹药耗尽", true);
                else if (weapon.BoltAction) { EquipStarter(p); p.FireRamp = 0; Popup(p, Rules.Weapons[p.Starter].Name + " · 备用"); }
                else BeginReload(p);
            } else if (weapon.BoltAction) p.ReloadTime = weapon.Reload;
        }
        void DirectDeath(Fighter p, string reason, bool shatter = false) {
            if (shatter) State.Shatters.Add(new Shatter { Id = ++nextId, Owner = p.Seat, X = p.X, Y = p.Y });
            p.Lives--; p.Respawns++; p.Respawn = p.Lives > 0 ? 1.1 : 0;
            p.Vx = p.Vy = 0; p.Cooldown = p.ReloadTime = 0; p.ShieldTime = p.JetFuel = p.InvisibleTime = p.WeaponTime = p.MeleeSwing = p.SpeedTime = p.FireRamp = p.SlowTime = 0; p.JetOwned = false;
            Log("成员 " + (p.Seat + 1) + " " + reason + " · 剩余 " + p.Lives + " 命");
        }
        void Throw(Fighter p) {
            if (p.Bombs <= 0 || p.BombCooldown > 0 || State.Grenades.Count >= 8) return;
            double dx, dy; Direction(p, out dx, out dy);
            State.Grenades.Add(new Grenade { Id = ++nextId, Owner = p.Seat, X = p.X + dx * 20, Y = p.Y - 25,
                Vx = dx * 510 + p.Vx * .3, Vy = dy * 350 - 220 });
            p.Bombs--; p.BombCooldown = .7;
        }
        bool Knock(Fighter p, double dx, double dy, double push, double impactPush = -1) {
            if (p.Lives <= 0 || p.Respawn > 0 || p.Invulnerable > 0 || p.ShieldTime > 0) return false;
            double factor = 1 + p.Impact / 120;
            p.Impact = Math.Min(180, p.Impact + (impactPush < 0 ? push : impactPush) / 18);
            p.Vx = Rules.Clamp(p.Vx + dx * push * factor, -1050, 1050);
            p.Vy = Rules.Clamp(p.Vy + dy * push * factor - push * .24, -900, 950);
            p.Grounded = false; p.HitStun = .16; return true;
        }
        void Explode(double x, double y, double radius, double push, bool rocket = false) {
            State.Bursts.Add(new Burst { X = x, Y = y, Radius = radius, Time = rocket ? .55 : .48, Rocket = rocket });
            if (State.Bursts.Count > 16) State.Bursts.RemoveAt(0);
            foreach (var p in State.Players) {
                double dx = p.X - x, dy = p.Y - 24 - y, length = Math.Sqrt(dx * dx + dy * dy);
                if (length < radius) {
                    if (length < 2) { dx = p.Seat == 0 ? -1 : 1; dy = -.3; length = 1; }
                    Knock(p, dx / length, dy / length, push * (.35 + .65 * (1 - length / radius)));
                }
            }
        }
        static bool SegmentHit(double x0, double y0, double x1, double y1, Fighter p) {
            double dx = x1 - x0, dy = y1 - y0;
            double length = dx * dx + dy * dy;
            double t = length < .01 ? 0 : Rules.Clamp(((p.X - x0) * dx + (p.Y - 24 - y0) * dy) / length, 0, 1);
            double x = x0 + t * dx, y = y0 + t * dy;
            return Math.Abs(x - p.X) <= 13 && Math.Abs(y - (p.Y - 24)) <= 25;
        }
        public void Step() {
            if (State.Finished) { if (State.Shatters.Count == 0) return; State.Tick++; StepShatters(); return; }
            State.Tick++;
            StepShatters();
            for (int seat = 0; seat < State.Players.Length; seat++) {
                var p = State.Players[seat];
                p.PopupTime = Math.Max(0, p.PopupTime - Rules.Dt);
                p.Invulnerable = Math.Max(0, p.Invulnerable - Rules.Dt); p.HitStun = Math.Max(0, p.HitStun - Rules.Dt);
                p.Cooldown = Math.Max(0, p.Cooldown - Rules.Dt); p.BombCooldown = Math.Max(0, p.BombCooldown - Rules.Dt);
                p.SlowTime = Math.Max(0, p.SlowTime - Rules.Dt);
                double oldReload = p.ReloadTime; p.ReloadTime = Math.Max(0, p.ReloadTime - Rules.Dt * (p.SpeedTime > 0 ? 2 : 1));
                if (oldReload > 0 && p.ReloadTime == 0) FinishReload(p);
                p.MeleeSwing = Math.Max(0, p.MeleeSwing - Rules.Dt);
                p.ShieldTime = Math.Max(0, p.ShieldTime - Rules.Dt); p.InvisibleTime = Math.Max(0, p.InvisibleTime - Rules.Dt); p.SpeedTime = Math.Max(0, p.SpeedTime - Rules.Dt);
                if ((p.Weapon == 6 || p.Weapon == 8) && p.Respawn <= 0 && p.Lives > 0) {
                    p.WeaponTime = Math.Max(0, p.WeaponTime - Rules.Dt);
                    if (p.WeaponTime <= 0) { DirectDeath(p, p.Weapon == 6 ? "牢张的愤怒倒计时结束" : "坐牢之剑时限结束", true); continue; }
                }
                p.DropTime = Math.Max(0, p.DropTime - Rules.Dt);
                var input = inputs[seat];
                if (p.Respawn > 0 || p.Lives <= 0) {
                    fireHeld[seat] = false;
                    if (input != null) { jump[seat] = input.Jump; bomb[seat] = input.Bomb; drop[seat] = input.Drop; }
                    if (p.Lives > 0) { p.Respawn -= Rules.Dt; if (p.Respawn <= 0) Spawn(p); }
                    continue;
                }
                if (input != null) {
                    p.AimX = input.AimX; p.AimY = input.AimY;
                    bool fresh = State.Tick - inputTicks[seat] <= 30;
                    int move = fresh ? (input.Right ? 1 : 0) - (input.Left ? 1 : 0) : 0;
                    if (p.HitStun <= 0) {
                        double target = move * 275 * (p.SpeedTime > 0 ? 2 : 1) * (p.SlowTime > 0 ? .5 : 1);
                        double approach = (p.Grounded ? 1500 : 1050) * Rules.Dt;
                        if (move != 0) p.Vx += Rules.Clamp(target - p.Vx, -approach, approach);
                        else p.Vx *= Math.Max(0, 1 - (p.Grounded ? 8 : 1.8) * Rules.Dt);
                    }
                    if (fresh && input.Jump > jump[seat] && p.Jumps < 2) { p.Vy = -565; p.Jumps++; p.Grounded = false; }
                    if (fresh && input.Jet && p.JetOwned && p.JetFuel > 0) {
                        p.Vy = Math.Max(-510, p.Vy - 1850 * Rules.Dt); p.JetFuel = Math.Max(0, p.JetFuel - Rules.Dt); p.Grounded = false;
                    }
                    if (fresh && input.Drop > drop[seat] && p.Grounded) { p.DropTime = .22; p.Y += 3; p.Vy = 90; p.Grounded = false; }
                    if (p.Weapon == 11 && fresh && input.Fire) p.FireRamp = Math.Min(Rules.Weapons[11].RampDuration, p.FireRamp + Rules.Dt);
                    else if (p.Weapon == 11) p.FireRamp = 0;
                    if (fresh && input.Fire && (Rules.Weapons[p.Weapon].Automatic || !fireHeld[seat])) Fire(p);
                    if (p.Respawn > 0 || p.Lives <= 0) { fireHeld[seat] = false; continue; }
                    if (fresh && input.Bomb > bomb[seat]) Throw(p);
                    jump[seat] = input.Jump; bomb[seat] = input.Bomb; drop[seat] = input.Drop; fireHeld[seat] = fresh && input.Fire;
                } else { fireHeld[seat] = false; if (p.HitStun <= 0) p.Vx *= .97; }
                double previousY = p.Y;
                p.Vy += Rules.Gravity * Rules.Dt; p.X += p.Vx * Rules.Dt; p.Y += p.Vy * Rules.Dt; p.Grounded = false;
                if (p.Vy >= 0 && p.DropTime <= 0) {
                    var landing = Rules.Arenas[State.Map].Platforms.Where(t => p.X >= t.X - 5 && p.X <= t.X + t.Width + 5 && previousY <= t.Y + .5 && p.Y >= t.Y)
                        .OrderBy(t => t.Y).FirstOrDefault();
                    if (landing != null) { p.Y = landing.Y; p.Vy = 0; p.Grounded = true; p.Jumps = 0; }
                }
            }
            foreach (var s in State.Shots.ToArray()) {
                double ox = s.X, oy = s.Y; s.X += s.Vx * Rules.Dt; s.Y += s.Vy * Rules.Dt; s.Time -= Rules.Dt;
                var enemy = State.Players.Where(target => target.Seat != s.Owner && target.Respawn <= 0 && target.Lives > 0 &&
                    (!s.DirectKill || target.Invulnerable <= 0 && target.ShieldTime <= 0) && SegmentHit(ox, oy, s.X, s.Y, target))
                    .OrderBy(target => (target.X - ox) * (target.X - ox) + (target.Y - oy) * (target.Y - oy)).FirstOrDefault();
                if (enemy != null) {
                    if (s.DirectKill) DirectDeath(enemy, "被克莱博击中", true);
                    else if (s.Rocket) Explode(s.X, s.Y, 190, 880, true);
                    else {
                        double len = Math.Sqrt(s.Vx * s.Vx + s.Vy * s.Vy);
                        if (Knock(enemy, s.Vx / len, s.Vy / len, s.Push, s.ImpactPush > 0 ? s.ImpactPush : -1) && s.SlowTarget) enemy.SlowTime = 2;
                    }
                    State.Shots.Remove(s); continue;
                }
                bool platform = s.Rocket && Rules.Arenas[State.Map].Platforms.Any(t => s.Vy > 0 && oy < t.Y && s.Y >= t.Y && s.X >= t.X && s.X <= t.X + t.Width);
                if (s.Time <= 0 || s.X < -100 || s.X > 1100 || s.Y < -170 || s.Y > 680 || platform) {
                    if (s.Rocket && (s.Time <= 0 || platform)) Explode(s.X, s.Y, 190, 880, true);
                    State.Shots.Remove(s);
                }
            }
            foreach (var g in State.Grenades.ToArray()) {
                double oldY = g.Y; g.Fuse -= Rules.Dt; g.Vy += Rules.Gravity * .75 * Rules.Dt;
                g.X += g.Vx * Rules.Dt; g.Y += g.Vy * Rules.Dt;
                var landing = Rules.Arenas[State.Map].Platforms.Where(t => oldY <= t.Y - 5 && g.Y >= t.Y - 5 && g.Vy > 0 && g.X >= t.X && g.X <= t.X + t.Width).OrderBy(t => t.Y).FirstOrDefault();
                if (landing != null) { g.Y = landing.Y - 5; g.Vy *= -.48; g.Vx *= .72; }
                if (g.Fuse <= 0) { Explode(g.X, g.Y, g.Launched ? 155 : 175, g.Launched ? 730 : 940); State.Grenades.Remove(g); }
                else if (g.X < -150 || g.X > 1150 || g.Y > 760) State.Grenades.Remove(g);
            }
            State.NextCrate -= Rules.Dt;
            if (State.NextCrate <= 0) {
                State.NextCrate = 7 + random.NextDouble() * 3;
                if (State.Crates.Count < 4) DropCrate(150 + random.NextDouble() * 700, RandomAirdropWeapon());
            }
            State.NextBigCrate -= Rules.Dt;
            if (State.NextBigCrate <= 0) {
                State.NextBigCrate = 28 + random.NextDouble() * 8;
                if (State.Crates.Count < 4) DropBigCrate(150 + random.NextDouble() * 700, RandomBigAirdropWeapon());
            }
            foreach (var crate in State.Crates.ToArray()) {
                crate.Age += Rules.Dt; double oldY = crate.Y;
                if (!crate.Landed) {
                    crate.Vy = Math.Min(115, crate.Vy + 220 * Rules.Dt); crate.Y += crate.Vy * Rules.Dt;
                    var landing = Rules.Arenas[State.Map].Platforms.Where(t => crate.X >= t.X && crate.X <= t.X + t.Width && oldY <= t.Y - 10 && crate.Y >= t.Y - 10).OrderBy(t => t.Y).FirstOrDefault();
                    if (landing != null) { crate.Y = landing.Y - 10; crate.Vy = 0; crate.Landed = true; }
                }
                var picker = State.Players.Where(p => p.Lives > 0 && p.Respawn <= 0 && !Rules.LargeDropWeapon(p.Weapon) &&
                    Math.Abs(p.X - crate.X) < 29 && Math.Abs(p.Y - 20 - crate.Y) < 32).OrderBy(p => Math.Abs(p.X - crate.X)).FirstOrDefault();
                if (picker != null) {
                    picker.Weapon = crate.Weapon; picker.Magazine = Rules.Weapons[crate.Weapon].Magazine;
                    picker.Reserve = crate.Weapon < Rules.StarterCount ? -1 : Math.Max(0, Rules.Weapons[crate.Weapon].Ammo - picker.Magazine); picker.ReloadTime = 0;
                    picker.FireRamp = 0; picker.WeaponTime = crate.Weapon == 6 || crate.Weapon == 8 ? 15 : 0;
                    if (crate.Weapon == 8) picker.InvisibleTime = 5;
                    picker.Cooldown = .12; string pickup = String.IsNullOrEmpty(Rules.Weapons[crate.Weapon].Pickup) ? Rules.Weapons[crate.Weapon].Name : Rules.Weapons[crate.Weapon].Pickup;
                    if (crate.Weapon == 8) pickup += " · 隐身5秒";
                    Popup(picker, pickup); State.Crates.Remove(crate);
                    Log("成员 " + (picker.Seat + 1) + " 获得 " + pickup + (pickup == Rules.Weapons[crate.Weapon].Name ? "" : "（" + Rules.Weapons[crate.Weapon].Name + "）"));
                } else if (crate.Y > 720 || crate.Age > 24) State.Crates.Remove(crate);
            }
            State.NextPower -= Rules.Dt;
            if (State.NextPower <= 0) {
                State.NextPower = 8 + random.NextDouble() * 2;
                if (State.Powerups.Count < 3) DropPowerup(150 + random.NextDouble() * 700, random.Next(4));
            }
            foreach (var item in State.Powerups.ToArray()) {
                item.Age += Rules.Dt; double oldY = item.Y;
                if (!item.Landed) {
                    item.Vy = Math.Min(105, item.Vy + 210 * Rules.Dt); item.Y += item.Vy * Rules.Dt;
                    var landing = Rules.Arenas[State.Map].Platforms.Where(t => item.X >= t.X && item.X <= t.X + t.Width && oldY <= t.Y - 10 && item.Y >= t.Y - 10).OrderBy(t => t.Y).FirstOrDefault();
                    if (landing != null) { item.Y = landing.Y - 10; item.Vy = 0; item.Landed = true; }
                }
                var picker = State.Players.Where(p => p.Lives > 0 && p.Respawn <= 0 && Math.Abs(p.X - item.X) < 29 && Math.Abs(p.Y - 20 - item.Y) < 32).OrderBy(p => Math.Abs(p.X - item.X)).FirstOrDefault();
                if (picker != null) {
                    if (item.Kind == 0) { picker.ShieldTime = 6; Popup(picker, "护盾 · 6秒"); }
                    else if (item.Kind == 1) { picker.JetOwned = true; picker.JetFuel = 3; Popup(picker, "火箭飞行器 · 燃料3秒 · 无时限"); }
                    else if (item.Kind == 2) { picker.InvisibleTime = 5; Popup(picker, "隐身装置 · 5秒"); }
                    else { picker.SpeedTime = 5; picker.Popup = "火力全开"; picker.PopupTime = 5; }
                    State.Powerups.Remove(item); Log("成员 " + (picker.Seat + 1) + " 获得 " + (item.Kind == 0 ? "护盾" : item.Kind == 1 ? "火箭飞行器" : item.Kind == 2 ? "隐身装置" : "火力全开"));
                } else if (item.Y > 720 || item.Age > 18) State.Powerups.Remove(item);
            }
            foreach (var burst in State.Bursts.ToArray()) { burst.Time -= Rules.Dt; if (burst.Time <= 0) State.Bursts.Remove(burst); }
            foreach (var p in State.Players) {
                if (p.Respawn <= 0 && p.Lives > 0 && (p.Y > 690 || p.X < -90 || p.X > 1090)) {
                    DirectDeath(p, "掉落");
                }
            }
            var survivors = State.Players.Where(p => p.Lives > 0).ToArray();
            if (survivors.Length == 0) State.Winner = -1;
            else if (survivors.Length == 1) State.Winner = survivors[0].Seat;
            if (State.Finished) Log(State.Winner < 0 ? "本局平局。" : "成员 " + (State.Winner + 1) + " 获胜。");
        }
        void StepShatters() {
            foreach (var shatter in State.Shatters.ToArray()) { shatter.Time -= Rules.Dt; if (shatter.Time <= 0) State.Shatters.Remove(shatter); }
        }
        internal void DropCrate(double x, int weapon) {
            State.Crates.Add(new Crate { Id = ++nextId, X = x, Y = -35, Weapon = weapon, Large = false });
        }
        internal void DropBigCrate(double x, int weapon) {
            State.Crates.Add(new Crate { Id = ++nextId, X = x, Y = -35, Weapon = weapon, Large = true });
        }
        internal int RandomAirdropWeapon() { int[] pool = { 3, 4, 5, 7, 9, 10, 13 }; return pool[random.Next(pool.Length)]; }
        internal int RandomBigAirdropWeapon() { int[] pool = { 6, 8, 11, 12 }; return pool[random.Next(pool.Length)]; }
        internal void DropPowerup(double x, int kind) {
            State.Powerups.Add(new Powerup { Id = ++nextId, X = x, Y = -35, Kind = kind });
        }
    }
    public sealed class Bot {
        int seq, jump, bomb, drop;
        readonly Random random;
        public Bot(int seed) { random = new Random(seed); }
        public InputFrame Think(MatchState state, int seat) {
            var p = state.Players[seat]; var enemy = state.Players.Where(opponent => opponent.Seat != seat && opponent.Lives > 0).OrderBy(opponent => Math.Abs(opponent.X - p.X) + Math.Abs(opponent.Y - p.Y) * .5).FirstOrDefault() ?? state.Players[(seat + 1) % state.Players.Length];
            double target = enemy.X;
            var crate = state.Crates.OrderBy(c => Math.Abs(c.X - p.X) + Math.Abs(c.Y - p.Y) * .4).FirstOrDefault();
            if (crate != null && crate.Landed && (p.Weapon == p.Starter || p.Magazine < 3)) target = crate.X;
            var power = state.Powerups.OrderBy(c => Math.Abs(c.X - p.X) + Math.Abs(c.Y - p.Y) * .4).FirstOrDefault();
            if (power != null && power.Landed && (p.ShieldTime <= 0 && (!p.JetOwned || p.JetFuel <= 0) && p.InvisibleTime <= 0 && p.SpeedTime <= 0)) target = power.X;
            var platforms = Rules.Arenas[state.Map].Platforms;
            bool edge = p.Grounded && !platforms.Any(t => p.X + Math.Sign(target - p.X) * 55 > t.X && p.X + Math.Sign(target - p.X) * 55 < t.X + t.Width && Math.Abs(t.Y - p.Y) < 1);
            bool falling = p.Y > 530 && p.Vy > 0;
            bool climb = enemy.Y < p.Y - 65 || crate != null && crate.Y < p.Y - 65;
            if (state.Tick % 27 == 0 && p.Jumps < 2 && (edge || falling || climb || random.NextDouble() < .12)) jump++;
            if (state.Tick % 120 == 0 && p.Bombs > 0 && Math.Abs(enemy.X - p.X) < 240 && Math.Abs(enemy.Y - p.Y) < 100) bomb++;
            if (state.Tick % 80 == 0 && p.Grounded && enemy.Y > p.Y + 80 && Math.Abs(enemy.X - p.X) < 100) drop++;
            if (p.X < 115) target = 300; if (p.X > 885) target = 700;
            double distance = target - p.X;
            bool move = Math.Abs(distance) > (crate != null && target == crate.X ? 14 : 180);
            double lead = Math.Min(.35, Math.Abs(enemy.X - p.X) / Rules.Weapons[p.Weapon].Speed);
            bool fire = enemy.Lives > 0 && enemy.Respawn <= 0;
            if (!Rules.Weapons[p.Weapon].Automatic) fire = fire && p.Cooldown <= 0 && p.ReloadTime <= 0 && p.Magazine > 0;
            return new InputFrame { Session = state.Session, Seq = ++seq, Jump = jump, Bomb = bomb, Drop = drop,
                Left = move && distance < 0, Right = move && distance > 0, Jet = p.JetFuel > 0 && (falling || climb),
                Fire = fire,
                AimX = Rules.Clamp(enemy.X + enemy.Vx * lead, -200, 1200),
                AimY = Rules.Clamp(enemy.Y - 25 + enemy.Vy * lead, -200, 800) };
        }
    }
}
