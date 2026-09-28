using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Box86_WinUI.Services;

namespace Box86_WinUI.Views
{
    /// <summary>
    /// First-run OOBE-style onboarding: 3 steps, each split into a left column (title, body,
    /// per-slide primary action) and a right column (hero graphic or, on slide 2, real app
    /// preferences). Steps crossfade in place (Slide1/Slide2/Slide3 are stacked Grid children of
    /// TourHost) rather than sliding, driven by each slide's own action button or the fixed bottom
    /// command bar (Home / Prev / Next).
    /// </summary>
    public sealed partial class WelcomeDialog : ContentDialog
    {
        private readonly ConfigService _configService = new();
        private FrameworkElement[] _slides = System.Array.Empty<FrameworkElement>();
        private int _currentIndex;

        public WelcomeDialog()
        {
            InitializeComponent();

            _slides = new FrameworkElement[] { Slide1, Slide2, Slide3 };

            DriveSoundsToggle.IsOn = _configService.GetDriveSeekSounds();
            CrtFilterToggle.IsOn = _configService.GetCrtFilterMode() != 0;

            UpdateNavButtons();
        }

        private void NavigateTo(int index)
        {
            if (index < 0 || index >= _slides.Length || index == _currentIndex)
                return;

            var from = _slides[_currentIndex];
            var to = _slides[index];

            to.Visibility = Visibility.Visible;
            to.Opacity = 0;

            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var fadeOut = new DoubleAnimation { From = 1.0, To = 0.0, Duration = new Duration(System.TimeSpan.FromMilliseconds(200)), EasingFunction = ease };
            var fadeIn = new DoubleAnimation { From = 0.0, To = 1.0, Duration = new Duration(System.TimeSpan.FromMilliseconds(200)), EasingFunction = ease };
            Storyboard.SetTarget(fadeOut, from);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");
            Storyboard.SetTarget(fadeIn, to);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(fadeOut);
            storyboard.Children.Add(fadeIn);
            storyboard.Completed += (s, e) =>
            {
                from.Visibility = Visibility.Collapsed;
                _currentIndex = index;
                UpdateNavButtons();
            };
            storyboard.Begin();
        }

        private void UpdateNavButtons()
        {
            PrevButton.IsEnabled = _currentIndex > 0;
            NextButton.IsEnabled = _currentIndex < _slides.Length - 1;
        }

        private void OnAdvanceClicked(object sender, RoutedEventArgs e) => NavigateTo(_currentIndex + 1);

        private void OnFinishClicked(object sender, RoutedEventArgs e) => Hide();

        private void OnHomeClicked(object sender, RoutedEventArgs e) => NavigateTo(0);

        private void OnPrevClicked(object sender, RoutedEventArgs e) => NavigateTo(_currentIndex - 1);

        private void OnNextClicked(object sender, RoutedEventArgs e) => NavigateTo(_currentIndex + 1);

        private void OnDriveSoundsToggled(object sender, RoutedEventArgs e)
        {
            // DriveSoundService.Enabled is a lazily-cached static (set once from config the first
            // time any drive activity fires); saving to config alone doesn't touch that cache, so
            // without this the toggle would silently do nothing until the app restarts.
            _configService.SaveDriveSeekSounds(DriveSoundsToggle.IsOn);
            DriveSoundService.Enabled = DriveSoundsToggle.IsOn;
        }

        private void OnCrtFilterToggled(object sender, RoutedEventArgs e)
        {
            int mode = CrtFilterToggle.IsOn ? 1 : 0;
            _configService.SaveCrtFilterMode(mode);
            Box86_WinUI.Interop.Box86CoreNative.EmuSetCrtFilterMode(mode);
        }
    }
}
