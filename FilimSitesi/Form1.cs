using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;

namespace FilimSitesi
{
    public partial class Form1 : Form
    {
        private Dictionary<string, Movie> _movies = new();
        private HashSet<string> _favorites = new();
        private Dictionary<string, Image> _posterCache = new();
        private Dictionary<string, List<string>> _comments = new();
        private HttpClient _http = new();

        public Form1()
        {
            InitializeComponent();
        }

        private Image GetPosterImage(Movie movie)
        {
            if (movie == null) return null;
            // prefer cached poster only (do not perform blocking network IO here)
            if (!string.IsNullOrEmpty(movie.PosterPath) && _posterCache.TryGetValue(movie.PosterPath, out var cached))
                return cached;

            // fallback: try designer field
            try
            {
                var field = this.GetType().GetField(movie.PanelName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null && field.GetValue(this) is Panel dp && dp.BackgroundImage != null) return dp.BackgroundImage;
            }
            catch { }

            // fallback: resource lookup
            try
            {
                var res = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
                var obj = res.GetObject(movie.PanelName + ".BackgroundImage");
                if (obj is Image im) return im;
            }
            catch { }

            return null;
        }

        private async Task<Image> DownloadAndCachePosterAsync(string posterPath)
        {
            if (string.IsNullOrEmpty(posterPath)) return null;
            if (_posterCache.TryGetValue(posterPath, out var cached)) return cached;
            try
            {
                if (posterPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var data = await _http.GetByteArrayAsync(posterPath);
                    using var ms = new MemoryStream(data);
                    var img = Image.FromStream(ms);
                    _posterCache[posterPath] = img;
                    return img;
                }
                else if (File.Exists(posterPath))
                {
                    var img = Image.FromFile(posterPath);
                    _posterCache[posterPath] = img;
                    return img;
                }
            }
            catch { }
            return null;
        }

        private async Task EnsurePosterLoadedAsync(Movie movie, Control target)
        {
            if (movie == null || target == null) return;
            if (string.IsNullOrEmpty(movie.PosterPath)) return;
            try
            {
                var img = await DownloadAndCachePosterAsync(movie.PosterPath);
                if (img != null)
                {
                    if (target.IsHandleCreated)
                        target.BeginInvoke(() => target.BackgroundImage = img);
                }
            }
            catch { }
        }

        private TabPage OpenOrSelectMovieTab(Movie movie, Image posterImage)
        {
            if (movie == null) return null;
            // find existing tab by Tag (PanelName)
            foreach (TabPage t in tabControl1.TabPages)
            {
                if (t.Tag is string tag && tag == movie.PanelName)
                {
                    tabControl1.SelectedTab = t;
                    return t;
                }
            }

            var newTab = CreateMovieTab(movie, posterImage ?? GetPosterImage(movie));
            newTab.Tag = movie.PanelName;
            tabControl1.TabPages.Add(newTab);
            tabControl1.SelectedTab = newTab;
            return newTab;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // load movie metadata from movies.json (located next to exe)
            try
            {
                var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "movies.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    var list = JsonSerializer.Deserialize<List<Movie>>(json);
                    if (list != null)
                        _movies = list.Where(m => !string.IsNullOrEmpty(m.PanelName)).ToDictionary(m => m.PanelName);
                }
            }
            catch
            {
                // ignore parse errors; fallback to empty
            }

            LoadFavorites();

            LoadComments();

            // Keep Trending and Search tabs, but do not replace the designer-built "Keşfet" tab.
            BuildTrendingTab();
            SetupSearchTab();

            // Set labels under posters on Keşfet from loaded movie data
            try
            {
                if (_movies != null)
                {
                    if (_movies.TryGetValue("panel1", out var m1)) labelPanel1.Text = m1.Title;
                    if (_movies.TryGetValue("panel2", out var m2)) labelPanel2.Text = m2.Title;
                    if (_movies.TryGetValue("panel3", out var m3)) labelPanel3.Text = m3.Title;
                    if (_movies.TryGetValue("panel4", out var m4)) labelPanel4.Text = m4.Title;
                    if (_movies.TryGetValue("panel5", out var m5)) labelPanel5.Text = m5.Title;
                    if (_movies.TryGetValue("panel6", out var m6)) labelPanel6.Text = m6.Title;
                    if (_movies.TryGetValue("panel7", out var m7)) labelPanel7.Text = m7.Title;
                    if (_movies.TryGetValue("panel8", out var m8)) labelPanel8.Text = m8.Title;
                    if (_movies.TryGetValue("panel9", out var m9)) labelPanel9.Text = m9.Title;
                    if (_movies.TryGetValue("panel10", out var m10)) labelPanel10.Text = m10.Title;
                    if (_movies.TryGetValue("panel11", out var m11)) labelPanel11.Text = m11.Title;
                    if (_movies.TryGetValue("panel12", out var m12)) labelPanel12.Text = m12.Title;
                    if (_movies.TryGetValue("panel13", out var m13)) labelPanel13.Text = m13.Title;
                    if (_movies.TryGetValue("panel14", out var m14)) labelPanel14.Text = m14.Title;
                    if (_movies.TryGetValue("panel15", out var m15)) labelPanel15.Text = m15.Title;
                    if (_movies.TryGetValue("panel16", out var m16)) labelPanel16.Text = m16.Title;
                }
            }
            catch { }
        }

