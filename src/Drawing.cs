using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace StickMayhem {
    public sealed class ArenaView : FrameworkElement {
        public MatchState State, Previous;
        public int Seat;
        public bool Minimal, Ready, AimVisible;
        public double Blend = 1, AimX = 700, AimY = 350;
        public Brush Ink, Muted, Line, Surface, Accent;
        public event Action<Point> Aimed;
        public event Action<bool> Shooting;
        public event Action Bombed;
        static readonly Typeface Face = new Typeface("Microsoft YaHei UI");
        public ArenaView() {
            Focusable = true; Cursor = Cursors.Cross;
            MouseMove += delegate(object sender, MouseEventArgs e) {
                Point world = ToWorld(e.GetPosition(this)); AimX = world.X; AimY = world.Y; AimVisible = true;
                var action = Aimed; if (action != null) action(world); InvalidateVisual();
            };
            MouseLeave += delegate { if (!IsMouseCaptured) { AimVisible = false; InvalidateVisual(); } };
            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (!Ready) return; Focus(); CaptureMouse();
                Point world = ToWorld(e.GetPosition(this)); AimX = world.X; AimY = world.Y; AimVisible = true;
                var aim = Aimed; if (aim != null) aim(world);
                var action = Shooting; if (action != null) action(true); e.Handled = true;
            };
            MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e) {
                var action = Shooting; if (action != null) action(false); ReleaseMouseCapture(); e.Handled = true;
            };
            MouseRightButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (!Ready) return; Focus(); Point world = ToWorld(e.GetPosition(this)); AimX = world.X; AimY = world.Y; AimVisible = true;
                var aim = Aimed; if (aim != null) aim(world);
                var action = Bombed; if (action != null) action(); e.Handled = true;
            };
            LostMouseCapture += delegate { var action = Shooting; if (action != null) action(false); };
        }
        public Rect StageRect {
            get {
                double scale = Math.Max(.001, Math.Min(Math.Max(1, ActualWidth - 8) / 1000, Math.Max(1, ActualHeight - 8) / 600));
                return new Rect((ActualWidth - 1000 * scale) / 2, (ActualHeight - 600 * scale) / 2, 1000 * scale, 600 * scale);
            }
        }
        public static double PlayerOpacity(int playerSeat, int viewerSeat, double invisibleTime) {
            if (invisibleTime <= 0) return 1;
            return playerSeat == viewerSeat ? .12 : 0;
        }
        public static bool IsOffscreen(double x, double footY) {
            double centerY = footY - 25;
            return x < 0 || x > Rules.Width || centerY < 0 || centerY > Rules.Height;
        }
        public static Point OffscreenPoint(double x, double footY) {
            double dx = x - Rules.Width / 2, dy = footY - 25 - Rules.Height / 2;
            double tx = Math.Abs(dx) < .001 ? Double.PositiveInfinity : (Rules.Width / 2 - 26) / Math.Abs(dx);
            double ty = Math.Abs(dy) < .001 ? Double.PositiveInfinity : (Rules.Height / 2 - 26) / Math.Abs(dy);
            double t = Math.Min(tx, ty);
            return new Point(Rules.Width / 2 + dx * t, Rules.Height / 2 + dy * t);
        }
        public Point ToWorld(Point point) {
            Rect r = StageRect;
            return new Point(Rules.Clamp((point.X - r.Left) * 1000 / r.Width, -250, 1250), Rules.Clamp((point.Y - r.Top) * 600 / r.Height, -220, 820));
        }
        public Point ToScreen(Point world) { Rect r = StageRect; return new Point(r.Left + world.X * r.Width / 1000, r.Top + world.Y * r.Height / 600); }
        void Text(DrawingContext dc, string text, double x, double y, double size, Brush color, bool center) {
            #pragma warning disable 618
            var f = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, color);
            #pragma warning restore 618
            dc.DrawText(f, new Point(center ? x - f.Width / 2 : x, y));
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc); if (Ink == null || ActualWidth <= 1 || ActualHeight <= 1) return;
            dc.DrawRectangle(Brushes.Transparent, new Pen(Line, 1), new Rect(.5, .5, ActualWidth - 1, ActualHeight - 1));
            if (State == null) return;
            Rect r = StageRect; double scale = r.Width / 1000;
            dc.PushClip(new RectangleGeometry(new Rect(1, 1, ActualWidth - 2, ActualHeight - 2)));
            dc.PushTransform(new TranslateTransform(r.Left, r.Top)); dc.PushTransform(new ScaleTransform(scale, scale));
            var thin = new Pen(Line, .65 / scale); var pen = new Pen(Ink, 1.25 / scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            for (int x = 100; x < 1000; x += 100) dc.DrawLine(thin, new Point(x, 30), new Point(x, 570));
            foreach (var platform in Rules.Arenas[State.Map].Platforms) {
                dc.DrawRectangle(Surface, null, new Rect(platform.X, platform.Y, platform.Width, 10));
                dc.DrawLine(new Pen(Muted, 1.2 / scale), new Point(platform.X, platform.Y), new Point(platform.X + platform.Width, platform.Y));
            }
            foreach (var crate in State.Crates) {
                double size = Math.Max(crate.Large ? 38 : 22, (crate.Large ? 12 : 8) / scale);
                var cp = new Pen(crate.Large ? Accent : Muted, (crate.Large ? 1.8 : 1) / scale);
                dc.DrawRectangle(Surface, cp, new Rect(crate.X - size / 2, crate.Y - size / 2, size, size));
                dc.DrawLine(thin, new Point(crate.X - size / 2, crate.Y - size / 2), new Point(crate.X + size / 2, crate.Y + size / 2));
                dc.DrawLine(thin, new Point(crate.X + size / 2, crate.Y - size / 2), new Point(crate.X - size / 2, crate.Y + size / 2));
                if (crate.Large) {
                    dc.DrawRectangle(null, cp, new Rect(crate.X - size / 2 + 4 / scale, crate.Y - size / 2 + 4 / scale, size - 8 / scale, size - 8 / scale));
                    Text(dc, "大", crate.X, crate.Y - 6, 10 / scale, Accent, true);
                }
                if (!crate.Landed) {
                    double parachute = crate.Large ? 48 : 36;
                    dc.DrawLine(cp, new Point(crate.X - parachute, crate.Y - 39), new Point(crate.X + parachute, crate.Y - 39));
                    dc.DrawLine(cp, new Point(crate.X - parachute, crate.Y - 39), new Point(crate.X, crate.Y - size / 2));
                    dc.DrawLine(cp, new Point(crate.X + parachute, crate.Y - 39), new Point(crate.X, crate.Y - size / 2));
                    dc.DrawEllipse(null, cp, new Point(crate.X, crate.Y - 42), parachute, 10);
                }
            }
            foreach (var item in State.Powerups) {
                double size = Math.Max(22, 8 / scale); Brush powerInk = item.Kind == 3 ? new SolidColorBrush(Color.FromRgb(35, 151, 255)) : item.Kind == 0 ? Accent : item.Kind == 1 ? Muted : Ink;
                var pp = new Pen(powerInk, 1.2 / scale);
                dc.DrawEllipse(Surface, pp, new Point(item.X, item.Y), size / 2, size / 2);
                Text(dc, item.Kind == 0 ? "盾" : item.Kind == 1 ? "火" : item.Kind == 2 ? "隐" : "速", item.X, item.Y - 7, 11 / scale, powerInk, true);
                if (!item.Landed) {
                    dc.DrawEllipse(null, pp, new Point(item.X, item.Y - 42), 33, 9);
                    dc.DrawLine(pp, new Point(item.X - 33, item.Y - 40), new Point(item.X, item.Y - 11));
                    dc.DrawLine(pp, new Point(item.X + 33, item.Y - 40), new Point(item.X, item.Y - 11));
                }
            }
            foreach (var shot in State.Shots) {
                double length = Math.Sqrt(shot.Vx * shot.Vx + shot.Vy * shot.Vy);
                if (length < 1) continue;
                Brush shotColor = shot.Energy ? new SolidColorBrush(Color.FromRgb(46, 174, 255)) : shot.Owner == Seat ? Accent : Muted;
                double trail = shot.Rocket || shot.DirectKill ? 18 : shot.Energy ? 13 : 9;
                dc.DrawLine(new Pen(shotColor, shot.Rocket ? 2.5 / scale : shot.Energy || shot.DirectKill ? 2 / scale : 1 / scale),
                    new Point(shot.X, shot.Y), new Point(shot.X - shot.Vx / length * trail, shot.Y - shot.Vy / length * trail));
                if (shot.Energy) dc.DrawEllipse(shotColor, null, new Point(shot.X, shot.Y), 2.4 / scale, 2.4 / scale);
            }
            foreach (var bomb in State.Grenades) {
                Brush bombInk = bomb.Launched ? Accent : Ink;
                double radius = (bomb.Launched ? 6 : 4) / scale;
                dc.DrawEllipse(bomb.Launched ? Surface : Ink, new Pen(bombInk, 1 / scale), new Point(bomb.X, bomb.Y), radius, radius);
                if (bomb.Launched) dc.DrawLine(new Pen(bombInk, 1.2 / scale), new Point(bomb.X, bomb.Y), new Point(bomb.X - bomb.Vx * .018, bomb.Y - bomb.Vy * .018));
            }
            foreach (var burst in State.Bursts) {
                double duration = burst.Rocket ? .55 : .48;
                double progress = Rules.Clamp(1 - burst.Time / duration, 0, 1);
                double fade = 1 - progress;
                dc.PushOpacity(fade * (burst.Rocket ? .68 : .48));
                double radius = burst.Radius * (.22 + .78 * progress);
                Brush flash = burst.Rocket ? new SolidColorBrush(Color.FromArgb(180, 255, 105, 35)) : new SolidColorBrush(Color.FromArgb(110, 255, 170, 55));
                Brush core = burst.Rocket ? new SolidColorBrush(Color.FromArgb(150, 255, 215, 115)) : new SolidColorBrush(Color.FromArgb(75, 255, 225, 130));
                dc.DrawEllipse(core, null, new Point(burst.X, burst.Y), radius * .31, radius * .31);
                dc.DrawEllipse(null, new Pen(flash, (burst.Rocket ? 5 : 2.5) / scale), new Point(burst.X, burst.Y), radius, radius * .78);
                dc.DrawEllipse(null, new Pen(Muted, 1 / scale), new Point(burst.X, burst.Y), radius * .68, radius * .53);
                if (burst.Rocket) for (int ray = 0; ray < 10; ray++) {
                    double a = ray * Math.PI / 5 + progress * .16;
                    double inner = radius * .52, outer = radius * (.83 + .12 * ((ray % 2) == 0 ? 1 : 0));
                    dc.DrawLine(new Pen(flash, 2 / scale), new Point(burst.X + Math.Cos(a) * inner, burst.Y + Math.Sin(a) * inner),
                        new Point(burst.X + Math.Cos(a) * outer, burst.Y + Math.Sin(a) * outer));
                }
                dc.Pop();
            }
            foreach (var p in State.Players) {
                if (p.Lives <= 0 || p.Respawn > 0) continue;
                double opacity = PlayerOpacity(p.Seat, Seat, p.InvisibleTime);
                if (opacity <= 0 || IsOffscreen(p.X, p.Y)) continue;
                double x = p.X, y = p.Y;
                if (Previous != null && Blend < 1) {
                    var before = Previous.Players[p.Seat];
                    if (before.Respawns == p.Respawns && before.Respawn <= 0) {
                        x = before.X + (x - before.X) * Blend; y = before.Y + (y - before.Y) * Blend;
                    }
                }
                Brush color = p.Seat == Seat ? Accent : Ink;
                var fp = new Pen(color, 1.4 / scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                if (opacity < 1) dc.PushOpacity(opacity);
                if (p.Invulnerable > 0) dc.PushOpacity(State.Tick % 14 < 7 ? .5 : .9);
                dc.DrawEllipse(null, fp, new Point(x, y - 39), 7, 7);
                dc.DrawLine(fp, new Point(x, y - 31), new Point(x, y - 14));
                double gait = Math.Abs(p.Vx) > 40 && p.Grounded ? Math.Sin(State.Seconds * 18) * 6 : 0;
                dc.DrawLine(fp, new Point(x, y - 14), new Point(x - 8 - gait, y));
                dc.DrawLine(fp, new Point(x, y - 14), new Point(x + 8 + gait, y));
                double aimX = Ready && p.Seat == Seat && AimVisible ? AimX : p.AimX;
                double aimY = Ready && p.Seat == Seat && AimVisible ? AimY : p.AimY;
                double dx = aimX - x, dy = aimY - (y - 25), length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 1) { dx = p.Seat == 0 ? 1 : -1; dy = 0; length = 1; } dx /= length; dy /= length;
                Point hand = new Point(x + dx * 15, y - 25 + dy * 15);
                dc.DrawLine(fp, new Point(x, y - 27), hand);
                dc.DrawLine(new Pen(color, 2 / scale), hand, new Point(x + dx * 31, y - 25 + dy * 31));
                if (p.Weapon == 8 && p.MeleeSwing > 0) {
                    double swing = 72 + 10 * Math.Sin((.28 - p.MeleeSwing) / .28 * Math.PI);
                    Point tip = new Point(x + dx * swing, y - 25 + dy * swing);
                    dc.DrawLine(new Pen(color, 3 / scale), hand, tip);
                    dc.DrawLine(new Pen(Muted, 2 / scale), new Point(hand.X - dy * 6, hand.Y + dx * 6), new Point(hand.X + dy * 6, hand.Y - dx * 6));
                }
                if (p.JetOwned) {
                    dc.DrawRectangle(Surface, fp, new Rect(x - 10, y - 28, 5, 14));
                    if (p.JetFuel > 0) {
                        double flame = 5 + (State.Tick % 4) * 2;
                        dc.DrawLine(new Pen(Muted, 2 / scale), new Point(x - 8, y - 13), new Point(x - 8, y - 13 + flame));
                    }
                }
                if (p.ShieldTime > 0) dc.DrawEllipse(null, new Pen(Accent, 1.4 / scale), new Point(x, y - 25), 27, 33);
                if (p.Invulnerable > 0) {
                    dc.DrawEllipse(null, thin, new Point(x, y - 24), 26, 31); dc.Pop();
                }
                Text(dc, (p.Seat + 1) + "号", x, y + 6, (Minimal ? 8 : 9) / scale, p.Seat == Seat ? Accent : Muted, true);
                Text(dc, ((int)p.Impact).ToString() + "%", Rules.Clamp(x, 95, 905), Math.Max(4, y - 58), (Minimal ? 9 : 10) / scale,
                    p.Impact > 0 ? color : Muted, true);
                double reloadDuration = Rules.Weapons[p.Weapon].Reload;
                if (p.ReloadTime > 0 && reloadDuration > 0) {
                    double cx = x, cy = y - 76, radius = 7.5, progress = Rules.Clamp(1 - p.ReloadTime / reloadDuration, 0, 1);
                    dc.DrawEllipse(null, new Pen(Muted, 1.4 / scale), new Point(cx, cy), radius, radius);
                    if (progress >= .999) dc.DrawEllipse(null, new Pen(Accent, 2.4 / scale), new Point(cx, cy), radius, radius);
                    else if (progress > 0) {
                        double angle = progress * Math.PI * 2;
                        Point end = new Point(cx + Math.Sin(angle) * radius, cy - Math.Cos(angle) * radius);
                        var arc = new StreamGeometry();
                        using (var geometry = arc.Open()) {
                            geometry.BeginFigure(new Point(cx, cy - radius), false, false);
                            geometry.ArcTo(end, new Size(radius, radius), 0, progress > .5, SweepDirection.Clockwise, true, false);
                        }
                        dc.DrawGeometry(null, new Pen(Accent, 2.4 / scale), arc);
                    }
                }
                if (p.PopupTime > 0) {
                    bool speedPopup = p.Popup == "火力全开" && p.SpeedTime > 0;
                    double opacityPopup = speedPopup ? (State.Tick % 18 < 12 ? Rules.Clamp(p.SpeedTime / .65, .3, 1) : .25) : Match.PopupOpacity(p);
                    dc.PushOpacity(opacityPopup);
                    string popupText = speedPopup ? p.Popup + "  " + Math.Ceiling(p.SpeedTime).ToString("0") + "s" : p.Weapon == 12 ? p.Popup + " · " + p.Magazine + "发" : p.Popup;
                    double popupY = Math.Max(4, y - (p.Weapon == 6 || p.Weapon == 8 || p.Weapon == 11 || p.Weapon == 12 ? 100 : 82) - Match.PopupRise(p));
                    if (speedPopup) Text(dc, popupText, Rules.Clamp(x + 1, 95, 905), popupY + 1, (Minimal ? 10 : 12) / scale, Brushes.Black, true);
                    Text(dc, popupText, Rules.Clamp(x, 95, 905), popupY, (Minimal ? 10 : 12) / scale, speedPopup ? Brushes.White : Ink, true); dc.Pop();
                }
                if ((p.Weapon == 6 || p.Weapon == 8) && p.WeaponTime > 0 && p.PopupTime <= 0) {
                    string weaponLabel = p.Weapon == 6 ? "牢张的愤怒" : "坐牢之剑";
                    Text(dc, weaponLabel + " · " + Math.Ceiling(p.WeaponTime).ToString("0") + "s", Rules.Clamp(x, 95, 905), Math.Max(4, y - 98), (Minimal ? 8 : 10) / scale, Ink, true);
                }
                if (p.Weapon == 12 && p.PopupTime <= 0)
                    Text(dc, "克莱博 · 剩余 " + p.Magazine + " 发", Rules.Clamp(x, 95, 905), Math.Max(4, y - 98), (Minimal ? 8 : 10) / scale, Ink, true);
                if (opacity < 1) dc.Pop();
            }
            foreach (var shatter in State.Shatters) DrawShatter(dc, shatter, scale);
            foreach (var p in State.Players) {
                if (p.Lives <= 0 || p.Respawn > 0 || !IsOffscreen(p.X, p.Y)) continue;
                double opacity = PlayerOpacity(p.Seat, Seat, p.InvisibleTime);
                if (opacity <= 0) continue;
                if (opacity < 1) dc.PushOpacity(opacity);
                Point tip = OffscreenPoint(p.X, p.Y);
                double dx = p.X - Rules.Width / 2, dy = p.Y - 25 - Rules.Height / 2;
                double length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); dx /= length; dy /= length;
                double size = 15, half = 7;
                Point back = new Point(tip.X - dx * size, tip.Y - dy * size);
                Point left = new Point(back.X - dy * half, back.Y + dx * half);
                Point right = new Point(back.X + dy * half, back.Y - dx * half);
                var arrow = new StreamGeometry();
                using (var geometry = arrow.Open()) { geometry.BeginFigure(tip, true, true); geometry.LineTo(left, true, false); geometry.LineTo(right, true, false); }
                Brush color = p.Seat == Seat ? Accent : Ink;
                dc.DrawGeometry(color, null, arrow);
                Text(dc, (p.Seat + 1) + "号", back.X - dx * 5, back.Y - dy * 5, (Minimal ? 8 : 9) / scale, color, true);
                if (opacity < 1) dc.Pop();
            }
            if (Ready && AimVisible) {
                double cross = 5 / scale;
                dc.DrawEllipse(null, new Pen(Muted, .8 / scale), new Point(AimX, AimY), cross, cross);
                dc.DrawLine(thin, new Point(AimX - cross - 3 / scale, AimY), new Point(AimX + cross + 3 / scale, AimY));
                dc.DrawLine(thin, new Point(AimX, AimY - cross - 3 / scale), new Point(AimX, AimY + cross + 3 / scale));
            }
            dc.Pop(); dc.Pop(); dc.Pop();
            if (State.Finished) {
                string title = State.Winner < 0 ? "平局" : State.Winner == Seat ? "本局获胜" : "本局结束";
                Text(dc, title, ActualWidth / 2, 14, Minimal ? 12 : 16, Ink, true);
            }
        }
        void FragmentLine(DrawingContext dc, Pen pen, double x, double y, double ax, double ay, double bx, double by, double vx, double vy, double t) {
            double fall = 530 * t * t / 2;
            dc.DrawLine(pen, new Point(x + ax + vx * t, y + ay + vy * t + fall),
                new Point(x + bx + vx * t, y + by + vy * t + fall));
        }
        void DrawShatter(DrawingContext dc, Shatter shatter, double scale) {
            double elapsed = .85 - shatter.Time, opacity = Rules.Clamp(shatter.Time / .85, 0, 1);
            if (opacity <= 0) return;
            Brush color = shatter.Owner == Seat ? Accent : Ink;
            var piece = new Pen(color, 1.8 / scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.PushOpacity(opacity);
            double hx = shatter.X - 72 * elapsed, hy = shatter.Y - 39 - 185 * elapsed + 265 * elapsed * elapsed;
            dc.DrawEllipse(null, piece, new Point(hx, hy), 4.5, 4.5);
            FragmentLine(dc, piece, shatter.X, shatter.Y, 0, -31, 0, -14, 28, -148, elapsed);
            FragmentLine(dc, piece, shatter.X, shatter.Y, 0, -28, -14, -23, -155, -185, elapsed);
            FragmentLine(dc, piece, shatter.X, shatter.Y, 0, -28, 14, -23, 155, -185, elapsed);
            FragmentLine(dc, piece, shatter.X, shatter.Y, -14, -23, -22, -13, -205, -85, elapsed);
            FragmentLine(dc, piece, shatter.X, shatter.Y, 14, -23, 22, -13, 205, -85, elapsed);
            FragmentLine(dc, piece, shatter.X, shatter.Y, 0, -14, -8, 0, -105, -45, elapsed);
            FragmentLine(dc, piece, shatter.X, shatter.Y, 0, -14, 8, 0, 105, -45, elapsed);
            dc.Pop();
        }
    }
}
