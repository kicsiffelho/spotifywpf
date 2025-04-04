using SpotifyAPI.Web;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace SpotifyControl
{
    public partial class MainWindow : Window
    {
        private SpotifyClient _spotify;

        public MainWindow()
        {
            InitializeComponent();
            StartOAuth();
        }

        private async void StartOAuth()
        {
            try
            {
                var (verifier, challenge) = PKCEUtil.GenerateCodes();

                var loginRequest = new LoginRequest(
                    new Uri("http://localhost:8888/callback"),
                    "490cf3365f5d45ce95a9ddc65951e3f9",
                    LoginRequest.ResponseType.Code
                )
                {
                    CodeChallengeMethod = "S256",
                    CodeChallenge = challenge,
                    Scope = new[]
                    {
                        Scopes.UserReadPlaybackState,
                        Scopes.UserModifyPlaybackState,
                        Scopes.UserReadCurrentlyPlaying
                    }
                };

                var uri = loginRequest.ToUri();
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.ToString(),
                    UseShellExecute = true
                });

                var http = new HttpListener();
                http.Prefixes.Add("http://localhost:8888/callback/");
                http.Start();

                var context = await http.GetContextAsync();
                var code = context.Request.QueryString.Get("code");

                var responseString = "<html><body><h2>You may now close this window.</h2></body></html>";
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
                http.Stop();

                var tokenRequest = new PKCETokenRequest(
                    "490cf3365f5d45ce95a9ddc65951e3f9",
                    code,
                    new Uri("http://localhost:8888/callback"),
                    verifier
                );

                var tokenResponse = await new OAuthClient().RequestToken(tokenRequest);
                var config = SpotifyClientConfig.CreateDefault().WithToken(tokenResponse.AccessToken);
                _spotify = new SpotifyClient(config);

                OpenSpotify();

                await ShowCurrentTrack();

                Dispatcher.Invoke(() => StatusText.Text = "🎧 Connected to Spotify!");
                await Task.Delay(3000);
                Dispatcher.Invoke(() => StatusText.Text = "");
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Authentication failed: " + ex.Message);
            }
        }

        private async Task ShowCurrentTrack()
        {
            if (_spotify == null)
            {
                MessageBox.Show("❌ Not connected to Spotify.");
                return;
            }

            var currentlyPlaying = await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
            var track = currentlyPlaying?.Item as FullTrack;

            if (track != null)
            {
                var title = track.Name;
                var artist = track.Artists.FirstOrDefault()?.Name;
                var imageUrl = track.Album.Images.FirstOrDefault()?.Url;

                SongName.Text = $"{title}";
                ArtistName.Text = $"by {artist}";

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    AlbumCover.Source = new BitmapImage(new Uri(imageUrl));
                }
            }
        }

        private async void TogglePlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_spotify == null) return;
            var playback = await _spotify.Player.GetCurrentPlayback();
            if (playback?.IsPlaying == true)
            {
                await _spotify.Player.PausePlayback();
                PausePlayImg.Source = new BitmapImage(new Uri("Assets/play.png", UriKind.Relative));
            }
            else
            {
                await _spotify.Player.ResumePlayback();
                PausePlayImg.Source = new BitmapImage(new Uri("Assets/pause.png", UriKind.Relative));
            }
            await ShowCurrentTrack();
        }

        private async void NextTrack_Click(object sender, RoutedEventArgs e)
        {
            if (_spotify == null) return;
            await _spotify.Player.SkipNext();
            await Task.Delay(500);
            await ShowCurrentTrack();

        }

        private async void PreviousTrack_Click(object sender, RoutedEventArgs e)
        {
            if (_spotify == null) return;
            await _spotify.Player.SkipPrevious();
            await Task.Delay(500);
            await ShowCurrentTrack();

        }

        private void mainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void mainWindow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }
        private void OpenSpotify()
        {
            try 
            {

                Process.Start("C:\\Users\\kicsi\\AppData\\Roaming\\Spotify\\Spotify.exe");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open Spotify: " + ex.Message);
            }
        }
    }
}