        private void LoadComments()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "comments.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var dic = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    if (dic != null) _comments = dic;
                }
            }
            catch { }
        }

        private void SaveComments()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "comments.json");
                var json = JsonSerializer.Serialize(_comments);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private void LoadFavorites()
        {
            try
            {
                var fpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.json");
                if (File.Exists(fpath))
                {
                    var json = File.ReadAllText(fpath);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null) _favorites = new HashSet<string>(list);
                }
            }
            catch { }
        }

        private void SaveFavorites()
        {
            try
            {
                var fpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.json");
                var json = JsonSerializer.Serialize(_favorites.ToList());
                File.WriteAllText(fpath, json);
            }
            catch { }
        }

        private void BuildDiscoverFlow()
        {
            // create a flow layout and populate it with movies
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = tabPage1.BackColor,
                Padding = new Padding(20),
                WrapContents = true
            };

            foreach (var movie in _movies.Values)
            {
                var item = CreateDiscoverItem(movie);
                flow.Controls.Add(item);
            }

            tabPage1.Controls.Clear();
            tabPage1.Controls.Add(flow);
        }

        private Control CreateDiscoverItem(Movie movie)
        {
            var container = new Panel { Width = 200, Height = 340, Margin = new Padding(10) };

            Image img = null;
            // 1) try to copy image from existing designer panel field if available
            try
            {
                var field = this.GetType().GetField(movie.PanelName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null && field.GetValue(this) is Panel designerPanel && designerPanel.BackgroundImage != null)
                    img = designerPanel.BackgroundImage;
            }
            catch { }

            // 2) if not found, try to load the same resource used by the designer: "panelX.BackgroundImage"
            if (img == null)
            {
                try
                {
                    var res = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
                    var obj = res.GetObject(movie.PanelName + ".BackgroundImage");
                    if (obj is Image im) img = im;
                }
                catch { }
            }

            // try to get poster (supports PosterPath URL/file, designer resource fallback)
            var posterImage = GetPosterImage(movie);

            var poster = new Panel
            {
                Width = 183,
                Height = 280,
                BackgroundImage = posterImage ?? img,
                BackgroundImageLayout = ImageLayout.Stretch,
                Cursor = Cursors.Hand,
                Name = movie.PanelName
            };
            poster.Click += panelPoster_Click;
            // start async load of poster (if PosterPath provided) and update control when done
            _ = EnsurePosterLoadedAsync(movie, poster);

            var title = new Label
            {
                Text = movie.Title,
                ForeColor = Color.White,
                Width = 183,
                Location = new Point(0, 285)
            };

            container.Controls.Add(poster);
            container.Controls.Add(title);
            return container;
        }

        private void BuildTrendingTab()
        {
            tabPage3.Controls.Clear();

            if (_movies == null || _movies.Count == 0)
            {
                var lbl = new Label { Text = "Trend verisi yok.", ForeColor = Color.White, AutoSize = true, Location = new Point(20, 20) };
                tabPage3.Controls.Add(lbl);
                return;
            }

            // pick a featured movie (random among top-rated) and show it large at the top
            var rng = new Random();
            var topRated = _movies.Values.OrderByDescending(m => m.Rating).Take(Math.Min(6, _movies.Count)).ToList();
            var featured = topRated[rng.Next(topRated.Count)];

            var featuredPanel = new Panel { Location = new Point(20, 20), Size = new Size(tabPage3.Width - 80, 260), BackColor = Color.Black };
            var featuredPoster = new Panel { Size = new Size(380, 240), Location = new Point(10, 10), BackgroundImage = null, BackgroundImageLayout = ImageLayout.Stretch };
            // try to set any cached/designer poster immediately, then load from PosterPath asynchronously
            try { featuredPoster.BackgroundImage = GetPosterImage(featured); } catch { }
            _ = EnsurePosterLoadedAsync(featured, featuredPoster);

            var ftTitle = new Label { Text = featured.Title, Font = new Font("Segoe UI", 20F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(410, 20), AutoSize = true };
            var ftRating = new Label { Text = $"Puan: {featured.Rating:0.0}/10", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.Gold, Location = new Point(410, 70), AutoSize = true };
            var ftDesc = new Label { Text = featured.Description, Font = new Font("Segoe UI", 10F), ForeColor = Color.White, Location = new Point(410, 100), Size = new Size(featuredPanel.Width - 430, 120) };
            var ftTrailer = new Button { Text = "Fragmanı İzle", Location = new Point(410, 220), Size = new Size(120, 30) };
            ftTrailer.Click += (s, e) => { if (!string.IsNullOrEmpty(featured.TrailerUrl)) Process.Start(new ProcessStartInfo { FileName = featured.TrailerUrl, UseShellExecute = true }); };
            var ftOpen = new Button { Text = "Detayları Aç", Location = new Point(540, 220), Size = new Size(120, 30) };
            ftOpen.Click += (s, e) => { OpenOrSelectMovieTab(featured, featuredPoster.BackgroundImage); };

            featuredPanel.Controls.Add(featuredPoster);
            featuredPanel.Controls.Add(ftTitle);
            featuredPanel.Controls.Add(ftRating);
            featuredPanel.Controls.Add(ftDesc);
            featuredPanel.Controls.Add(ftTrailer);
            featuredPanel.Controls.Add(ftOpen);

            tabPage3.Controls.Add(featuredPanel);

            // below featured, show a shuffled grid of other trending items
            var flow = new FlowLayoutPanel { Location = new Point(20, 300), Size = new Size(tabPage3.Width - 60, tabPage3.Height - 340), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AutoScroll = true, BackColor = tabPage3.BackColor, Padding = new Padding(10) };

            var others = _movies.Values.Where(m => m.PanelName != featured.PanelName).ToList();
            // mix them to create a trending feel
            others = others.OrderBy(x => rng.Next()).ToList();

            foreach (var movie in others)
            {
                var item = CreateDiscoverItem(movie);
                // add a small rating badge overlay
                var badge = new Label { Text = movie.Rating > 0 ? movie.Rating.ToString("0.0") : "-", BackColor = Color.FromArgb(180, Color.Black), ForeColor = Color.Gold, AutoSize = true, Location = new Point(item.Width - 40, 5) };
                item.Controls.Add(badge);
                flow.Controls.Add(item);
            }

            tabPage3.Controls.Add(flow);
        }

        private FlowLayoutPanel _searchResultsPanel;

        private void SetupSearchTab()
        {
            tabPage4.Controls.Clear();

            var txt = new TextBox { Location = new Point(20, 20), Width = 400, Name = "txtSearch" };
            var btn = new Button { Location = new Point(430, 18), Text = "Ara", Size = new Size(80, 26) };
            btn.Click += (s, e) => DoSearch(txt.Text);
            txt.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { DoSearch(txt.Text); e.Handled = true; e.SuppressKeyPress = true; } };

            _searchResultsPanel = new FlowLayoutPanel { Location = new Point(20, 60), Size = new Size(tabPage4.Width - 40, tabPage4.Height - 80), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AutoScroll = true, BackColor = tabPage4.BackColor, FlowDirection = FlowDirection.TopDown, WrapContents = false };

            tabPage4.Controls.Add(txt);
            tabPage4.Controls.Add(btn);
            tabPage4.Controls.Add(_searchResultsPanel);
        }

        private void DoSearch(string query)
        {
            _searchResultsPanel.Controls.Clear();
            if (string.IsNullOrWhiteSpace(query)) return;
            var q = query.Trim().ToLowerInvariant();
            var results = _movies.Values.Where(m => (m.Title != null && m.Title.ToLowerInvariant().Contains(q)) || (m.Cast != null && m.Cast.Any(c => c.ToLowerInvariant().Contains(q)))).ToList();

            if (!results.Any())
            {
                var none = new Label { Text = "Sonuç bulunamadı.", ForeColor = Color.White, AutoSize = true };
                _searchResultsPanel.Controls.Add(none);
                return;
            }

            // show text list of movie titles (clickable) and a small poster thumbnail
            foreach (var movie in results)
            {
                var item = new Panel { Width = _searchResultsPanel.Width - 25, Height = 80, BackColor = Color.FromArgb(40, 40, 40), Margin = new Padding(3) };
                item.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

                Image img = null;
                try { var f = this.GetType().GetField(movie.PanelName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); if (f != null && f.GetValue(this) is Panel dp && dp.BackgroundImage != null) img = dp.BackgroundImage; }
                catch { }
                if (img == null)
                {
                    try { var res = new System.ComponentModel.ComponentResourceManager(typeof(Form1)); var obj = res.GetObject(movie.PanelName + ".BackgroundImage"); if (obj is Image im) img = im; } catch { }
                }

                var thumb = new Panel { Size = new Size(60, 80), Location = new Point(5, 0), BackgroundImage = GetPosterImage(movie) ?? img, BackgroundImageLayout = ImageLayout.Stretch, Cursor = Cursors.Hand };
                thumb.Click += (s, e) => { OpenOrSelectMovieTab(movie, null); };
                _ = EnsurePosterLoadedAsync(movie, thumb);

                var title = new Label { Text = movie.Title, ForeColor = Color.White, Location = new Point(75, 10), AutoSize = false, Width = item.Width - 160, Height = 30 };
                var meta = new Label { Text = $"Puan: {movie.Rating:0.0}/10", ForeColor = Color.Gold, Location = new Point(75, 40), AutoSize = true };

                // clicking the whole item opens details
                item.Click += (s, e) => { OpenOrSelectMovieTab(movie, img); };
                foreach (Control c in new Control[] { thumb, title, meta }) { c.Click += (s, e) => { OpenOrSelectMovieTab(movie, img); }; }

                item.Controls.Add(thumb);
                item.Controls.Add(title);
                item.Controls.Add(meta);
                _searchResultsPanel.Controls.Add(item);
            }
        }

        private void panel3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel6_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void panelPoster_Click(object? sender, EventArgs e)
        {
            // sender may be the inner poster Panel or the container Panel; ensure we get the panel with Name
            Panel p = null;
            if (sender is Panel sp) p = sp;
            else if (sender is Control c)
            {
                // maybe clicked label or container
                p = c.Controls.OfType<Panel>().FirstOrDefault();
            }
            if (p == null) return;

            _movies.TryGetValue(p.Name, out var movie);

            // Update the existing details tab (tabPage2)
            tabPage2.Text = movie?.Title ?? p.Name;
            panel25.BackgroundImage = p.BackgroundImage;
            label2.Text = movie?.Title ?? "Film Başlığı";
            label1.Text = movie?.Description ?? "Açıklama yok.";

            // create and open a new tab for this movie with richer UI
            OpenOrSelectMovieTab(movie, p.BackgroundImage);
        }

        private TabPage CreateMovieTab(Movie movie, Image posterImage)
        {
            var title = movie?.Title ?? "Film Detayı";
            var newTab = new TabPage(title) { BackColor = tabPage2.BackColor };

            var poster = new Panel
            {
                BackgroundImage = posterImage ?? GetPosterImage(movie),
                BackgroundImageLayout = ImageLayout.Stretch,
                Location = new Point(20, 20),
                Size = new Size(300, 420)
            };
            _ = EnsurePosterLoadedAsync(movie, poster);

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(340, 30),
                AutoSize = true
            };

            var ratingLabel = new Label
            {
                Text = movie != null ? $"Puan: {movie.Rating:0.0}/10" : "Puan: -",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.Gold,
                Location = new Point(340, 70),
                AutoSize = true
            };

            var descLabel = new Label
            {
                Text = movie?.Description ?? "Açıklama yok.",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.White,
                Location = new Point(340, 100),
                Size = new Size(900, 140)
            };

            var castHeader = new Label
            {
                Text = "Oyuncular:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(340, 260),
                AutoSize = true
            };

            var castLabel = new Label
            {
                Text = movie != null && movie.Cast != null ? string.Join(", ", movie.Cast) : "Bilgi yok.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.White,
                Location = new Point(340, 290),
                Size = new Size(900, 120)
            };

            var trailerBtn = new Button
            {
                Text = "Fragmanı Dışarıda Aç",
                Location = new Point(340, 420),
                Size = new Size(160, 35)
            };
            trailerBtn.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(movie?.TrailerUrl))
                {
                    try { Process.Start(new ProcessStartInfo { FileName = movie.TrailerUrl, UseShellExecute = true }); }
                    catch { MessageBox.Show("Fragman açılamadı."); }
                }
            };

            var embedBtn = new Button { Text = "Fragmanı Uygulamada Aç", Location = new Point(510, 420), Size = new Size(180, 35) };

            WebView2 webView = null;
            embedBtn.Click += async (s, e) =>
            {
                if (movie == null || string.IsNullOrEmpty(movie.TrailerUrl)) return;
                try
                {
                    if (webView == null)
                    {
                        webView = new WebView2 { Location = new Point(340, 470), Size = new Size(900, 400) };
                        newTab.Controls.Add(webView);
                        await webView.EnsureCoreWebView2Async();
                    }
                    webView.CoreWebView2.Navigate(movie.TrailerUrl);
                }
                catch
                {
                    MessageBox.Show("Yerel oynatıcı başlatılamadı. WebView2 runtime yüklü olmayabilir.");
                }
            };

            var favBtn = new Button
            {
                Text = _favorites.Contains(movie?.PanelName) ? "Favoriden Kaldır" : "Favorilere Ekle",
                Location = new Point(700, 420),
                Size = new Size(140, 35)
            };
            favBtn.Click += (s, e) =>
            {
                if (movie == null) return;
                if (_favorites.Contains(movie.PanelName))
                {
                    _favorites.Remove(movie.PanelName);
                    favBtn.Text = "Favorilere Ekle";
                }
                else
                {
                    _favorites.Add(movie.PanelName);
                    favBtn.Text = "Favoriden Kaldır";
                }
                SaveFavorites();
            };

            // Similar movies
            var similarHeader = new Label { Text = "Benzer Filmler:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(20, 460), AutoSize = true };
            var similarFlow = new FlowLayoutPanel { Location = new Point(20, 490), Size = new Size(300, 200), AutoScroll = true, FlowDirection = FlowDirection.LeftToRight };
            if (movie?.Similar != null)
            {
                foreach (var sim in movie.Similar)
                {
                    Movie simMovie = null;
                    // try by panel name
                    if (_movies.TryGetValue(sim, out var m)) simMovie = m;
                    else simMovie = _movies.Values.FirstOrDefault(x => x.Title == sim);
                    if (simMovie == null) continue;

                    var simImg = GetPosterImage(simMovie);
                    var simPanel = new Panel { Size = new Size(90, 120), BackgroundImage = simImg, BackgroundImageLayout = ImageLayout.Stretch, Cursor = Cursors.Hand, Tag = simMovie };
                    simPanel.Click += (s, e) => { OpenOrSelectMovieTab((Movie)((Panel)s).Tag, null); };
                    _ = EnsurePosterLoadedAsync(simMovie, simPanel);
                    similarFlow.Controls.Add(simPanel);
                }
            }

            // Comments
            var commentsHeader = new Label { Text = "Yorumlar:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(340, 500), AutoSize = true };
            var commentsFlow = new FlowLayoutPanel { Location = new Point(340, 530), Size = new Size(900, 220), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(30, 30, 30) };
            if (movie != null && _comments.TryGetValue(movie.PanelName, out var cmts))
            {
                foreach (var c in cmts) commentsFlow.Controls.Add(new Label { Text = c, ForeColor = Color.White, AutoSize = false, Width = commentsFlow.Width - 25 });
            }

            var commentBox = new TextBox { Location = new Point(340, 760), Size = new Size(700, 30) };
            var commentBtn = new Button { Text = "Yorum Ekle", Location = new Point(1050, 760), Size = new Size(120, 30) };
            commentBtn.Click += (s, e) =>
            {
                if (movie == null) return;
                var text = commentBox.Text?.Trim();
                if (string.IsNullOrEmpty(text)) return;
                if (!_comments.ContainsKey(movie.PanelName)) _comments[movie.PanelName] = new List<string>();
                _comments[movie.PanelName].Add(text);
                commentsFlow.Controls.Add(new Label { Text = text, ForeColor = Color.White, AutoSize = false, Width = commentsFlow.Width - 25 });
                commentBox.Text = string.Empty;
                SaveComments();
            };

            var closeBtn = new Button { Text = "Kapat", Location = new Point(1200, 10), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            closeBtn.Click += (s, e) => { tabControl1.TabPages.Remove(newTab); };

            newTab.Controls.Add(poster);
            newTab.Controls.Add(titleLabel);
            newTab.Controls.Add(ratingLabel);
            newTab.Controls.Add(descLabel);
            newTab.Controls.Add(castHeader);
            newTab.Controls.Add(castLabel);
            newTab.Controls.Add(trailerBtn);
            newTab.Controls.Add(embedBtn);
            newTab.Controls.Add(favBtn);
            newTab.Controls.Add(similarHeader);
            newTab.Controls.Add(similarFlow);
            newTab.Controls.Add(commentsHeader);
            newTab.Controls.Add(commentsFlow);
            newTab.Controls.Add(commentBox);
            newTab.Controls.Add(commentBtn);
            newTab.Controls.Add(closeBtn);

            return newTab;
        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void labelPanel1_Click(object sender, EventArgs e)
        {

        }
    }
}
