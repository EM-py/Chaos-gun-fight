using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace StickMayhem {
    public sealed class PanelWindow : Window {
        internal readonly bool VerificationMode;
        readonly string root = AppDomain.CurrentDomain.BaseDirectory;
        internal ArenaView Field;
        Grid body, lobby, live, settings, help, records;
        TextBox address, port;
        TextBlock lobbyStatus, status, ownHud, enemyHud, timerHud, hint, recordText;
        Button hostButton, joinButton, leaveButton, resumeButton, copyButton, addressButton, mapButton, livesButton;
        Button[] starters = new Button[Rules.StarterCount];
        string theme = "codex", appearance = "dark", page = "lobby", fingerprint;
        bool minimal, host, practice, playing, stopped;
        int seat, starter, map, lives = 5, inputSeq, jumpSerial, bombSerial, dropSerial, visualTick;
        double aimX = 750, aimY = 450, lastTime, accumulated, receivedAt;
        bool firing;
        MatchOptions hostOptions;
        readonly List<MatchOptions> roomOptions = new List<MatchOptions>();
        readonly Dictionary<int, int> roomSeats = new Dictionary<int, int>();
        string[] addresses = new string[0];
        readonly HashSet<Key> pressed = new HashSet<Key>();
        Peer peer;
        internal Match Engine;
        internal MatchState Current;
        MatchState previous;
        Bot bot;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly DispatcherTimer timer;
        Brush ink, muted, line, surface, back, accent;
        internal bool IsPlaying { get { return playing && !stopped; } }
        internal bool IsMinimal { get { return minimal; } }
        internal int Seat { get { return seat; } }
        internal int RoomCount { get { return roomOptions.Count; } }
        internal string DebugState { get { return "playing=" + playing + ", stopped=" + stopped + ", finished=" + (Current != null && Current.Finished) + ", status=" + (lobbyStatus == null ? "" : lobbyStatus.Text); } }
        internal int ListeningPort { get { return peer == null ? 0 : peer.ListeningPort; } }

        public PanelWindow(bool verification) {
            VerificationMode = verification;
            Title = "协作面板"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 11; FontWeight = FontWeights.Normal; UseLayoutRounding = true; SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            if (!verification) LoadPreferences(); Build();
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
            timer.Tick += delegate { Tick(); }; timer.Start();
            PreviewKeyDown += KeyDownInput; PreviewKeyUp += KeyUpInput;
            Deactivated += delegate { ClearInput(); };
            Closed += delegate { timer.Stop(); if (peer != null) peer.Dispose(); };
        }
        Brush B(string color) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
        void Palette() {
            bool dark = theme == "codex" && appearance == "dark";
            back = B(dark ? "#0D1117" : "#FFFFFF"); ink = B(dark ? "#E6EDF3" : "#34383D");
            muted = B(dark ? "#8C96A3" : "#858C95"); line = B(dark ? "#30363D" : "#E2E6EB");
            surface = B(dark ? "#161B22" : "#F5F6F8"); accent = B(dark ? "#BBC7D2" : theme == "wps" ? "#597D69" : "#536575");
        }
        TextBlock T(string text, double size, bool gray) {
            return new TextBlock { Text = text, FontSize = size, Foreground = gray ? muted : ink, FontWeight = FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
        }
        Button Btn(string text, Action action, bool selected) {
            var b = new Button { Content = text, Height = minimal ? 23 : 26, FontSize = minimal ? 10 : 11, FontWeight = FontWeights.Normal,
                Foreground = ink, Background = selected ? surface : Brushes.Transparent, BorderBrush = line, BorderThickness = new Thickness(selected ? 1 : 0),
                Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(1, 0, 1, 0), Cursor = Cursors.Hand };
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border)); border.Name = "Surface";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(3, 0, 3, 0)); border.AppendChild(content); template.VisualTree = border;
            var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, line, "Surface")); template.Triggers.Add(hover);
            var disabled = new Trigger { Property = Button.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .4)); template.Triggers.Add(disabled); b.Template = template;
            if (action != null) b.Click += delegate { action(); }; return b;
        }
        TextBox Box(string text) { return new TextBox { Text = text, Height = 25, FontSize = 10, FontWeight = FontWeights.Normal,
            Foreground = ink, Background = back, BorderBrush = line, BorderThickness = new Thickness(1), Padding = new Thickness(4, 2, 4, 2), VerticalContentAlignment = VerticalAlignment.Center }; }
        static Grid Rows(params double[] heights) {
            var grid = new Grid(); foreach (double h in heights) grid.RowDefinitions.Add(new RowDefinition { Height = h < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(h) }); return grid;
        }
        static void Put(Grid g, UIElement e, int row, int col) { Grid.SetRow(e, row); Grid.SetColumn(e, col); g.Children.Add(e); }
        internal void Configure(string nextTheme, bool nextMinimal, string nextAppearance) {
            ClearInput(); theme = nextTheme; minimal = nextMinimal; appearance = nextAppearance; Build(); SavePreferences();
        }
        internal void ChooseStarter(int value) { if (value < 0 || value >= Rules.StarterCount || peer != null) return; starter = value; SavePreferences(); Refresh(); }
        void Build() {
            string addr = address == null ? "" : address.Text, p = port == null ? Wire.Port.ToString() : port.Text;
            string message = lobbyStatus == null ? "选择自己的开局武器。同一局域网，最多 6 人加入房间。" : lobbyStatus.Text;
            Palette(); MinWidth = MinHeight = 0; MaxWidth = MaxHeight = Double.PositiveInfinity;
            Width = minimal ? 392 : 548; Height = minimal ? 300 : 474;
            MinWidth = MaxWidth = Width; MinHeight = MaxHeight = Height; Background = back; Foreground = ink;
            var outer = new Border { BorderBrush = line, BorderThickness = new Thickness(1), Background = back }; Content = outer;
            var shell = Rows(minimal ? 25 : 28, minimal ? 23 : 26, -1, 18); outer.Child = shell;
            var title = new Grid { Background = surface }; title.ColumnDefinitions.Add(new ColumnDefinition());
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            var label = T("▣  协作面板", 11, false); label.Margin = new Thickness(8, 0, 0, 0); title.Children.Add(label);
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.OriginalSource == label || e.OriginalSource == title) DragMove(); };
            Put(title, Btn("—", delegate { ClearInput(); WindowState = WindowState.Minimized; }, false), 0, 1); Put(title, Btn("×", Close, false), 0, 2); Put(shell, title, 0, 0);
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
            tabs.Children.Add(Btn("项目", delegate { ShowPage(Current == null ? "lobby" : "live"); }, false));
            tabs.Children.Add(Btn("连接", delegate { ShowPage("lobby"); }, false)); tabs.Children.Add(Btn("记录", delegate { ShowPage("records"); }, false));
            tabs.Children.Add(Btn("设置", delegate { ShowPage("settings"); }, false)); tabs.Children.Add(Btn("说明", delegate { ShowPage("help"); }, false));
            var test = Btn("test", StartPractice, false); test.FontSize = 8; test.ToolTip = "本地火柴人练习（电脑对手）"; tabs.Children.Add(test);
            Put(shell, new Border { BorderBrush = line, BorderThickness = new Thickness(0, 0, 0, 1), Child = tabs }, 1, 0);
            body = new Grid { Margin = new Thickness(minimal ? 6 : 8, 4, minimal ? 6 : 8, 3) }; Put(shell, body, 2, 0);
            BuildLobby(addr, p, message); BuildLive(); BuildSettings(); BuildHelp(); BuildRecords();
            var footer = new Grid { Background = surface };
            status = T("", 9, true); status.Margin = new Thickness(8, 0, 0, 0); footer.Children.Add(status);
            var quiet = T("静音  100%", 9, true); quiet.HorizontalAlignment = HorizontalAlignment.Right; quiet.Margin = new Thickness(0, 0, 8, 0); footer.Children.Add(quiet);
            Put(shell, footer, 3, 0); ShowPage(page); Refresh();
        }
        void BuildLobby(string addr, string portText, string message) {
            lobby = Rows(minimal ? 16 : 23, minimal ? 16 : 22, minimal ? 47 : 62, minimal ? 25 : 30, 18, 26, -1, 26); body.Children.Add(lobby);
            Put(lobby, T("火柴人协作 · 平台竞技", minimal ? 10 : 12, false), 0, 0); Put(lobby, T("我的开局武器 · 无限弹药备用", 9, true), 1, 0);
            var weapons = new UniformGrid { Rows = 1, Columns = 3 };
            for (int i = 0; i < Rules.StarterCount; i++) { int index = i; var weapon = Rules.Weapons[i];
                starters[i] = Btn(weapon.Name, delegate { ChooseStarter(index); }, i == starter);
                starters[i].ToolTip = weapon.Name + " · " + weapon.Magazine + " 发弹匣 · 换弹 " + weapon.Reload.ToString("0.##") + " 秒" + (weapon.Automatic ? " · 可连发" : " · 单发"); weapons.Children.Add(starters[i]); }
            Put(lobby, weapons, 2, 0);
            var config = new Grid(); config.ColumnDefinitions.Add(new ColumnDefinition()); config.ColumnDefinitions.Add(new ColumnDefinition());
            mapButton = Btn("地图：" + Rules.Arenas[map].Name, delegate { map = (map + 1) % Rules.Arenas.Length; SavePreferences(); Refresh(); }, false);
            mapButton.ToolTip = "点击切换地图，联机以房主选择为准"; Put(config, mapButton, 0, 0);
            livesButton = Btn("生命：" + lives, delegate { lives = lives == 3 ? 5 : lives == 5 ? 7 : lives == 7 ? 10 : 3; SavePreferences(); Refresh(); }, false);
            livesButton.ToolTip = "点击切换 3 / 5 / 7 / 10 命，联机以房主选择为准"; Put(config, livesButton, 0, 1); Put(lobby, config, 3, 0);
            Put(lobby, T("房主地址 · 右侧为端口", 9, true), 4, 0);
            var entry = new Grid(); entry.ColumnDefinitions.Add(new ColumnDefinition());
            foreach (double width in new[] { 23.0, 36.0, 10.0, 57.0 }) entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
            address = Box(addr); address.ToolTip = "完整 IP:端口，可以选择后 Ctrl+C"; Put(entry, address, 0, 0);
            addressButton = Btn("▾", AddressMenu, false); Put(entry, addressButton, 0, 1); copyButton = Btn("复制", CopyAddress, false); Put(entry, copyButton, 0, 2);
            port = Box(portText); Put(entry, port, 0, 4); Put(lobby, entry, 5, 0);
            lobbyStatus = T(message, minimal ? 9 : 10, true); lobbyStatus.TextWrapping = TextWrapping.Wrap;
            lobbyStatus.VerticalAlignment = VerticalAlignment.Top; lobbyStatus.Margin = new Thickness(0, 5, 0, 0); Put(lobby, lobbyStatus, 6, 0);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            leaveButton = Btn("离开", Leave, false); resumeButton = Btn("返回项目", delegate { ShowPage("live"); }, false);
            hostButton = Btn("创建房间", delegate { if (host && peer != null) StartRoom(); else StartHost(ParsePort()); }, false); joinButton = Btn("加入", JoinUi, false);
            foreach (var button in new[] { leaveButton, resumeButton, hostButton, joinButton }) buttons.Children.Add(button); Put(lobby, buttons, 7, 0);
        }
        void BuildLive() {
            live = Rows(minimal ? 29 : 37, -1, minimal ? 19 : 24, minimal ? 18 : 24); body.Children.Add(live);
            var header = Rows(minimal ? 15 : 19, minimal ? 14 : 18);
            ownHud = T("", minimal ? 9 : 11, false); enemyHud = T("", minimal ? 9 : 10, true);
            Put(header, ownHud, 0, 0); Put(header, enemyHud, 1, 0); Put(live, header, 0, 0);
            Field = new ArenaView { Ink = ink, Muted = muted, Line = line, Surface = surface, Accent = accent, Minimal = minimal };
            Field.Aimed += point => { aimX = point.X; aimY = point.Y; };
            Field.Shooting += value => { firing = value; SendInput(); };
            Field.Bombed += delegate { bombSerial++; SendInput(); }; Put(live, Field, 1, 0);
            var meta = new Grid(); timerHud = T("", 9, true); meta.Children.Add(timerHud);
            var backButton = Btn("房间", delegate { ShowPage("lobby"); }, false); backButton.HorizontalAlignment = HorizontalAlignment.Right; backButton.Height = minimal ? 19 : 24; backButton.FontSize = 9; meta.Children.Add(backButton); Put(live, meta, 2, 0);
            hint = T("A/D 移动 · W 跳跃 · 空格跳/飞行 · 左键射击 · 右键炸弹", minimal ? 8 : 9, true); Put(live, hint, 3, 0);
        }
        StackPanel Options(string caption, string[] values, int selected, Action<int> action) {
            var stack = new StackPanel { Margin = new Thickness(0, 2, 0, 5) }; stack.Children.Add(T(caption, 10, true));
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            for (int i = 0; i < values.Length; i++) { int n = i; var b = Btn(values[i], delegate { action(n); }, selected == i); b.MinWidth = 66; row.Children.Add(b); }
            stack.Children.Add(row); return stack;
        }
        void BuildSettings() {
            settings = new Grid(); body.Children.Add(settings); var stack = new StackPanel(); settings.Children.Add(stack);
            stack.Children.Add(Options("工具风格", new[] { "WPS", "Codex" }, theme == "wps" ? 0 : 1, i => Configure(i == 0 ? "wps" : "codex", minimal, appearance)));
            stack.Children.Add(Options("窗口尺寸", new[] { "普通", "极简" }, minimal ? 1 : 0, i => Configure(theme, i == 1, appearance)));
            var colors = Options("Codex 外观", new[] { "浅色", "深色" }, appearance == "light" ? 0 : 1, i => Configure(theme, minimal, i == 0 ? "light" : "dark")); colors.IsEnabled = theme == "codex"; stack.Children.Add(colors);
            stack.Children.Add(T("普通 548×474 / 极简 392×300 · Esc 最小化\n开局武器、地图与生命数在连接页选择", 9, true));
        }
        void BuildHelp() {
            help = new Grid(); body.Children.Add(help);
            var text = T("火柴人平台竞技\n\n用枪和爆炸物把对手击退出场外，掉落扣一命，最后存活者获胜。\n\n鼠标瞄准；左键射击/挥剑，右键炸弹。A / D 移动，W / ↑ 跳跃；空格也能跳跃，获得火箭飞行器后长按空格飞行。S / ↓ 穿过平台。\n\n开局只选手枪，均为无限备弹但需要换弹：左轮 6 发、换弹 3 秒且冲击增强；USP 12 发、换弹 2 秒；格洛克 20 发、换弹 1.5 秒且可连发。换弹时角色头顶圆环显示进度。\n\n普通武器箱约每 7–10 秒掉落霰弹枪、狙击枪、火箭筒、榴弹发射器、突击步枪或冲锋枪。突击步枪 30 发弹匣另有 1 个备用弹匣；普通冲锋枪同为 30 发、一个备用弹匣，耗尽后切回开局手枪。火箭筒爆炸范围和击退增强并有火焰冲击特效。手投炸弹范围和冲击力增强。\n\n大空投箱约每 28–36 秒掉落加特林“牢张的愤怒”、近战“坐牢之剑”、电能冲锋枪或克莱博。前两把限时 15 秒；电能冲锋枪弹容 45 发，按住开火会逐渐加速，蓝色子弹；克莱博 5 发，每发后需要 1 秒重新装填，击中直接碎裂击杀，开火反冲会把自己向后震飞。四把都不能被其他武器箱替换；弹药打空后切回开局手枪。\n\n增益补给每 8–10 秒随机刷新护盾、火箭飞行器、隐身装置或速度道具。护盾持续 6 秒；火箭飞行器没有持有时限，但燃料为 3 秒，长按空格消耗；隐身持续 5 秒，自己半透明，对手视角消失；速度持续 5 秒，头顶“火力全开”闪烁并加快移动。\n\n每条命有 3 枚炸弹，爆炸可以把自己轰飞。命中累积冲击值，越高越容易被打飞；重生清零并获得 1.8 秒保护。最后一条命复活显示“背水一战”，随后淡出。\n\n最多 6 人局域网联机，房主为 1 号，加入者依次编号 2–6；至少 2 人加入后房主点“开始对战”。六张地图各有六个出生点，地图与生命数由房主决定，默认端口 42673。房主统一计算物理、命中、换弹、补给、击退与胜负。断线后停止，离开再重建。\n\n顶栏 test 与电脑练习。切页、失去焦点和最小化会释放输入，对局继续。设置保存在 preferences.ini，对局不保存。", 10, true);
            text.TextWrapping = TextWrapping.Wrap; text.VerticalAlignment = VerticalAlignment.Top;
            help.Children.Add(new ScrollViewer { Content = text, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        }
        void BuildRecords() {
            records = Rows(22, -1); body.Children.Add(records); Put(records, T("项目记录", 11, false), 0, 0);
            recordText = T("尚未开始。", 10, true); recordText.TextWrapping = TextWrapping.Wrap; recordText.VerticalAlignment = VerticalAlignment.Top;
            Put(records, new ScrollViewer { Content = recordText, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 1, 0);
        }
        internal void ShowPage(string value) {
            if (value != "live") ClearInput(); page = value;
            lobby.Visibility = value == "lobby" ? Visibility.Visible : Visibility.Collapsed; live.Visibility = value == "live" ? Visibility.Visible : Visibility.Collapsed;
            settings.Visibility = value == "settings" ? Visibility.Visible : Visibility.Collapsed; help.Visibility = value == "help" ? Visibility.Visible : Visibility.Collapsed;
            records.Visibility = value == "records" ? Visibility.Visible : Visibility.Collapsed; Refresh();
            if (value == "live" && Field != null) Field.Focus();
        }
        bool CanPlay() { return playing && !stopped && Current != null && !Current.Finished; }
        void KeyDownInput(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) { ClearInput(); WindowState = WindowState.Minimized; e.Handled = true; return; }
            if (page != "live" || !CanPlay() || Keyboard.FocusedElement is TextBox || Keyboard.Modifiers != ModifierKeys.None) return;
            if (e.Key != Key.A && e.Key != Key.D && e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.W && e.Key != Key.Up && e.Key != Key.Space && e.Key != Key.S && e.Key != Key.Down) return;
            bool changed = pressed.Add(e.Key);
            if (changed && !e.IsRepeat) {
                if (e.Key == Key.W || e.Key == Key.Up || e.Key == Key.Space) jumpSerial++;
                if (e.Key == Key.S || e.Key == Key.Down) dropSerial++;
                SendInput();
            }
            e.Handled = true;
        }
        void KeyUpInput(object sender, KeyEventArgs e) { if (pressed.Remove(e.Key)) { SendInput(); e.Handled = true; } }
        void ClearInput() {
            pressed.Clear(); firing = false;
            if (Field != null && Field.IsMouseCaptured) Field.ReleaseMouseCapture(); SendInput();
        }
        InputFrame LocalFrame() {
            bool active = page == "live";
            return new InputFrame { Session = Current.Session, Seq = ++inputSeq, Jump = jumpSerial, Bomb = bombSerial, Drop = dropSerial,
                Left = active && (pressed.Contains(Key.A) || pressed.Contains(Key.Left)), Right = active && (pressed.Contains(Key.D) || pressed.Contains(Key.Right)),
                Fire = active && firing, Jet = active && pressed.Contains(Key.Space), AimX = aimX, AimY = aimY };
        }
        void SendInput() {
            if (!CanPlay()) return;
            var input = LocalFrame();
            if (host || practice) Engine.Submit(seat, input);
            else if (peer == null || !peer.Send(new Message { Kind = "input", Input = input })) Disconnect("输入发送失败。");
        }
        internal bool Inject(InputFrame input) {
            if (!CanPlay()) return false;
            pressed.Clear(); if (input.Left) pressed.Add(Key.A); if (input.Right) pressed.Add(Key.D);
            firing = input.Fire; jumpSerial = input.Jump; bombSerial = input.Bomb; dropSerial = input.Drop;
            if (input.Jet) pressed.Add(Key.Space);
            aimX = input.AimX; aimY = input.AimY; input.Seq = ++inputSeq; input.Session = Current.Session;
            if (host || practice) return Engine.Submit(seat, input);
            return peer.Send(new Message { Kind = "input", Input = input });
        }
        MatchOptions OptionsNow() { return new MatchOptions { Starter = starter, Map = map, Lives = lives }; }
        int ParsePort() { int value; if (!Int32.TryParse(port.Text, out value) || value < 1024 || value > 65535) { lobbyStatus.Text = "端口应为 1024–65535。"; return -1; } return value; }
        void CopyAddress() {
            if (VerificationMode || String.IsNullOrWhiteSpace(address.Text)) return;
            try { Clipboard.SetText(address.Text); lobbyStatus.Text = "地址已复制。"; } catch (Exception) { lobbyStatus.Text = "剪贴板忙，可选中地址后 Ctrl+C。"; }
        }
        void AddressMenu() {
            var menu = new ContextMenu { Background = surface, Foreground = ink, FontSize = 10, FontWeight = FontWeights.Normal };
            foreach (string text in addresses) { string choice = text; var item = new MenuItem { Header = text }; item.Click += delegate { address.Text = choice; }; menu.Items.Add(item); }
            addressButton.ContextMenu = menu; menu.PlacementTarget = addressButton; menu.IsOpen = menu.Items.Count > 0;
        }
        void JoinUi() { int value = ParsePort(); if (value < 0) return; try { StartJoin(Wire.Endpoint(address.Text, value)); } catch (Exception ex) { lobbyStatus.Text = ex.Message; } }
        void Reset() {
            Engine = null; Current = previous = null; playing = practice = stopped = false; accumulated = 0;
            roomOptions.Clear(); roomSeats.Clear();
            pressed.Clear(); firing = false; inputSeq = jumpSerial = bombSerial = dropSerial = 0;
        }
        void Dispatch(Peer source, Action action) {
            if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(delegate { if (peer == source && !Dispatcher.HasShutdownStarted) action(); }));
        }
        void Attach(Peer source) {
            peer = source; fingerprint = Wire.Fingerprint();
            source.Connected += delegate { Dispatch(source, delegate {
                if (!host) { lobbyStatus.Text = "已连接，正在申请玩家编号…"; source.Send(new Message { Kind = "hello", Fingerprint = fingerprint, Options = OptionsNow() }); }
                else if (Engine == null) lobbyStatus.Text = "房间已建 · " + roomOptions.Count + "/6 人 · 等待加入后点击开始。";
                Refresh();
            }); };
            source.Received += m => Dispatch(source, delegate { Receive(m); });
            source.Disconnected += text => Dispatch(source, delegate { Disconnect(text); });
        }
        internal void StartHost(int value) {
            if (value < 0 || peer != null) return; Reset(); host = true; seat = 0; hostOptions = OptionsNow(); roomOptions.Add(hostOptions);
            try { var source = new Peer(); Attach(source); source.Host(value); addresses = Wire.Addresses(source.ListeningPort); address.Text = addresses[0];
                lobbyStatus.Text = "房间已建 · 1/6 人 · 最多 5 名玩家加入，全部到齐后点击开始。"; }
            catch (Exception ex) { if (peer != null) peer.Dispose(); peer = null; lobbyStatus.Text = "创建失败：" + ex.Message; }
            Refresh();
        }
        internal void StartJoin(IPEndPoint endpoint) {
            if (peer != null) return; Reset(); host = false; seat = -1;
            var source = new Peer(); Attach(source); lobbyStatus.Text = "正在连接…"; source.Join(endpoint); Refresh();
        }
        internal void StartRoom() {
            if (!host || peer == null || playing || roomOptions.Count < 2) return;
            Engine = new Match(roomOptions.ToArray(), Environment.TickCount); Current = Engine.State;
            foreach (var entry in roomSeats) peer.SendTo(entry.Key, new Message { Kind = "welcome", Fingerprint = fingerprint, Seat = entry.Value, State = Rules.Copy(Current) });
            BeginPlay(); Refresh();
        }
        void BeginPlay() {
            playing = true; stopped = false; lastTime = clock.Elapsed.TotalSeconds; accumulated = 0;
            var target = Current.Players.Where(p => p.Seat != seat && p.Lives > 0).FirstOrDefault();
            aimX = target == null ? 500 : target.X; aimY = target == null ? Current.Players[seat].Y - 25 : target.Y - 25;
            lobbyStatus.Text = "已分配 " + (seat + 1) + " 号玩家，对局已开始。"; ShowPage("live"); SendInput();
        }
        void Receive(Message m) {
            if (m.Protocol != 1) { Reject("协议不一致，请使用同一份程序包。"); return; }
            if (m.Kind == "reject") { Disconnect(m.Text ?? "版本校验失败。"); return; }
            if (host) {
                if (m.Kind == "hello") {
                    if (Engine != null) { peer.SendTo(m.ConnectionId, new Message { Kind = "reject", Text = "本局已经开始，无法中途加入。" }); return; }
                    if (m.Fingerprint != fingerprint || !Match.ValidOptions(m.Options)) { peer.SendTo(m.ConnectionId, new Message { Kind = "reject", Text = "程序校验失败，请使用相同版本。" }); return; }
                    int assigned;
                    if (!roomSeats.TryGetValue(m.ConnectionId, out assigned)) {
                        if (roomOptions.Count >= 6) { peer.SendTo(m.ConnectionId, new Message { Kind = "reject", Text = "房间已满，最多 6 人。" }); return; }
                        assigned = roomOptions.Count; roomSeats[m.ConnectionId] = assigned;
                        roomOptions.Add(new MatchOptions { Starter = m.Options.Starter, Map = hostOptions.Map, Lives = hostOptions.Lives });
                    }
                    peer.SendTo(m.ConnectionId, new Message { Kind = "waiting", Seat = assigned, Text = "已分配 " + (assigned + 1) + " 号玩家，等待房主开始。" });
                    lobbyStatus.Text = "房间已建 · " + roomOptions.Count + "/6 人 · 已加入玩家按顺序编号，点击开始对战。"; Refresh(); return;
                }
                if (m.Kind == "input") {
                    if (Engine == null || !playing || Current.Finished) return;
                    int playerSeat;
                    if (!roomSeats.TryGetValue(m.ConnectionId, out playerSeat) || !Engine.Submit(playerSeat, m.Input)) { Reject("输入校验失败，对局已停止。"); return; } return;
                }
            } else {
                if (m.Kind == "waiting") { seat = m.Seat; lobbyStatus.Text = m.Text ?? ("已分配 " + (seat + 1) + " 号玩家，等待房主开始。"); Refresh(); return; }
                if (m.Kind == "welcome" && Current == null) {
                    if (m.Fingerprint != fingerprint || !Wire.Valid(m.State)) { Disconnect("开局状态校验失败。"); return; }
                    seat = m.Seat; Current = m.State; BeginPlay(); return;
                }
                if (m.Kind == "state" && Current != null && playing) {
                    if (!Wire.Valid(m.State) || m.State.Session != Current.Session) { Disconnect("收到无效状态，对局已停止。"); return; }
                    if (m.State.Tick <= Current.Tick) return;
                    previous = Current; Current = m.State; receivedAt = clock.Elapsed.TotalSeconds; Refresh(); return;
                }
            }
            Reject("房间消息状态不匹配：" + m.Kind + " / 当前状态=" + (Current != null) + " / 对局中=" + playing);
        }
        void Reject(string text) {
            if (peer != null) peer.Send(new Message { Kind = "reject", Text = text });
            playing = false; stopped = true; lobbyStatus.Text = text;
            var source = peer; var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            delay.Tick += delegate { delay.Stop(); if (peer == source && source != null) source.Dispose(); }; delay.Start(); Refresh();
        }
        void Disconnect(string text) {
            playing = false; stopped = true; pressed.Clear(); firing = false;
            if (peer != null) peer.Dispose(); if (Field != null && Field.IsMouseCaptured) Field.ReleaseMouseCapture();
            lobbyStatus.Text = text + " 请离开并重新建房。"; Refresh();
        }
        void Snapshot() { if (peer != null && playing && !peer.Send(new Message { Kind = "state", State = Rules.Copy(Current) })) Disconnect("网络队列已满。"); }
        internal void Leave() {
            if (peer != null) peer.Dispose(); peer = null; Reset(); addresses = new string[0];
            lobbyStatus.Text = "已离开。选择武器后可重新创建或加入。"; ShowPage("lobby");
        }
        internal void StartPractice() {
            if (peer != null) { lobbyStatus.Text = "请先离开局域网房间，再进入练习。"; ShowPage("lobby"); return; }
            Reset(); practice = true; host = false; seat = 0;
            Engine = new Match(OptionsNow(), new MatchOptions { Starter = starter, Map = map, Lives = lives }, Environment.TickCount);
            Current = Engine.State; bot = new Bot(Environment.TickCount); BeginPlay();
        }
        void Tick() {
            double now = clock.Elapsed.TotalSeconds, delta = Math.Min(.12, now - lastTime); lastTime = now;
            visualTick++;
            if (playing && !stopped && Current != null) {
                if (CanPlay() && visualTick % 2 == 0) SendInput();
                if (host || practice) {
                    if (!Current.Finished || Current.Shatters.Count > 0) accumulated += delta;
                    while (accumulated >= Rules.Dt && (!Current.Finished || Current.Shatters.Count > 0)) {
                        accumulated -= Rules.Dt;
                        if (practice && !Current.Finished) Engine.Submit(1, bot.Think(Current, 1));
                        Engine.Step();
                        if (host && Current.Tick % 2 == 0) Snapshot();
                    }
                }
            }
            if (visualTick % 5 == 0) Refresh();
            if (page == "live") {
                Field.State = Current; Field.Previous = previous; Field.Seat = seat; Field.Ready = CanPlay();
                Field.AimX = aimX; Field.AimY = aimY; Field.Blend = stopped ? 1 : Math.Min(1, (now - receivedAt) / (2 * Rules.Dt)); Field.InvalidateVisual();
            }
        }
        internal void Refresh() {
            if (status == null) return; bool busy = peer != null;
            hostButton.Content = host && busy ? "开始对战" : "创建房间";
            hostButton.IsEnabled = !busy || host && !playing && roomOptions.Count >= 2; joinButton.IsEnabled = !busy; leaveButton.IsEnabled = busy || Current != null; resumeButton.IsEnabled = Current != null;
            address.IsReadOnly = busy && host; port.IsEnabled = !busy; copyButton.IsEnabled = address.Text.Length > 0; addressButton.IsEnabled = addresses.Length > 1;
            for (int i = 0; i < Rules.StarterCount; i++) { starters[i].IsEnabled = !busy; starters[i].Background = i == starter ? surface : Brushes.Transparent; starters[i].BorderThickness = new Thickness(i == starter ? 1 : 0); }
            mapButton.IsEnabled = livesButton.IsEnabled = !busy; mapButton.Content = "地图：" + Rules.Arenas[map].Name; livesButton.Content = "生命：" + lives;
            status.Text = stopped ? "连接已断开 · 操作停止" : practice ? "本地练习" : playing ? "局域网 · " + (seat + 1) + "号" : busy ? "房间 · " + roomOptions.Count + "/6人" : "协作 · 未连接";
            if (Current == null) return;
            var mine = Current.Players[seat]; var enemy = Current.Players.Where(p => p.Seat != seat && p.Lives > 0).FirstOrDefault() ?? Current.Players[(seat + 1) % Current.Players.Length];
            string ammo = mine.ReloadTime > 0 ? "换弹 " + mine.ReloadTime.ToString("0.0") + "s" : Rules.Weapons[mine.Weapon].Melee ? "近战" : mine.Magazine + "/" + (mine.Reserve < 0 ? "∞" : mine.Reserve.ToString());
            string buff = mine.ShieldTime > 0 ? " · 盾 " + mine.ShieldTime.ToString("0.0") + "s" : mine.JetOwned ? " · 火箭燃料 " + mine.JetFuel.ToString("0.0") + "s" : mine.InvisibleTime > 0 ? " · 隐身 " + mine.InvisibleTime.ToString("0.0") + "s" : mine.SpeedTime > 0 ? " · 速 " + mine.SpeedTime.ToString("0.0") + "s" : "";
            if (mine.Weapon == 6 || mine.Weapon == 8) buff += " · 剩余 " + mine.WeaponTime.ToString("0.0") + "s";
            ownHud.Text = (Current.Players.Length > 2 ? (mine.Seat + 1) + "号 " : "我 ") + mine.Lives + "命 · " + Rules.Weapons[mine.Weapon].Name + " " + ammo + " · 弹 " + mine.Bombs + buff +
                (minimal ? "" : " · 冲击 " + (int)mine.Impact + "%");
            enemyHud.Text = Current.Players.Length > 2 ? "存活 " + Current.Players.Count(p => p.Lives > 0) + "/" + Current.Players.Length + " · 编号显示在角色头顶" : "对  " + enemy.Lives + " 命 · " + Rules.Weapons[enemy.Weapon].Name + (enemy.Weapon == 6 ? " · " + enemy.Magazine + "发/" + enemy.WeaponTime.ToString("0") + "s" : enemy.Weapon == 8 ? " · " + enemy.WeaponTime.ToString("0") + "s" : "") + (minimal ? "" : " · 冲击 " + (int)enemy.Impact + "%");
            timerHud.Text = String.Format("{0:00}:{1:00} · {2} · 武 {3}s · 大 {4}s · 补 {5}s", (int)Current.Seconds / 60, (int)Current.Seconds % 60,
                Rules.Arenas[Current.Map].Name, (int)Math.Ceiling(Current.NextCrate), (int)Math.Ceiling(Current.NextBigCrate), (int)Math.Ceiling(Current.NextPower));
            hint.Text = stopped ? "断线已停止。连接页离开后可重建。" : Current.Finished ? "本局结束 · 联机离开重建 / 点 test 重新练习" :
                "A/D 移动 · W 跳跃 · 空格跳/飞行 · 左键射击 · 右键炸弹";
            recordText.Text = String.Join("\n", Current.Log.AsEnumerable().Reverse());
            Field.State = Current; Field.Seat = seat; Field.Ready = CanPlay(); Field.InvalidateVisual();
        }
        void LoadPreferences() {
            try {
                string file = Path.Combine(root, "preferences.ini"); if (!File.Exists(file)) return;
                foreach (string line in File.ReadAllLines(file)) {
                    string[] p = line.Split('='); if (p.Length != 2) continue; int value;
                    if (p[0] == "theme" && (p[1] == "wps" || p[1] == "codex")) theme = p[1];
                    if (p[0] == "appearance" && (p[1] == "light" || p[1] == "dark")) appearance = p[1];
                    if (p[0] == "mode") minimal = p[1] == "minimal";
                    if (p[0] == "starter" && Int32.TryParse(p[1], out value) && value >= 0 && value < Rules.StarterCount) starter = value;
                    if (p[0] == "map" && Int32.TryParse(p[1], out value) && value >= 0 && value < Rules.Arenas.Length) map = value;
                    if (p[0] == "lives" && Int32.TryParse(p[1], out value) && (value == 3 || value == 5 || value == 7 || value == 10)) lives = value;
                }
            } catch (IOException) { }
        }
        void SavePreferences() {
            if (VerificationMode) return;
            try { File.WriteAllText(Path.Combine(root, "preferences.ini"), "theme=" + theme + "\nappearance=" + appearance + "\nmode=" +
                (minimal ? "minimal" : "normal") + "\nstarter=" + starter + "\nmap=" + map + "\nlives=" + lives + "\n"); }
            catch (Exception) { if (lobbyStatus != null) lobbyStatus.Text = "设置已生效，当前目录无法保存偏好。"; }
        }
        internal void SetPreview(MatchState state) {
            timer.Stop(); Reset(); Current = state; seat = 0; playing = false; practice = false;
            aimX = 700; aimY = 310; ShowPage("live"); Field.Ready = true; Field.AimVisible = true; Field.AimX = aimX; Field.AimY = aimY;
        }
    }
}
