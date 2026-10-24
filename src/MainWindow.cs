using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Relay
{
    public sealed class MainWindow : Window
    {
        private readonly EngineRunner _engine;
        private readonly bool _startMinimized, _autoStart;
        private readonly DispatcherTimer _timer;
        private readonly List<string> _logs = new List<string>();
        private readonly ObservableCollection<ActivityItem> _recent = new ObservableCollection<ActivityItem>();
        private readonly CancellationTokenSource _diagnosticCancellation = new CancellationTokenSource();
        private readonly List<RadioButton> _navButtons = new List<RadioButton>();
        private readonly List<RadioButton> _profileCards = new List<RadioButton>();
        private UIElement[] _pages;
        private ColumnDefinition _sidebarColumn;
        private Grid _pageHost;
        private StackPanel _dashboardPage;
        private Border _headerFeature, _powerHalo;
        private TextBlock _headerState, _headerFeatureText, _heroBadge, _stateTitle, _stateDescription, _profileDescription, _footerStatus, _telemetry, _logCount, _sideState, _lastTarget, _recentCount, _emptyActivity;
        private TextBlock _tlsChip, _dnsChip, _voiceChip;
        private Border _statusDot;
        private Button _toggleButton, _powerButton, _testButton, _heroTestButton, _restartButton, _dnsButton;
        private ComboBox _profilePicker;
        private CheckBox _tlsOption, _domainOption, _startupOption;
        private TextBox _logBox;
        private readonly TextBlock[] _probeStates = new TextBlock[3];
        private MetricView _latencyMetric, _sessionsMetric, _bytesMetric, _enginesMetric;
        private System.Windows.Forms.NotifyIcon _tray;
        private System.Windows.Forms.ToolStripMenuItem _trayToggle;
        private bool _busy, _startupBusy, _probeBusy, _forceExit, _syncProfiles, _dnsBusy;
        private string _busyText = "Başlatılıyor…", _lastProbeHost;
        private int _selectedProfileIndex, _selectedPage;
        private long? _lastLatencyMs;
        private volatile bool _closing;
        private bool _enableTls = true, _useDomainList = true;
        private static readonly Brush Muted = Brush("#8C9BA4"), Light = Brush("#E8EFF1"), Blue = Brush("#42CADD"), Green = Brush("#00D4A5"), Red = Brush("#F17C8B");
        private static readonly string[] ProfileNotes = {
            "İlk deneme için başlangıç profili. TCP, DNS ve ses araçları birlikte açılır.",
            "Standart profil sonuç vermiyorsa farklı TCP ayarını dener.",
            "İlk iki profil sonuç vermediğinde denenebilecek daha yoğun ayar.",
            "TTL değerini 3 olarak ayarlar. Sonuç ağdaki yönlendiricilere göre değişebilir."
        };
        private const string ShieldIcon = "M12,2 L4,5 L4,11 C4,17 8,21 12,23 C16,21 20,17 20,11 L20,5 Z";
        private const string RouteIcon = "M5,3 L5,9 L12,13 L19,9 L19,3 M12,13 L12,22 M2,5 L5,2 L8,5 M16,5 L19,2 L22,5";
        private const string ConsoleIcon = "M3,4 L21,4 L21,20 L3,20 Z M6,8 L9,11 L6,14 M12,15 L17,15";
        private const string SettingsIcon = "M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M8,3 L8,9 M16,9 L16,15 M10,15 L10,21";
        private const string PowerIcon = "M12,2 L12,11 M6,5 A9,9 0 1 0 18,5";
        private const string ArrowIcon = "M4,12 L20,12 M14,6 L20,12 L14,18";
        private const string RefreshIcon = "M20,7 A9,9 0 1 0 21,15 M20,2 L20,7 L15,7";
        private const string PulseIcon = "M2,12 L6,12 L9,4 L14,20 L17,12 L22,12";
        private sealed class MetricView { public TextBlock Value, Unit, Hint; }
        private sealed class ActivityItem
        {
            public string Time { get; set; }
            public string Kind { get; set; }
            public string Message { get; set; }
            public string Detail { get; set; }
            public Brush Color { get; set; }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public MainWindow(bool startMinimized) : this(startMinimized, true) { }
        internal MainWindow(bool startMinimized, bool autoStart)
        {
            _autoStart = autoStart; _startMinimized = startMinimized;
            _engine = new EngineRunner(); _engine.LogMessage += OnEngineLog;
            Resources = UiTheme.Create();
            Title = "Relay"; Width = Math.Min(860, SystemParameters.WorkArea.Width - 32); Height = Math.Min(600, SystemParameters.WorkArea.Height - 32);
            MinWidth = Math.Min(800, Width); MinHeight = Math.Min(560, Height);
            WindowStartupLocation = WindowStartupLocation.CenterScreen; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
            Background = Brush("#090D10"); FontFamily = new FontFamily("Segoe UI"); UseLayoutRounding = true; SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            SourceInitialized += delegate { int dark = 1; DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int)); };
            string icon = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"assets\relay.ico");
            if (File.Exists(icon)) { try { Icon = BitmapFrame.Create(new Uri(icon)); } catch (IOException) { } }
            BuildInterface(autoStart && EngineRunner.IsStartupEnabled());
            InitializeTray();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += delegate { if (!_closing && !_busy) { _engine.CheckHealth(); UpdateVisualState(); } };
            _timer.Start(); UpdateVisualState();
            SizeChanged += delegate { ApplyResponsiveLayout(); };
            Loaded += delegate { AppendLog("Relay açıldı."); if (_autoStart) StartEngineAsync(); if (_startMinimized) Hide(); };
        }

        private void BuildInterface(bool startupEnabled)
        {
            Grid shell = new Grid(); shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) }); shell.RowDefinitions.Add(new RowDefinition());
            shell.Children.Add(BuildTitleBar());
            Grid body = new Grid(); _sidebarColumn = new ColumnDefinition { Width = new GridLength(176) }; body.ColumnDefinitions.Add(_sidebarColumn); body.ColumnDefinitions.Add(new ColumnDefinition());
            body.Children.Add(BuildSidebar());
            _pageHost = new Grid { Margin = new Thickness(32, 28, 32, 28) };
            _pages = new[] { BuildConnectionPage(), BuildProfilesPage(), BuildDiagnosticsPage(), BuildSettingsPage(startupEnabled) };
            for (int i = 0; i < _pages.Length; i++) { _pages[i].Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed; _pageHost.Children.Add(_pages[i]); }
            Grid.SetColumn(_pageHost, 1); body.Children.Add(_pageHost); Grid.SetRow(body, 1); shell.Children.Add(body);
            Content = new Border { Background = Brush("#090D10"), BorderBrush = Brush("#253036"), BorderThickness = new Thickness(1), Child = shell };
            _navButtons[0].IsChecked = true; ApplyResponsiveLayout();
        }

        private UIElement BuildTitleBar()
        {
            Grid header = new Grid { Margin = new Thickness(16, 0, 16, 0), Background = Brushes.Transparent };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            Border mark = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(7), Background = Brush("#0C2620"), BorderBrush = Brush("#164E40"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) };
            mark.Child = Glyph("M6,20 L6,5 L13,5 C17,5 19,7 19,10 C19,13 17,15 13,15 L6,15 M13,15 L20,21", Green, 16); brand.Children.Add(mark);
            TextBlock name = Text("Relay", 16, Light, FontWeights.Bold); name.VerticalAlignment = VerticalAlignment.Center; brand.Children.Add(name);
            TextBlock version = Mono("v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3), 10, Muted);
            brand.Children.Add(new Border { Child = version, CornerRadius = new CornerRadius(4), Background = Brush("#1B2329"), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(brand, 1); header.Children.Add(brand);
            StackPanel status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _statusDot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = Muted, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            status.Children.Add(_statusDot); _headerState = Mono("Beklemede", 11, Muted); status.Children.Add(_headerState);
            Grid.SetColumn(status, 2); header.Children.Add(new Border { Child = status, Background = Brush("#101C1C"), BorderBrush = Brush("#19322F"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Padding = new Thickness(10, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn((UIElement)header.Children[header.Children.Count - 1], 2);
            StackPanel tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid picker = new Grid { Width = 206 }; picker.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); picker.ColumnDefinitions.Add(new ColumnDefinition());
            picker.Children.Add(Glyph(SettingsIcon, Blue, 14));
            _profilePicker = new ComboBox { Height = 30, FontFamily = new FontFamily("Consolas"), FontSize = 11.5 };
            foreach (EngineProfile profile in EngineRunner.Profiles) _profilePicker.Items.Add(profile.Name);
            _profilePicker.SelectedIndex = 0; AutomationProperties.SetName(_profilePicker, "Bağlantı profili");
            _profilePicker.SelectionChanged += delegate {
                int selected = _profilePicker.SelectedIndex; if (selected < 0 || selected == _selectedProfileIndex) return;
                _selectedProfileIndex = selected; UpdateProfileCards();
                AppendLog("Profil seçildi: " + EngineRunner.Profiles[selected].Name + ".");
                if (_engine.IsRunning) StartEngineAsync();
            };
            Grid.SetColumn(_profilePicker, 1); picker.Children.Add(_profilePicker); tools.Children.Add(picker);
            StackPanel feature = new StackPanel { Orientation = Orientation.Horizontal }; feature.Children.Add(Glyph(PulseIcon, Blue, 13));
            _headerFeatureText = Mono("TLS-SPLIT", 10.5, Blue); _headerFeatureText.Margin = new Thickness(7, 0, 0, 0); feature.Children.Add(_headerFeatureText);
            _headerFeature = new Border { Child = feature, CornerRadius = new CornerRadius(13), Background = Brush("#0D2028"), BorderBrush = Brush("#183942"), BorderThickness = new Thickness(1), Padding = new Thickness(11, 6, 11, 6), Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center }; tools.Children.Add(_headerFeature);
            Button settings = Button("Ayarları aç", "IconButton", delegate { SelectTab(3); }); settings.Content = Glyph(SettingsIcon, Muted, 16); settings.Width = 32; settings.ToolTip = "Ayarlar"; tools.Children.Add(settings);
            Grid.SetColumn(tools, 4); header.Children.Add(tools);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) ToggleMaximize(); else if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            return new Border { Background = Brush("#0C1115"), BorderBrush = Brush("#192127"), BorderThickness = new Thickness(0, 0, 0, 1), Child = header };
        }

        private UIElement BuildSidebar()
        {
            Grid side = new Grid { Margin = new Thickness(10, 18, 10, 10) }; side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); side.RowDefinitions.Add(new RowDefinition()); side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel nav = new StackPanel(); TextBlock label = Mono("KONTROL PANELİ", 10.5, Brush("#6C7B85")); label.Margin = new Thickness(10, 0, 0, 20); nav.Children.Add(label);
            string[] labels = { "Bağlantı merkezi", "Ağ profilleri", "Canlı günlük", "Ayarlar" }; string[] icons = { ShieldIcon, RouteIcon, ConsoleIcon, SettingsIcon };
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i; StackPanel row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(Glyph(icons[i], null, 15));
                TextBlock title = new TextBlock { Text = labels[i], FontSize = 11.5, FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; title.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) }); row.Children.Add(title);
                RadioButton button = new RadioButton { Content = row, GroupName = "RelayNavigation", Style = (Style)Resources["Navigation"], Margin = new Thickness(0, 0, 0, 7) };
                AutomationProperties.SetName(button, labels[i]); button.Checked += delegate { if (_pages != null) SelectTab(index); }; _navButtons.Add(button); nav.Children.Add(button);
            }
            side.Children.Add(nav);
            StackPanel footer = new StackPanel(); Grid system = new Grid(); system.ColumnDefinitions.Add(new ColumnDefinition()); system.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock systemName = Mono("Sistem durumu", 10.5, Muted); system.Children.Add(systemName); _sideState = Mono("Beklemede", 10.5, Muted); Grid.SetColumn(_sideState, 1); system.Children.Add(_sideState);
            footer.Children.Add(new Border { Child = system, Background = Brush("#121A1F"), BorderBrush = Brush("#1C272D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 10) });
            _dnsButton = Button("DNS önbelleğini temizle", "Link", ClearDnsAsync); StackPanel dnsLabel = new StackPanel { Orientation = Orientation.Horizontal }; dnsLabel.Children.Add(Glyph(RefreshIcon, Blue, 13)); TextBlock dnsText = Mono("DNS'i temizle", 10.5, Light); dnsText.Margin = new Thickness(8, 0, 0, 0); dnsLabel.Children.Add(dnsText); _dnsButton.Content = dnsLabel; footer.Children.Add(_dnsButton);
            _footerStatus = Text("Hazır", 10.5, Muted); _footerStatus.Margin = new Thickness(10, 6, 0, 0); footer.Children.Add(_footerStatus);
            Grid.SetRow(footer, 2); side.Children.Add(footer);
            return new Border { Background = Brush("#0E1418"), BorderBrush = Brush("#1C252B"), BorderThickness = new Thickness(0, 0, 1, 0), Child = side };
        }

        private UIElement BuildConnectionPage()
        {
            StackPanel page = new StackPanel(); _dashboardPage = page; Grid hero = new Grid(); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); hero.ColumnDefinitions.Add(new ColumnDefinition()); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _powerButton = Button("Bağlantı araçlarını başlat", "Power", ToggleEngineState); _powerButton.Content = Glyph(PowerIcon, Green, 25); _powerButton.Width = 62; _powerButton.Height = 62;
            _powerHalo = new Border { Child = _powerButton, Width = 64, Height = 64, CornerRadius = new CornerRadius(32), VerticalAlignment = VerticalAlignment.Center, Effect = new DropShadowEffect { BlurRadius = 26, ShadowDepth = 0, Color = Color.FromRgb(0, 210, 161), Opacity = 0.32 } }; hero.Children.Add(_powerHalo);
            StackPanel heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
            _stateTitle = Text("Discord bağlantısı beklemede", 20, Light, FontWeights.SemiBold); _stateTitle.TextWrapping = TextWrapping.Wrap; heroText.Children.Add(_stateTitle);
            _heroBadge = Mono("BAŞLATMAYA HAZIR", 10.5, Green); heroText.Children.Add(new Border { Child = _heroBadge, Background = Brush("#0C3229"), CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 5, 10, 5), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 8) });
            _stateDescription = Mono("Seçili profili başlat, Discord erişimini tek yerden yönet.", 11.5, Muted); _stateDescription.TextWrapping = TextWrapping.Wrap; heroText.Children.Add(_stateDescription); Grid.SetColumn(heroText, 1); hero.Children.Add(heroText);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _heroTestButton = Button("Erişim testi", "Cyan", delegate { RunDiagnosticsAsync(); }); _heroTestButton.Content = ButtonContent(PulseIcon, "Erişim testi"); _heroTestButton.Margin = new Thickness(0, 0, 10, 0); actions.Children.Add(_heroTestButton);
            _toggleButton = Button("Başlat", "Primary", ToggleEngineState); _toggleButton.MinWidth = 88; actions.Children.Add(_toggleButton); Grid.SetColumn(actions, 2); hero.Children.Add(actions);
            LinearGradientBrush heroBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) }; heroBrush.GradientStops.Add(new GradientStop(Color.FromRgb(16, 38, 31), 0)); heroBrush.GradientStops.Add(new GradientStop(Color.FromRgb(16, 22, 26), 0.55)); heroBrush.GradientStops.Add(new GradientStop(Color.FromRgb(12, 32, 39), 1));
            page.Children.Add(new Border { Background = heroBrush, BorderBrush = Brush("#1A2A2B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Padding = new Thickness(14, 13, 14, 13), MinHeight = 126, Child = hero });
            Grid metrics = new Grid { Margin = new Thickness(0, 24, 0, 0) }; for (int i = 0; i < 4; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
            AddMetric(metrics, 0, "HTTPS YANIT SÜRESİ", "M4,17 A9,9 0 1 1 20,17 M12,13 L17,8", Green, out _latencyMetric);
            AddMetric(metrics, 1, "AKTİF TLS OTURUMU", ShieldIcon, Green, out _sessionsMetric);
            AddMetric(metrics, 2, "L7 VERİ AKIŞI", "M8,3 L8,20 M3,8 L8,3 L13,8 M16,4 L16,21 M11,16 L16,21 L21,16", Blue, out _bytesMetric);
            AddMetric(metrics, 3, "ÇALIŞAN MOTORLAR", SettingsIcon, Muted, out _enginesMetric); page.Children.Add(metrics);
            Grid protocols = new Grid(); protocols.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); protocols.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); protocols.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock protocolLabel = Mono("PROTOKOLLER", 10, Muted); protocolLabel.VerticalAlignment = VerticalAlignment.Center; protocolLabel.Margin = new Thickness(0, 0, 16, 0); protocols.Children.Add(protocolLabel);
            StackPanel chips = new StackPanel { Orientation = Orientation.Horizontal }; chips.Children.Add(ProtocolChip("TLS-Split :443", out _tlsChip)); chips.Children.Add(ProtocolChip("DNS :1253", out _dnsChip)); chips.Children.Add(ProtocolChip("UDP ses", out _voiceChip)); Grid.SetColumn(chips, 1); protocols.Children.Add(chips);
            _lastTarget = Mono("Son hedef: henüz yok", 10.5, Muted); _lastTarget.TextTrimming = TextTrimming.CharacterEllipsis; _lastTarget.HorizontalAlignment = HorizontalAlignment.Right; _lastTarget.VerticalAlignment = VerticalAlignment.Center; _lastTarget.Margin = new Thickness(18, 0, 0, 0); Grid.SetColumn(_lastTarget, 2); protocols.Children.Add(_lastTarget);
            page.Children.Add(new Border { Child = protocols, Background = Brush("#0D1317"), BorderBrush = Brush("#1A232A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 24, 0, 0) });
            Grid activity = new Grid(); activity.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) }); activity.RowDefinitions.Add(new RowDefinition { Height = new GridLength(114) });
            Grid activityHeader = new Grid { Margin = new Thickness(16, 0, 10, 0) }; activityHeader.ColumnDefinitions.Add(new ColumnDefinition()); activityHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel activityName = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; activityName.Children.Add(Mono("CANLI İŞLEM GÜNLÜĞÜ", 11, Light));
            _recentCount = Mono("İşlem bekleniyor", 10, Green); activityName.Children.Add(new Border { Child = _recentCount, Background = Brush("#0B2B23"), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 4, 7, 4), Margin = new Thickness(12, 0, 0, 0) }); activityHeader.Children.Add(activityName);
            Button fullLog = Button("Tüm günlüğü aç", "Link", delegate { SelectTab(2); }); Grid.SetColumn(fullLog, 1); activityHeader.Children.Add(fullLog); activity.Children.Add(activityHeader);
            Grid feed = new Grid(); ItemsControl recentRows = new ItemsControl { ItemsSource = _recent, ItemTemplate = (DataTemplate)Resources["ActivityRow"] }; feed.Children.Add(recentRows);
            _emptyActivity = Mono("Bağlantı araçlarını başlattığında işlemler burada görünecek.", 11.5, Muted); _emptyActivity.Margin = new Thickness(18, 0, 18, 0); _emptyActivity.VerticalAlignment = VerticalAlignment.Center; _emptyActivity.TextWrapping = TextWrapping.Wrap; feed.Children.Add(_emptyActivity); Grid.SetRow(feed, 1); activity.Children.Add(feed);
            page.Children.Add(new Border { Child = activity, Background = Brush("#0D1317"), BorderBrush = Brush("#1A232A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 24, 0, 0) });
            return Scroll(page);
        }

        private UIElement BuildProfilesPage()
        {
            StackPanel page = new StackPanel(); page.Children.Add(Text("Ağ profilleri", 25, Light, FontWeights.SemiBold));
            TextBlock intro = Text("Standart profille başlayıp gerekirse diğer seçenekleri deneyebilirsin.", 13, Muted); intro.Margin = new Thickness(0, 9, 0, 24); page.Children.Add(intro);
            Grid cards = new Grid(); cards.ColumnDefinitions.Add(new ColumnDefinition()); cards.ColumnDefinitions.Add(new ColumnDefinition()); cards.RowDefinitions.Add(new RowDefinition()); cards.RowDefinitions.Add(new RowDefinition());
            string[] badges = { "İLK DENEME", "TCP -6", "TCP -9", "TTL 3" };
            for (int i = 0; i < EngineRunner.Profiles.Length; i++)
            {
                int index = i; StackPanel info = new StackPanel(); Grid titleRow = new Grid(); titleRow.ColumnDefinitions.Add(new ColumnDefinition()); titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); titleRow.Children.Add(Text(EngineRunner.Profiles[i].Name, 16, Light, FontWeights.SemiBold));
                TextBlock badge = Mono(badges[i], 10, Green); Grid.SetColumn(badge, 1); titleRow.Children.Add(badge); info.Children.Add(titleRow);
                TextBlock note = Text(ProfileNotes[i], 12.5, Muted); note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 8, 0, 0); info.Children.Add(note);
                RadioButton card = new RadioButton { Content = info, GroupName = "RelayProfiles", Style = (Style)Resources["ProfileCard"], IsChecked = i == 0, Margin = new Thickness(i % 2 == 0 ? 0 : 8, 0, i % 2 == 0 ? 8 : 0, 16) }; AutomationProperties.SetName(card, EngineRunner.Profiles[i].Name);
                card.Checked += delegate { if (!_syncProfiles && _profilePicker.SelectedIndex != index) _profilePicker.SelectedIndex = index; }; _profileCards.Add(card); Grid.SetColumn(card, i % 2); Grid.SetRow(card, i / 2); cards.Children.Add(card);
            }
            page.Children.Add(cards); _profileDescription = Text("Seçili profil: Standart", 13, Green); _profileDescription.TextWrapping = TextWrapping.Wrap; _profileDescription.Margin = new Thickness(0, 10, 0, 0); page.Children.Add(_profileDescription);
            _restartButton = Button("Discord'u yeniden başlat", "Button", RestartDiscord); _restartButton.HorizontalAlignment = HorizontalAlignment.Left; _restartButton.Margin = new Thickness(0, 24, 0, 0); _restartButton.Content = ButtonContent(RefreshIcon, "Discord'u yeniden başlat"); page.Children.Add(_restartButton);
            TextBlock hint = Text("Açıkken profil değiştirirsen araçlar yeniden başlatılır. Her profilin sonucu ağa göre değişebilir.", 12, Muted); hint.TextWrapping = TextWrapping.Wrap; hint.Margin = new Thickness(0, 20, 0, 0); page.Children.Add(hint); return Scroll(page);
        }

        private UIElement BuildSettingsPage(bool startupEnabled)
        {
            StackPanel page = new StackPanel(); page.Children.Add(Text("Ayarlar", 25, Light, FontWeights.SemiBold)); TextBlock intro = Text("Bağlantı araçlarının nasıl çalışacağını buradan seçebilirsin.", 13, Muted); intro.Margin = new Thickness(0, 9, 0, 24); page.Children.Add(intro);
            StackPanel options = new StackPanel();
            _tlsOption = Option("Yerel TLS proxy", "Discord adreslerini yerel proxy'ye yönlendirir. Eklenen hosts kayıtları durdurunca kaldırılır.", true); _tlsOption.Checked += delegate { SetTlsOption(true); }; _tlsOption.Unchecked += delegate { SetTlsOption(false); }; options.Children.Add(_tlsOption); options.Children.Add(Rule());
            _domainOption = Option("Discord alan adı listesini kullan", "TCP ve QUIC kurallarını daraltır. DNS ve UDP ses kuralları başka bağlantıları da etkileyebilir.", true); _domainOption.Checked += delegate { SetDomainOption(true); }; _domainOption.Unchecked += delegate { SetDomainOption(false); }; options.Children.Add(_domainOption); options.Children.Add(Rule());
            _startupOption = Option("Oturum açıldığında başlat", "Kendi Windows hesabında, yönetici yetkisiyle ve sistem tepsisinde açılır.", startupEnabled); _startupOption.Checked += delegate { ChangeStartup(true); }; _startupOption.Unchecked += delegate { ChangeStartup(false); }; options.Children.Add(_startupOption);
            page.Children.Add(new Border { Child = options, Background = Brush("#0E1519"), BorderBrush = Brush("#202B30"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(24) }); return Scroll(page);
        }

        private UIElement BuildDiagnosticsPage()
        {
            Grid page = new Grid(); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition());
            StackPanel top = new StackPanel(); Grid heading = new Grid(); heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); heading.Children.Add(Text("Canlı günlük", 25, Light, FontWeights.SemiBold)); _testButton = Button("Erişim testi", "Cyan", RunDiagnosticsAsync); _testButton.Content = ButtonContent(PulseIcon, "Erişim testi"); Grid.SetColumn(_testButton, 1); heading.Children.Add(_testButton); top.Children.Add(heading);
            TextBlock hint = Text("HTTPS yanıtı ve istek süresi ölçülür. Sesli sohbeti Discord içinde ayrıca deneyebilirsin.", 12.5, Muted); hint.Margin = new Thickness(0, 10, 0, 22); hint.TextWrapping = TextWrapping.Wrap; top.Children.Add(hint);
            Grid probes = new Grid(); string[] hosts = { "updates.discord.com", "discord.com", "cdn.discordapp.com" };
            for (int i = 0; i < hosts.Length; i++)
            {
                probes.ColumnDefinitions.Add(new ColumnDefinition()); StackPanel data = new StackPanel(); data.Children.Add(Mono(hosts[i], 11.5, Light)); _probeStates[i] = Mono("Henüz denenmedi", 11, Muted); _probeStates[i].Margin = new Thickness(0, 9, 0, 0); data.Children.Add(_probeStates[i]); Border box = new Border { Child = data, Background = Brush("#10191D"), BorderBrush = Brush("#202B30"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Margin = new Thickness(i == 0 ? 0 : 6, 0, i == 2 ? 0 : 6, 0) }; Grid.SetColumn(box, i); probes.Children.Add(box);
            }
            top.Children.Add(probes); _telemetry = Mono("", 11.5, Muted); _telemetry.TextWrapping = TextWrapping.Wrap; _telemetry.Margin = new Thickness(0, 18, 0, 20); top.Children.Add(_telemetry); page.Children.Add(top);
            Grid logs = new Grid(); logs.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); logs.RowDefinitions.Add(new RowDefinition()); Grid bar = new Grid { Margin = new Thickness(0, 0, 0, 10) }; bar.ColumnDefinitions.Add(new ColumnDefinition()); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _logCount = Mono("GÜNLÜK · 0 KAYIT", 11, Light); _logCount.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(_logCount);
            Button clear = Button("Temizle", "Link", delegate { _logs.Clear(); _recent.Clear(); _logBox.Clear(); UpdateLogLabels(); }); Grid.SetColumn(clear, 1); bar.Children.Add(clear);
            Button copy = Button("Kopyala", "Link", delegate { try { Clipboard.SetText(string.Join(Environment.NewLine, _logs)); } catch (System.Runtime.InteropServices.COMException) { AppendLog("Pano kullanılamıyor. Günlük alanından metni seçip kopyalayabilirsin."); } }); Grid.SetColumn(copy, 2); bar.Children.Add(copy); logs.Children.Add(bar);
            _logBox = new TextBox { IsReadOnly = true, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 11.5, MinHeight = 150 }; AutomationProperties.SetName(_logBox, "İşlem günlüğü"); Grid.SetRow(_logBox, 1); logs.Children.Add(_logBox); Grid.SetRow(logs, 1); page.Children.Add(logs);
            ScrollViewer viewport = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = page }; page.MinHeight = 420; page.SetBinding(FrameworkElement.HeightProperty, new Binding("ViewportHeight") { Source = viewport }); return viewport;
        }

        private void SelectTab(int index)
        {
            if (_pages == null || index < 0 || index >= _pages.Length) return; _selectedPage = index;
            for (int i = 0; i < _pages.Length; i++) { _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed; if (_navButtons[i].IsChecked != (i == index)) _navButtons[i].IsChecked = i == index; }
        }
        private void UpdateProfileCards()
        {
            _syncProfiles = true; for (int i = 0; i < _profileCards.Count; i++) _profileCards[i].IsChecked = i == _selectedProfileIndex; _syncProfiles = false;
            if (_profileDescription != null) _profileDescription.Text = "Seçili profil: " + EngineRunner.Profiles[_selectedProfileIndex].Name + ". " + ProfileNotes[_selectedProfileIndex];
        }
        private void ApplyResponsiveLayout()
        {
            if (_headerFeature == null || _sidebarColumn == null) return; double width = ActualWidth > 0 ? ActualWidth : Width;
            _headerFeature.Visibility = width < 1000 ? Visibility.Collapsed : Visibility.Visible; _sidebarColumn.Width = new GridLength(width < 1000 ? 176 : 192); _pageHost.Margin = new Thickness(width < 1000 ? 14 : 24);
            if (_dashboardPage != null) for (int i = 1; i < _dashboardPage.Children.Count; i++) ((FrameworkElement)_dashboardPage.Children[i]).Margin = new Thickness(0, width < 1000 ? 12 : 20, 0, 0);
        }
        private void ToggleMaximize() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private static UIElement Glyph(string path, Brush color, double size)
        {
            System.Windows.Shapes.Path icon = new System.Windows.Shapes.Path { Data = Geometry.Parse(path), StrokeThickness = 1.7, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.Uniform, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            if (color != null) icon.Stroke = color; else icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) }); return icon;
        }
        private static StackPanel ButtonContent(string icon, string label) { StackPanel row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(Glyph(icon, null, 14)); TextBlock text = new TextBlock { Text = label, FontSize = 11.5, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; text.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) }); row.Children.Add(text); return row; }
        private static TextBlock Mono(string text, double size, Brush color) { TextBlock block = Text(text, size, color); block.FontFamily = new FontFamily("Consolas"); return block; }
        private static UIElement ProtocolChip(string title, out TextBlock text) { text = Mono("● " + title, 10.5, Muted); return new Border { Child = text, Background = Brush("#151D22"), BorderBrush = Brush("#243038"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 0, 8, 0) }; }
        private static void AddMetric(Grid grid, int index, string title, string icon, Brush accent, out MetricView view)
        {
            view = new MetricView(); StackPanel body = new StackPanel(); Grid heading = new Grid(); heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); TextBlock name = Mono(title, 10, Muted); name.VerticalAlignment = VerticalAlignment.Center; name.TextTrimming = TextTrimming.CharacterEllipsis; name.ToolTip = title; heading.Children.Add(name); UIElement graphic = Glyph(icon, accent, 14); Grid.SetColumn(graphic, 1); heading.Children.Add(graphic); body.Children.Add(heading);
            StackPanel number = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) }; view.Value = Text("0", 28, Light, FontWeights.SemiBold); view.Unit = Mono("", 11, Muted); view.Unit.Margin = new Thickness(6, 15, 0, 0); number.Children.Add(view.Value); number.Children.Add(view.Unit); body.Children.Add(number);
            view.Hint = Mono("", 10, Muted); view.Hint.Margin = new Thickness(0, 6, 0, 0); view.Hint.TextTrimming = TextTrimming.CharacterEllipsis; view.Hint.SetBinding(TextBlock.ToolTipProperty, new Binding("Text") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) }); body.Children.Add(view.Hint);
            Border card = new Border { Child = body, Background = Brush("#0F1519"), BorderBrush = Brush("#1D272D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 13, 14, 13), Margin = new Thickness(index == 0 ? 0 : 5, 0, index == 3 ? 0 : 5, 0), MinHeight = 102 }; Grid.SetColumn(card, index); grid.Children.Add(card);
        }
        private void SetTlsOption(bool enabled) { _enableTls = enabled; if (_engine.IsRunning) StartEngineAsync(); }
        private void SetDomainOption(bool enabled) { _useDomainList = enabled; if (_engine.IsRunning) StartEngineAsync(); }
        private void ChangeStartup(bool enabled)
        {
            if (_startupBusy || _closing) return;
            _startupBusy = true; _startupOption.IsEnabled = false;
            ThreadPool.QueueUserWorkItem(delegate {
                bool success = EngineRunner.SetStartupEnabled(enabled);
                bool actual = success ? enabled : EngineRunner.IsStartupEnabled();
                Post(delegate {
                    _startupOption.IsChecked = actual;
                    AppendLog(success ? (enabled ? "Otomatik başlangıç açıldı." : "Otomatik başlangıç kapatıldı.") : "Başlangıç ayarı değiştirilemedi: " + StartupManager.LastError);
                    _startupBusy = false; _startupOption.IsEnabled = !_busy;
                });
            });
        }
        private void ToggleEngineState() { if (_busy || _closing) return; if (_engine.IsRunning) StopEngineAsync(); else StartEngineAsync(); }
        private void StartEngineAsync()
        {
            if (_busy || _closing) return;
            _busy = true; _busyText = "Başlatılıyor…";
            EngineProfile profile = EngineRunner.Profiles[_selectedProfileIndex]; bool tls = _enableTls, list = _useDomainList;
            UpdateVisualState();
            ThreadPool.QueueUserWorkItem(delegate {
                try { _engine.Start(profile, tls, list); }
                catch (Exception ex) { OnEngineLog("Başlatılamadı: " + ex.Message); }
                finally { Post(delegate { _busy = false; UpdateVisualState(); }); }
            });
        }
        private void StopEngineAsync()
        {
            if (_busy || _closing) return;
            _busy = true; _busyText = "Durduruluyor…"; UpdateVisualState();
            ThreadPool.QueueUserWorkItem(delegate {
                try { _engine.Stop(); OnEngineLog("Bağlantı araçları durduruldu."); }
                finally { Post(delegate { _busy = false; UpdateVisualState(); }); }
            });
        }
        private void RestartDiscord()
        {
            if (_closing || !_restartButton.IsEnabled) return;
            _restartButton.IsEnabled = false;
            ThreadPool.QueueUserWorkItem(delegate { try { _engine.RestartDiscordClient(); } finally { Post(delegate { _restartButton.IsEnabled = true; }); } });
        }
        private void UpdateVisualState()
        {
            bool active = _engine.IsRunning, error = !string.IsNullOrEmpty(_engine.LastError);
            Brush stateColor = _busy ? Blue : active ? Green : error ? Red : Muted;
            string state = _busy ? _busyText : active ? "Araçlar çalışıyor" : error ? "Kontrol gerekiyor" : "Beklemede";
            _toggleButton.Content = _busy ? _busyText : active ? "Durdur" : "Başlat";
            _toggleButton.Style = (Style)Resources[active ? "Danger" : "Primary"];
            _toggleButton.IsEnabled = !_busy; _powerButton.IsEnabled = !_busy;
            string action = active ? "Bağlantı araçlarını durdur" : "Bağlantı araçlarını başlat";
            AutomationProperties.SetName(_toggleButton, _busy ? _busyText : action); AutomationProperties.SetName(_powerButton, action);
            _powerButton.ToolTip = action; _profilePicker.IsEnabled = !_busy;
            foreach (RadioButton card in _profileCards) card.IsEnabled = !_busy;
            _tlsOption.IsEnabled = !_busy; _domainOption.IsEnabled = !_busy; _startupOption.IsEnabled = !_busy && !_startupBusy;
            _testButton.IsEnabled = _heroTestButton.IsEnabled = !_busy && !_probeBusy;
            _testButton.Content = ButtonContent(PulseIcon, _probeBusy ? "Ölçülüyor…" : "Erişim testi");
            _heroTestButton.Content = ButtonContent(PulseIcon, _probeBusy ? "Ölçülüyor…" : "Erişim testi");
            _statusDot.Background = stateColor; _headerState.Text = state; _headerState.Foreground = stateColor; _sideState.Text = active ? "● Açık" : error ? "● Hata" : "● Hazır"; _sideState.Foreground = stateColor;
            _stateTitle.Text = _busy ? _busyText : active ? "Discord bağlantı araçları aktif" : error ? "Bağlantı başlatılamadı" : "Discord bağlantısına hazır";
            _stateDescription.Text = _busy ? "Seçili profilin araçları güncelleniyor." : active ? "TCP, DNS ve UDP araçları devrede. Discord erişimini test edebilirsin." : error ? "Ayrıntıyı günlükte görebilirsin. Ayarları kontrol edip yeniden dene." : "Seçili profili başlat. Discord erişimini ve ses bağlantısını tek yerden yönet.";
            _heroBadge.Text = _busy ? "İŞLEM SÜRÜYOR" : active ? "ARAÇLAR DEVREDE" : error ? "GÜNLÜĞÜ KONTROL ET" : "BAŞLATMAYA HAZIR"; _heroBadge.Foreground = active || !error ? Green : Red;
            ((DropShadowEffect)_powerHalo.Effect).Opacity = active ? 0.65 : 0.28;
            _footerStatus.Text = _busy ? _busyText : active ? "Profil: " + EngineRunner.Profiles[_selectedProfileIndex].Name : "Relay · Windows x64";
            _headerFeatureText.Text = _engine.TlsProxy.IsRunning ? "TLS-SPLIT AÇIK" : "TLS-SPLIT";
            _latencyMetric.Value.Text = _lastLatencyMs.HasValue ? _lastLatencyMs.Value.ToString() : "—"; _latencyMetric.Value.Foreground = _lastLatencyMs.HasValue ? Green : Light; _latencyMetric.Unit.Text = "ms";
            _latencyMetric.Hint.Text = _probeBusy ? "HTTPS isteği ölçülüyor" : _lastLatencyMs.HasValue ? _lastProbeHost : "Ölçmek için erişim testi";
            _sessionsMetric.Value.Text = _engine.TlsProxy.ActiveSessions.ToString(); _sessionsMetric.Hint.Text = "Toplam " + _engine.TlsProxy.TotalSessions + " oturum";
            string[] bytes = FormatBytes(_engine.TlsProxy.TotalBytes).Split(' '); _bytesMetric.Value.Text = bytes[0]; _bytesMetric.Unit.Text = bytes[1]; _bytesMetric.Hint.Text = "Yerel TLS proxy üzerinden";
            int engines = (_engine.IsTcpRunning ? 1 : 0) + (_engine.IsVoiceRunning ? 1 : 0); _enginesMetric.Value.Text = engines.ToString(); _enginesMetric.Unit.Text = "/ 2"; _enginesMetric.Hint.Text = active ? "TCP ve UDP ses araçları" : "Başlatılması bekleniyor";
            _tlsChip.Foreground = _engine.TlsProxy.IsRunning ? Green : Muted; _dnsChip.Foreground = _engine.IsTcpRunning ? Green : Muted; _voiceChip.Foreground = _engine.IsVoiceRunning ? Green : Muted;
            _lastTarget.Text = "Son hedef: " + (_engine.TlsProxy.LastHost == "-" ? "henüz yok" : _engine.TlsProxy.LastHost);
            _telemetry.Text = "Aktif TLS oturumu: " + _engine.TlsProxy.ActiveSessions + "   ·   Aktarılan: " + FormatBytes(_engine.TlsProxy.TotalBytes) + "   ·   " + _lastTarget.Text;
            if (_tray != null) { _tray.Text = "Relay — " + (active ? "Araçlar açık" : "Kapalı"); _trayToggle.Text = active ? "Durdur" : "Başlat"; _trayToggle.Enabled = !_busy; }
        }
        private void ClearDnsAsync()
        {
            if (_dnsBusy || _closing) return; _dnsBusy = true; _dnsButton.IsEnabled = false;
            ThreadPool.QueueUserWorkItem(delegate {
                string result;
                try
                {
                    using (Process process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ipconfig.exe"), "/flushdns") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                    {
                        bool exited = process.WaitForExit(5000);
                        result = exited && process.ExitCode == 0 ? "DNS önbelleği temizlendi." : "DNS önbelleği temizlenemedi.";
                    }
                }
                catch (Exception ex) { result = "DNS önbelleği temizlenemedi: " + ex.Message; }
                Post(delegate { AppendLog(result); _dnsBusy = false; _dnsButton.IsEnabled = true; });
            });
        }

        private void RunDiagnosticsAsync()
        {
            if (_probeBusy || _closing) return;
            _probeBusy = true; _lastLatencyMs = null; _lastProbeHost = null; UpdateVisualState();
            foreach (TextBlock state in _probeStates) { state.Text = "Bekliyor…"; state.Foreground = Muted; }
            ThreadPool.QueueUserWorkItem(delegate {
                string[] urls = { "https://updates.discord.com/distributions/app/manifests/latest?channel=stable&platform=win&arch=x64", "https://discord.com/api/v9/experiments", "https://cdn.discordapp.com/" };
                try
                {
                    for (int i = 0; i < urls.Length && !_closing; i++)
                    {
                        int index = i; Post(delegate { _probeStates[index].Text = "Kontrol ediliyor…"; });
                        Stopwatch watch = Stopwatch.StartNew(); int status = 0; string failure = null;
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urls[i]); request.Method = "HEAD"; request.Timeout = 6000; request.ReadWriteTimeout = 6000; request.AllowAutoRedirect = false;
                        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                        using (_diagnosticCancellation.Token.Register(delegate { request.Abort(); }))
                        {
                            try { using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) status = (int)response.StatusCode; }
                            catch (WebException ex) { using (HttpWebResponse response = ex.Response as HttpWebResponse) { if (response != null) status = (int)response.StatusCode; else failure = ex.Status.ToString(); } }
                        }
                        watch.Stop(); int code = status; long elapsed = watch.ElapsedMilliseconds;
                        OnEngineLog(new Uri(urls[i]).Host + (code > 0 ? " → HTTP " + code + " (" + elapsed + " ms)" : " → Yanıt alınamadı: " + failure));
                        string host = new Uri(urls[i]).Host;
                        Post(delegate { if (code > 0) { _lastLatencyMs = elapsed; _lastProbeHost = host; } UpdateVisualState(); _probeStates[index].Text = code > 0 ? "HTTP " + code + " · " + elapsed + " ms" : "Yanıt alınamadı"; _probeStates[index].Foreground = code > 0 ? Green : Red; });
                    }
                }
                catch (Exception ex) { OnEngineLog("Erişim testi tamamlanamadı: " + ex.Message); }
                finally { Post(delegate { _probeBusy = false; UpdateVisualState(); }); }
            });
        }

        private void InitializeTray()
        {
            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Relay'i aç", null, delegate { Post(RestoreFromTray); });
            _trayToggle = new System.Windows.Forms.ToolStripMenuItem("Başlat", null, delegate { Post(ToggleEngineState); }); menu.Items.Add(_trayToggle);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator()); menu.Items.Add("Durdur ve çık", null, delegate { Post(ExitApplication); });
            System.Drawing.Icon icon = System.Drawing.SystemIcons.Application;
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"assets\relay.ico"); if (File.Exists(path)) { try { icon = new System.Drawing.Icon(path); } catch (IOException) { } }
            _tray = new System.Windows.Forms.NotifyIcon { Text = "Relay", Icon = icon, ContextMenuStrip = menu, Visible = true };
            _tray.DoubleClick += delegate { Post(RestoreFromTray); };
        }
        private void RestoreFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }
        private void ExitApplication() { _forceExit = true; Close(); }
        private void OnEngineLog(string message) { Post(delegate { AppendLog(message); }); }
        private void AppendLog(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            _logs.Add(time + "  " + message); if (_logs.Count > 400) _logs.RemoveAt(0);
            string kind = "SYS", detail = "Uygulama"; Brush color = Muted;
            if (message.StartsWith("TLS", StringComparison.OrdinalIgnoreCase)) { kind = "TLS"; color = Green; detail = "Yerel proxy"; }
            else if (message.StartsWith("goodbyedpi.exe", StringComparison.OrdinalIgnoreCase)) { kind = "TCP"; color = Blue; detail = "TCP aracı"; }
            else if (message.StartsWith("winws.exe", StringComparison.OrdinalIgnoreCase)) { kind = "UDP"; color = Blue; detail = "Ses aracı"; }
            if (message.Contains("→ HTTP")) { kind = "HTTP"; color = Green; detail = "HTTPS yanıtı"; }
            if (message.Contains("HATA") || message.Contains("alınamadı") || message.Contains("tamamlanamadı") || message.Contains("Başlatılamadı")) { kind = "ERR"; color = Red; detail = "Günlüğü kontrol et"; }
            _recent.Insert(0, new ActivityItem { Time = time, Kind = kind, Message = message, Detail = detail, Color = color });
            if (_recent.Count > 3) _recent.RemoveAt(3);
            _logBox.Text = string.Join(Environment.NewLine, _logs); _logBox.ScrollToEnd(); UpdateLogLabels();
        }
        private void UpdateLogLabels()
        {
            _logCount.Text = "GÜNLÜK · " + _logs.Count + " KAYIT";
            _recentCount.Text = _logs.Count == 0 ? "İşlem bekleniyor" : "Son " + _recent.Count + " işlem";
            _emptyActivity.Visibility = _recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        private void Post(Action action)
        {
            if (_closing || Dispatcher.HasShutdownStarted) return;
            try { Dispatcher.BeginInvoke(new Action(delegate { if (!_closing) action(); })); } catch (InvalidOperationException) { }
        }
        internal void ShutdownEngine()
        {
            if (_closing) return; _closing = true; _timer.Stop(); _diagnosticCancellation.Cancel(); _engine.Dispose();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_forceExit && _engine.IsRunning)
            {
                MessageBoxResult answer = MessageBox.Show(this, "Relay arka planda çalışmaya devam etsin mi?\n\nEvet: Sistem tepsisine küçültür.\nHayır: Araçları durdurur ve çıkar.", "Relay", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel) { e.Cancel = true; return; }
                if (answer == MessageBoxResult.Yes) { e.Cancel = true; Hide(); return; }
            }
            ShutdownEngine(); base.OnClosing(e);
        }

        private Button Button(string text, string style, Action action)
        {
            Button button = new Button { Content = text, Style = (Style)Resources[style] }; button.Click += delegate { action(); }; AutomationProperties.SetName(button, text); return button;
        }
        private static TextBlock Text(string text, double size, Brush color) { return Text(text, size, color, FontWeights.Normal); }
        private static TextBlock Text(string text, double size, Brush color, FontWeight weight) { return new TextBlock { Text = text, FontSize = size, Foreground = color, FontWeight = weight }; }
        private static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
        private static ScrollViewer Scroll(UIElement content) { return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; }
        private static UIElement Rule() { return new Border { Height = 1, Background = Brush("#202B30"), Margin = new Thickness(0, 16, 0, 16) }; }
        private static CheckBox Option(string title, string description, bool value)
        {
            StackPanel text = new StackPanel(); text.Children.Add(Text(title, 14, Light, FontWeights.SemiBold));
            TextBlock note = Text(description, 13, Muted); note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 6, 0, 0); text.Children.Add(note);
            CheckBox check = new CheckBox { Content = text, IsChecked = value, HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = 54 };
            AutomationProperties.SetName(check, title); return check;
        }
        private static string FormatBytes(long bytes) { return bytes >= 1048576 ? (bytes / 1048576.0).ToString("0.0") + " MB" : (bytes / 1024.0).ToString("0") + " KB"; }
    }
}
