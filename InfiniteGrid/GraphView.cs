#nullable disable
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InfiniteGrid
{
    // Klasör ağacını graf olarak çizen, sonsuz kaydırılabilir kontrol. Modlar (F1-F5):
    //   F1 BFS (kuyruk)   F2 DFS (yığın)   F3 En kısa yol (A -> B, BFS)
    //   F4 AVL ağacıyla ada göre arama     F5 Klasör boyutları (max-heap ile en büyük 10)
    public class GraphView : FrameworkElement
    {
        private class Node
        {
            public string Name, Path;
            public int Parent = -1, Depth;
            public List<int> Kids = new(), Links = new();
            public double X, Y;
            public long Own, Total;
            public string[] Files;
            public double Dx, Dy;          // gölgenin ittiği kayma
            public bool InShadow;
            public int Island;
            public FormattedText Label;
        }

        private class Island { public string Name; public int Style; public double CX, CY, R; public FormattedText Label; }
        private static readonly string[] StyleNames = { "radyal ağaç", "spiral", "halkalar", "ızgara" };
        private readonly List<Island> _islands = new();
        private const int IslandCap = 300;
        private int _cap = int.MaxValue;
        private bool _fit;
        private static readonly Pen IslandPen = MakePen(Color.FromArgb(34, 150, 170, 220), 1.5);
        private static readonly string[] ModeNames = { "BFS", "DFS", "Yol", "Ara", "Boyut", "Kopya" };
        private readonly List<Node> _nodes = new();
        private readonly List<(int a, int b)> _edges = new();
        private readonly Graph _graph = new();
        private readonly AvlTree _avl = new();
        private readonly HashSet<int> _hl = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Random _rng = new();

        // kamera
        private double _ox, _oy, _z = 1, _vx, _vy, _moved, _lastSave, _maxTotal = 1;
        private bool _drag;
        private Point _last, _mouse = new(-1e4, -1e4);
        private int _hover = -1;
        private double _minX, _maxX, _minY, _maxY;
        private readonly List<(int a, int b)> _links = new();
        private static readonly string CamFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileGraphWallpaper", "cam.txt");

        // mod ve animasyon durumu
        private int _mode, _runColor, _pathA = -1;
        private string _query = "", _info = "";
        private bool _hasRun;
        private double _t0, _step = 300;
        private int[] _ord = Array.Empty<int>(), _idx = Array.Empty<int>(), _from = Array.Empty<int>();

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private static Pen MakePen(Color c, double w) { var p = new Pen(Frozen(c), w); p.Freeze(); return p; }
        private static readonly Brush Bg = Frozen(Color.FromRgb(0x0b, 0x10, 0x20));
        private static readonly Brush NodeBrush = Frozen(Color.FromRgb(0x7c, 0x93, 0xd6));
        private static readonly Brush PinkBrush = Frozen(Color.FromRgb(0xf4, 0x72, 0xb6));
        private static readonly Brush LabelBrush = Frozen(Color.FromArgb(204, 220, 230, 255));
        private static readonly Brush PathBrush = Frozen(Color.FromRgb(226, 232, 240));
        private static readonly Pen GridPen = MakePen(Color.FromArgb(15, 120, 150, 255), 1);
        private static readonly Pen EdgePen = MakePen(Color.FromArgb(64, 124, 147, 214), 1.2);
        private static readonly Pen PinkPen = MakePen(Color.FromArgb(160, 0xf4, 0x72, 0xb6), 2);
        // BFS, DFS, yol, (arama/boyut)
        private static readonly Color[] Cols =
        {
            Color.FromRgb(0x38, 0xbd, 0xf8), Color.FromRgb(0xfb, 0x92, 0x3c),
            Color.FromRgb(0xfa, 0xcc, 0x15), Color.FromRgb(0xf4, 0x72, 0xb6)
        };

        private static Brush MakeGrad(Color a, Color b) { var g = new LinearGradientBrush(a, b, 90); g.Freeze(); return g; }
        private static Color Mix(Color a, Color b, double k) =>
            Color.FromRgb((byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k));
        private static readonly Brush FolderBack = Frozen(Color.FromRgb(0xE0, 0xA1, 0x1F));
        private static readonly Brush FolderFront = MakeGrad(Color.FromRgb(0xFF, 0xD7, 0x6A), Color.FromRgb(0xFB, 0xB9, 0x2B));
        private static readonly Pen HoverPen = MakePen(Color.FromArgb(200, 255, 255, 255), 1.5);

        private static readonly Pen LinkPen = MakePen(Color.FromArgb(160, 0xc0, 0x84, 0xfc), 1.5);
        private static readonly Brush MapBg = Frozen(Color.FromArgb(150, 15, 23, 42));
        private static readonly Pen MapPen = MakePen(Color.FromArgb(120, 124, 147, 214), 1);
        private static readonly Pen MapViewPen = MakePen(Color.FromArgb(220, 250, 204, 21), 1.2);

        private string _hoverFile;
        private QuadTree _qt;
        private ShadowCamera _cam;
        private WriteableBitmap _shadow;
        private readonly byte[] _maskBuf = new byte[ShadowCamera.W * ShadowCamera.H];
        private readonly byte[] _pix = new byte[ShadowCamera.W * ShadowCamera.H * 4];
        private static readonly Brush Floor = Frozen(Color.FromRgb(0x3b, 0x41, 0x4f));
        private int _drawn;
        private List<int> _seen;
        private static readonly Pen FileLinkPen = MakePen(Color.FromArgb(40, 150, 170, 220), 1);
        private static readonly Brush[] FileBrushes = new[] { "#f87171", "#fb923c", "#facc15", "#4ade80", "#2dd4bf", "#60a5fa", "#a78bfa", "#f472b6" }
            .Select(h => Frozen((Color)ColorConverter.ConvertFromString(h))).ToArray();
        private static int ExtIdx(string ext) => Math.Abs(ext.Aggregate(7, (h, c) => h * 31 + c)) % FileBrushes.Length;
        private static string[] ListFiles(string path)
        {
            try { return Directory.EnumerateFiles(path).Take(60).ToArray(); }
            catch { return Array.Empty<string>(); }
        }

        public GraphView()
        {
            ClipToBounds = true;
            Focusable = true;
            LoadCam();
            CompositionTarget.Rendering += (s, e) => Tick();
            Unloaded += (s, e) => { _cam?.Dispose(); _cam = null; };
        }

        private void LoadCam()
        {
            try
            {
                string[] p = File.ReadAllText(CamFile).Split(';');
                _ox = double.Parse(p[0], CultureInfo.InvariantCulture);
                _oy = double.Parse(p[1], CultureInfo.InvariantCulture);
                _z = double.Parse(p[2], CultureInfo.InvariantCulture);
            }
            catch { }
        }

        private void SaveCam()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CamFile));
                File.WriteAllText(CamFile, string.Join(";", new[] { _ox, _oy, _z }.Select(v => v.ToString(CultureInfo.InvariantCulture))));
            }
            catch { }
        }

        // ---------- Veriyi kur: FolderTree -> Graph + AVL ----------
        public void Load(string rootPath, int depth)
        {
            Add(FolderTree.Build(rootPath, depth), -1, 0);
            Layout();
            ComputeSizes();
            AddShortcutLinks();
            _minX = _nodes.Min(n => n.X); _maxX = _nodes.Max(n => n.X);
            _minY = _nodes.Min(n => n.Y); _maxY = _nodes.Max(n => n.Y);
        }

        // Her klasör ayrı bir "ada" (ayrı kök) olur; adalar kendi yerleşim biçimiyle ızgara üzerinde dizilir.
        public void LoadHome(string[] folders)
        {
            var starts = new List<int>();
            foreach (string f in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                FolderNode tree = FolderTree.Build(f, 3);
                if (CountNodes(tree) < 3) continue;          // tek klasörlük adalar atlanır
                starts.Add(_nodes.Count);
                _cap = _nodes.Count + IslandCap;
                Add(tree, -1, 0);
            }
            if (starts.Count == 0) return;
            LayoutIslands(starts);
            ComputeSizes();
            AddShortcutLinks();
            _minX = _nodes.Min(n => n.X); _maxX = _nodes.Max(n => n.X);
            _minY = _nodes.Min(n => n.Y); _maxY = _nodes.Max(n => n.Y);
            _qt = new QuadTree(_minX - 1, _minY - 1, _maxX - _minX + 2, _maxY - _minY + 2);
            for (int i = 0; i < _nodes.Count; i++) _qt.Insert(i, _nodes[i].X, _nodes[i].Y);
            _fit = true;
        }

        // Ada başına bir yerleşim: 0 radyal ağaç, 1 spiral (ayçiçeği), 2 eş merkezli halkalar, 3 ızgara.
        private void LayoutIslands(List<int> starts)
        {
            int n = _nodes.Count;
            var leaves = new int[n];
            for (int i = n - 1; i >= 0; i--)
                leaves[i] = _nodes[i].Kids.Count == 0 ? 1 : _nodes[i].Kids.Sum(k => leaves[k]);

            for (int s = 0; s < starts.Count; s++)
            {
                int a = starts[s], b = s + 1 < starts.Count ? starts[s + 1] : n;
                var isl = new Island { Name = _nodes[a].Name, Style = s % 4 };
                List<int> ids = Enumerable.Range(a, b - a).OrderBy(x => _nodes[x].Depth).ThenBy(x => x).ToList();
                int maxD = Math.Max(1, ids.Max(x => _nodes[x].Depth));
                switch (isl.Style)
                {
                    case 0:
                        Place(a, 0, 2 * Math.PI, leaves, Math.Max(150, leaves[a] * 30 / (2 * Math.PI) / maxD));
                        break;
                    case 1:
                        for (int k = 0; k < ids.Count; k++)
                        {
                            double ang = k * 2.399963, rad = 38 * Math.Sqrt(k);
                            _nodes[ids[k]].X = Math.Cos(ang) * rad; _nodes[ids[k]].Y = Math.Sin(ang) * rad;
                        }
                        break;
                    case 2:
                        for (int d = 0; d <= maxD; d++)
                        {
                            List<int> ring = ids.Where(x => _nodes[x].Depth == d).ToList();
                            double rad = d == 0 ? 0 : Math.Max(120 * d, ring.Count * 40 / (2 * Math.PI));
                            for (int k = 0; k < ring.Count; k++)
                            {
                                double ang = 2 * Math.PI * k / ring.Count;
                                _nodes[ring[k]].X = Math.Cos(ang) * rad; _nodes[ring[k]].Y = Math.Sin(ang) * rad;
                            }
                        }
                        break;
                    default:
                        int cols = (int)Math.Ceiling(Math.Sqrt(ids.Count * 1.3));
                        for (int k = 0; k < ids.Count; k++)
                        {
                            _nodes[ids[k]].X = (k % cols - (cols - 1) / 2.0) * 56;
                            _nodes[ids[k]].Y = (k / cols - (ids.Count / cols) / 2.0) * 56;
                        }
                        break;
                }
                foreach (int x in ids)
                {
                    isl.R = Math.Max(isl.R, Math.Sqrt(_nodes[x].X * _nodes[x].X + _nodes[x].Y * _nodes[x].Y));
                    _nodes[x].Island = s;
                }
                double sk = isl.R > 1100 ? 1100 / isl.R : (isl.R > 0 && isl.R < 300 ? Math.Min(3, 300 / isl.R) : 1);
                foreach (int x in ids) { _nodes[x].X *= sk; _nodes[x].Y *= sk; }
                isl.R = Math.Max(isl.R * sk, 260);
                _islands.Add(isl);
            }

            // adaları ızgara düzeninde yerleştir
            int cnt = _islands.Count, gridCols = (int)Math.Ceiling(Math.Sqrt(cnt));
            double gap = _islands.Max(x => x.R) * 2 + 300;
            for (int s = 0; s < cnt; s++)
            {
                Island isl = _islands[s];
                isl.CX = (s % gridCols - (gridCols - 1) / 2.0) * gap;
                isl.CY = (s / gridCols - ((cnt - 1) / gridCols) / 2.0) * gap;
                int a = starts[s], b = s + 1 < starts.Count ? starts[s + 1] : n;
                for (int x = a; x < b; x++) { _nodes[x].X += isl.CX; _nodes[x].Y += isl.CY; }
            }
            // spiral ve ızgara adalarında taban kenarlarını çizme (düzen bozulmasın); BFS/DFS çizgileri yine görünür
            _edges.RemoveAll(e => _islands[_nodes[e.a].Island].Style is 1 or 3);
        }

        private static int CountNodes(FolderNode n) => 1 + n.Children.Sum(c => CountNodes(c));

        private void FitAll()
        {
            double w = _maxX - _minX + 500, h = _maxY - _minY + 500;
            _z = Math.Clamp(Math.Min(ActualWidth / w, ActualHeight / h), 0.08, 3);
            _ox = -(_minX + _maxX) / 2 * _z; _oy = -(_minY + _maxY) / 2 * _z;
        }

        private void Add(FolderNode f, int parent, int depth)
        {
            if (_nodes.Count >= _cap) return;
            int id = _nodes.Count;
            _nodes.Add(new Node { Name = f.Name, Path = f.Path, Parent = parent, Depth = depth });
            _graph.AddNode(id.ToString());
            _avl.Insert(f.Name + "|" + id, new NodeItem { Id = id.ToString(), Title = f.Name, Path = f.Path });
            if (parent >= 0)
            {
                _graph.AddEdge(parent.ToString(), id.ToString());
                _edges.Add((parent, id));
                _nodes[parent].Kids.Add(id);
            }
            foreach (FolderNode c in f.Children.Take(40)) Add(c, id, depth + 1);
        }

        // Radyal ağaç yerleşimi: her alt ağaç, yaprak sayısı oranında bir açı dilimi alır.
        private void Layout()
        {
            int n = _nodes.Count, maxD = 1;
            var leaves = new int[n];
            for (int i = n - 1; i >= 0; i--)
            {
                Node nd = _nodes[i];
                leaves[i] = nd.Kids.Count == 0 ? 1 : nd.Kids.Sum(k => leaves[k]);
                maxD = Math.Max(maxD, nd.Depth);
            }
            Place(0, 0, 2 * Math.PI, leaves, Math.Max(230, leaves[0] * 36 / (2 * Math.PI) / maxD));
        }

        private void Place(int id, double a0, double a1, int[] leaves, double stepR)
        {
            Node nd = _nodes[id];
            double a = (a0 + a1) / 2, r = nd.Depth * stepR;
            nd.X = Math.Cos(a) * r;
            nd.Y = Math.Sin(a) * r;
            double s = a0;
            foreach (int k in nd.Kids)
            {
                double w = (a1 - a0) * leaves[k] / leaves[id];
                Place(k, s, s + w, leaves, stepR);
                s += w;
            }
        }

        // Her klasörün kendi dosyalarının boyutu + alt ağacın toplamı (ağaçtaki klasörler içinde).
        private void ComputeSizes()
        {
            foreach (Node nd in _nodes)
            {
                try { nd.Own = new DirectoryInfo(nd.Path).EnumerateFiles().Sum(f => f.Length); }
                catch { }
            }
            for (int i = _nodes.Count - 1; i >= 0; i--)
                _nodes[i].Total = _nodes[i].Own + _nodes[i].Kids.Sum(k => _nodes[k].Total);
            _maxTotal = Math.Max(1, _nodes.Where(n => n.Parent >= 0).Select(n => n.Total).DefaultIfEmpty(1).Max());
        }

        // Max-heap: en büyük 'count' klasörü çıkarır (heap sort mantığı). O(n log n).
        private static List<int> TopByKey(int count, long[] key)
        {
            var h = new List<int>();
            void Up(int i)
            {
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (key[h[p]] >= key[h[i]]) break;
                    (h[p], h[i]) = (h[i], h[p]); i = p;
                }
            }
            void Down(int i)
            {
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < h.Count && key[h[l]] > key[h[m]]) m = l;
                    if (r < h.Count && key[h[r]] > key[h[m]]) m = r;
                    if (m == i) break;
                    (h[m], h[i]) = (h[i], h[m]); i = m;
                }
            }
            for (int i = 1; i < key.Length; i++) { h.Add(i); Up(h.Count - 1); }   // 0 = kök, hariç
            var res = new List<int>();
            while (res.Count < count && h.Count > 0)
            {
                res.Add(h[0]);
                h[0] = h[h.Count - 1];
                h.RemoveAt(h.Count - 1);
                if (h.Count > 0) Down(0);
            }
            return res;
        }

        private static string Fmt(long b) =>
            b >= 1L << 30 ? (b / (double)(1L << 30)).ToString("F1") + " GB" :
            b >= 1L << 20 ? (b / (double)(1L << 20)).ToString("F1") + " MB" :
            (b / 1024.0).ToString("F0") + " KB";

        // .lnk kısayolları okunur; hedefi ağaçta bir klasörse iki klasör arasına çapraz kenar eklenir (ağaç -> gerçek graf).
        private void AddShortcutLinks()
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            dynamic sh = Activator.CreateInstance(t);
            var byPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _nodes.Count; i++) byPath[_nodes[i].Path.TrimEnd('\\')] = i;
            for (int i = 0; i < _nodes.Count; i++)
            {
                try
                {
                    foreach (string f in Directory.EnumerateFiles(_nodes[i].Path, "*.lnk"))
                    {
                        string target = (string)sh.CreateShortcut(f).TargetPath;
                        if (string.IsNullOrEmpty(target)) continue;
                        string dir = Directory.Exists(target) ? target : Path.GetDirectoryName(target);
                        if (dir != null && byPath.TryGetValue(dir.TrimEnd('\\'), out int j) && j != i
                            && _nodes[i].Parent != j && !_nodes[i].Kids.Contains(j) && !_nodes[i].Links.Contains(j))
                        {
                            _links.Add((i, j)); _nodes[i].Links.Add(j); _nodes[j].Links.Add(i);
                            _graph.AddEdge(i.ToString(), j.ToString());
                        }
                    }
                }
                catch { }
            }
        }

        // Hash tablosu: aynı ad + boyuttaki dosyaları gruplar, kopya içeren klasörleri işaretler.
        private void FindDuplicates()
        {
            var table = new Dictionary<string, List<int>>();
            for (int i = 0; i < _nodes.Count; i++)
                try
                {
                    foreach (FileInfo f in new DirectoryInfo(_nodes[i].Path).EnumerateFiles())
                    {
                        string key = f.Name.ToLowerInvariant() + "|" + f.Length;
                        if (!table.TryGetValue(key, out List<int> l)) table[key] = l = new List<int>();
                        l.Add(i);
                    }
                }
                catch { }
            int groups = 0;
            foreach (List<int> l in table.Values)
                if (l.Count > 1) { groups++; foreach (int i in l) _hl.Add(i); }
            _info = "Kopya (hash tablosu): " + groups + " grup, " + _hl.Count + " klasör işaretlendi, " + table.Count + " farklı dosya";
        }

        // Sıralama yarışı: aynı boyut dizisini üç algoritmayla sıralayıp süreyi (µs) ölçer.
        private static void Bubble(long[] a)
        {
            for (int i = 0; i < a.Length; i++)
                for (int j = 0; j + 1 < a.Length - i; j++)
                    if (a[j] > a[j + 1]) (a[j], a[j + 1]) = (a[j + 1], a[j]);
        }
        private static void Quick(long[] a, int lo, int hi)
        {
            if (lo >= hi) return;
            long p = a[(lo + hi) / 2]; int i = lo, j = hi;
            while (i <= j)
            {
                while (a[i] < p) i++;
                while (a[j] > p) j--;
                if (i <= j) { (a[i], a[j]) = (a[j], a[i]); i++; j--; }
            }
            Quick(a, lo, j); Quick(a, i, hi);
        }
        private static void Merge(long[] a, long[] t, int lo, int hi)
        {
            if (hi <= lo) return;
            int m = (lo + hi) / 2;
            Merge(a, t, lo, m); Merge(a, t, m + 1, hi);
            int i = lo, j = m + 1, k = lo;
            while (i <= m && j <= hi) t[k++] = a[i] <= a[j] ? a[i++] : a[j++];
            while (i <= m) t[k++] = a[i++];
            while (j <= hi) t[k++] = a[j++];
            Array.Copy(t, lo, a, lo, hi - lo + 1);
        }
        private static double TimeUs(Action<long[]> sort, long[] src)
        {
            var sw = Stopwatch.StartNew();
            for (int r = 0; r < 20; r++) sort((long[])src.Clone());
            return sw.Elapsed.TotalMilliseconds * 1000 / 20;
        }
        private string SortRace()
        {
            long[] sz = _nodes.Select(n => n.Total).ToArray();
            return "  |  sıralama (µs): kabarcık " + TimeUs(Bubble, sz).ToString("F0")
                 + " · birleştirme " + TimeUs(a => Merge(a, new long[a.Length], 0, a.Length - 1), sz).ToString("F0")
                 + " · hızlı " + TimeUs(a => Quick(a, 0, a.Length - 1), sz).ToString("F0");
        }

        private static double MeasureUs(Action a)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 2000; i++) a();
            return sw.Elapsed.TotalMilliseconds * 1000 / 2000;
        }

        // ---------- Modlar ----------
        private void SetMode(int m)
        {
            _mode = m; _pathA = -1; _query = ""; _info = "";
            ClearRun();
            if (m == 2) _info = "Başlangıç klasörüne tıkla, sonra hedefe tıkla.";
            if (m == 3) _info = "Klavyeden klasör adının baş harflerini yaz (Backspace siler).";
            if (m == 5) FindDuplicates();
            if (m == 4)
            {
                List<int> top = TopByKey(10, _nodes.Select(n => n.Total).ToArray());
                foreach (int i in top) _hl.Add(i);
                _info = "En büyük: " + string.Join(", ", top.Take(2).Select(i => _nodes[i].Name + " " + Fmt(_nodes[i].Total)))
                        + SortRace();
            }
        }

        private void ClearRun()
        {
            _ord = Array.Empty<int>(); _idx = Array.Empty<int>(); _from = Array.Empty<int>();
            _hl.Clear(); _hasRun = false;
        }

        private void SetRun(int[] ord, int color, bool isPath)
        {
            _hl.Clear();
            _ord = ord;
            _idx = Enumerable.Repeat(-1, _nodes.Count).ToArray();
            for (int i = 0; i < ord.Length; i++) _idx[ord[i]] = i;

            _from = new int[ord.Length];
            for (int i = 1; i < ord.Length; i++)
            {
                if (isPath) { _from[i] = ord[i - 1]; continue; }
                Node v = _nodes[ord[i]];
                int src = -1;
                foreach (int k in v.Kids.Concat(v.Links).Concat(new[] { v.Parent }))
                    if (k >= 0 && _idx[k] >= 0 && _idx[k] < i && (src < 0 || (color == 1 ? _idx[k] > _idx[src] : _idx[k] < _idx[src]))) src = k;
                _from[i] = src;
            }
            _runColor = color;
            _t0 = _clock.Elapsed.TotalMilliseconds;
            _step = Math.Clamp(6000.0 / Math.Max(1, ord.Length), 40, 300);
            _hasRun = true;
        }

        private void StartAlgo(int s)
        {
            List<string> order = _mode == 1 ? _graph.Dfs(s.ToString()) : _graph.Bfs(s.ToString());
            SetRun(order.Select(x => int.Parse(x)).ToArray(), _mode, false);
            _info = (_mode == 1 ? "DFS (yığın): " : "BFS (kuyruk): ") + _ord.Length + " düğüm, başlangıç: " + _nodes[s].Name;
        }

        private void Search()
        {
            _hl.Clear();
            if (_query.Length > 0)
                foreach (NodeItem it in _avl.StartsWith(_query)) _hl.Add(int.Parse(it.Id));
            double avl = MeasureUs(() => _avl.StartsWith(_query));
            double lin = MeasureUs(() => _nodes.Count(n => n.Name.StartsWith(_query, StringComparison.OrdinalIgnoreCase)));
            _info = "AVL: " + _hl.Count + " sonuç · AVL " + avl.ToString("F1") + " µs vs doğrusal " + lin.ToString("F1") + " µs  (yükseklik " + _avl.Height + ", n=" + _avl.Count + ")";
            if (_hl.Count > 0) { Node f = _nodes[_hl.Min()]; _ox = -f.X * _z; _oy = -f.Y * _z; }   // ilk sonuca git
        }

        private void OnNodeClick(int h)
        {
            if (_mode <= 1) { StartAlgo(h); return; }
            if (_mode == 2)
            {
                if (_pathA < 0)
                {
                    _pathA = h; ClearRun(); _hl.Add(h);
                    _info = "Başlangıç: " + _nodes[h].Name + ". Şimdi hedefe tıkla.";
                }
                else
                {
                    List<string> path = _graph.ShortestPath(_pathA.ToString(), h.ToString());
                    SetRun(path.Select(x => int.Parse(x)).ToArray(), 2, true);
                    _info = "Yol: " + _nodes[_pathA].Name + " → " + _nodes[h].Name + "  (" + (path.Count - 1) + " kenar)";
                    _pathA = -1;
                }
            }
            else if (_mode == 4)
                _info = _nodes[h].Name + ": toplam " + Fmt(_nodes[h].Total) + ", alt klasör: " + _nodes[h].Kids.Count;
        }

        private int MaskAt(int x, int y) =>
            x < 0 || y < 0 || x >= ShadowCamera.W || y >= ShadowCamera.H ? 0 : _maskBuf[y * ShadowCamera.W + x];

        // Gölge maskesi klasörlerle etkileşir: gölgenin içindeki klasör, maskenin eğimiyle dışarı itilir, sonra yay gibi geri döner.
        private void Interact()
        {
            double W = ActualWidth, H = ActualHeight, cx = W / 2 + _ox, cy = H / 2 + _oy;
            foreach (int i in _seen)
            {
                Node n = _nodes[i];
                int mx = (int)(((n.X + n.Dx) * _z + cx) / W * ShadowCamera.W);
                int my = (int)(((n.Y + n.Dy) * _z + cy) / H * ShadowCamera.H);
                n.InShadow = MaskAt(mx, my) > 110;
                if (n.InShadow)
                {
                    double gx = MaskAt(mx - 6, my) - MaskAt(mx + 6, my);
                    double gy = MaskAt(mx, my - 6) - MaskAt(mx, my + 6);
                    double len = Math.Sqrt(gx * gx + gy * gy);
                    if (len > 1) { n.Dx += gx / len * 6 / _z; n.Dy += gy / len * 6 / _z; }
                }
                n.Dx *= 0.93; n.Dy *= 0.93;
            }
        }

        public void ToggleCamera()
        {
            if (_cam != null)
            {
                _cam.Dispose(); _cam = null; _shadow = null; _info = "Kamera kapatıldı.";
                foreach (Node nd in _nodes) { nd.Dx = nd.Dy = 0; nd.InShadow = false; }
                return;
            }
            var cam = new ShadowCamera();
            if (!cam.Start()) { cam.Dispose(); _info = "Kamera açılamadı (Windows gizlilik ayarlarında kamera iznini kontrol et)."; return; }
            _cam = cam;
            _shadow = new WriteableBitmap(ShadowCamera.W, ShadowCamera.H, 96, 96, PixelFormats.Bgra32, null);
            _info = "Kamera açık: ilk 1-2 saniye kameranın önünden çekil (kalibrasyon).";
        }

        private void Tick()
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            if (_mode <= 1 && _nodes.Count > 0 && (!_hasRun || now > _t0 + (_ord.Length + 9) * _step))
                StartAlgo(_rng.Next(_nodes.Count));
            if (!_drag) { _ox += _vx; _oy += _vy; _vx *= 0.94; _vy *= 0.94; }
            if (now - _lastSave > 2000) { _lastSave = now; SaveCam(); }
            if (_cam != null && _cam.TryGet(_maskBuf))
            {
                for (int i = 0; i < _maskBuf.Length; i++)      // maske -> yarı saydam koyu gölge
                {
                    int o = i * 4;
                    _pix[o] = 6; _pix[o + 1] = 8; _pix[o + 2] = 16; _pix[o + 3] = (byte)(_maskBuf[i] * 0.75);
                }
                _shadow.WritePixels(new Int32Rect(0, 0, ShadowCamera.W, ShadowCamera.H), _pix, ShadowCamera.W * 4, 0);
            }
            if (_cam != null && _seen != null) Interact();
            InvalidateVisual();
        }

        // ---------- Çizim ----------
        protected override void OnRender(DrawingContext dc)
        {
            double W = ActualWidth, H = ActualHeight;
            if (W <= 0 || H <= 0 || _nodes.Count == 0) return;

            double now = _clock.Elapsed.TotalMilliseconds;
            if (_fit) { _fit = false; FitAll(); }
            double cx = W / 2 + _ox, cy = H / 2 + _oy;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            Point S(Node n) => new Point((n.X + n.Dx) * _z + cx, (n.Y + n.Dy) * _z + cy);
            bool Vis(Point p) => p.X > -40 && p.X < W + 40 && p.Y > -40 && p.Y < H + 40;

            dc.DrawRectangle(Bg, null, new Rect(0, 0, W, H));
            if (_shadow != null)
            {
                dc.DrawRectangle(Floor, null, new Rect(0, 0, W, H));
                dc.DrawImage(_shadow, new Rect(0, 0, W, H));
            }

            double g = 80 * _z;
            if (_shadow == null) for (double x = cx % g; x < W; x += g) dc.DrawLine(GridPen, new Point(x, 0), new Point(x, H));
            if (_shadow == null) for (double y = cy % g; y < H; y += g) dc.DrawLine(GridPen, new Point(0, y), new Point(W, y));

            var geo = new StreamGeometry();
            using (StreamGeometryContext gc = geo.Open())
            {
                foreach (var (a, b) in _edges)
                {
                    Point pa = S(_nodes[a]), pb = S(_nodes[b]);
                    if (!Vis(pa) && !Vis(pb)) continue;
                    gc.BeginFigure(pa, false, false);
                    gc.LineTo(pb, true, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, EdgePen, geo);
            foreach (var (la, lb) in _links)
            {
                Point pa = S(_nodes[la]), pb = S(_nodes[lb]);
                if (Vis(pa) || Vis(pb)) dc.DrawLine(LinkPen, pa, pb);
            }

            double k = Math.Floor((now - _t0) / _step);
            Color col = Cols[_runColor];
            for (int i = 1; i <= Math.Min(k, _ord.Length - 1); i++)
            {
                if (_from[i] < 0) continue;
                double age = now - (_t0 + i * _step);
                Point pa = S(_nodes[_from[i]]), pb = S(_nodes[_ord[i]]);
                if (!Vis(pa) && !Vis(pb)) continue;
                double t = Math.Min(1, age / _step), alpha = Math.Max(0.3, 1 - age / 3000);
                var pe = new Point(pa.X + (pb.X - pa.X) * t, pa.Y + (pb.Y - pa.Y) * t);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), col.R, col.G, col.B)), 2.5), pa, pe);
            }

            _hover = -1; _hoverFile = null;
            double hd = 18 * 18;
            var seen = new List<int>();
            _qt.Query((-40 - cx) / _z, (-40 - cy) / _z, (W + 40 - cx) / _z, (H + 40 - cy) / _z, seen);
            _drawn = seen.Count;
            _seen = seen;
            foreach (int i in seen)
            {
                Node n = _nodes[i];
                Point p = S(n);
                if (!Vis(p)) continue;

                double d = (p.X - _mouse.X) * (p.X - _mouse.X) + (p.Y - _mouse.Y) * (p.Y - _mouse.Y);
                if (d < hd) { hd = d; _hover = i; }

                int j = i < _idx.Length ? _idx[i] : -1;
                bool lit = j >= 0 && j <= k, hl = _hl.Contains(i) || n.InShadow;
                double sc = _mode == 4 && i > 0 ? 0.8 + Math.Sqrt(Math.Min(1.0, (double)n.Total / _maxTotal)) : 1;
                sc *= Math.Clamp(_z * 1.6, 0.35, 1.0);
                if (lit) sc *= 1 + 0.35 * Math.Max(0, 1 - (now - (_t0 + j * _step)) / 800);
                double fw = 26 * sc, fh = 20 * sc, r = fh / 2;
                if (lit)
                {
                    var glow = new RadialGradientBrush(Color.FromArgb(150, col.R, col.G, col.B), Color.FromArgb(0, col.R, col.G, col.B));
                    dc.DrawEllipse(glow, null, p, fw, fw);
                }
                if (hl) dc.DrawRoundedRectangle(null, PinkPen, new Rect(p.X - fw / 2 - 5, p.Y - fh / 2 - 5, fw + 10, fh + 10), 5, 5);
                if (_z > 1.1)
                {
                    n.Files ??= ListFiles(n.Path);
                    for (int fi = 0; fi < n.Files.Length; fi++)
                    {
                        Point q = FilePoint(p, fi);
                        if (!Vis(q)) continue;
                        dc.DrawLine(FileLinkPen, p, q);
                        dc.DrawRoundedRectangle(FileBrushes[ExtIdx(Path.GetExtension(n.Files[fi]).ToLowerInvariant())], null, new Rect(q.X - 4, q.Y - 5, 8, 10), 1.5, 1.5);
                        if ((q.X - _mouse.X) * (q.X - _mouse.X) + (q.Y - _mouse.Y) * (q.Y - _mouse.Y) < 49) _hoverFile = n.Files[fi];
                    }
                }
                DrawFolder(dc, p, fw, fh, lit ? col : (Color?)null, i == _hover);

                if (_z > 0.7 || i == _hover || hl)
                {
                    n.Label ??= Text(n.Name.Length > 18 ? n.Name.Substring(0, 17) + "…" : n.Name, 12, LabelBrush, dpi);
                    dc.DrawText(n.Label, new Point(p.X - n.Label.Width / 2, p.Y + r + 3));
                }
            }

            // adaların çerçevesi ve başlığı
            foreach (Island isl in _islands)
            {
                var ic = new Point(isl.CX * _z + cx, isl.CY * _z + cy);
                double ir = (isl.R + 70) * _z;
                if (ic.X + ir < 0 || ic.X - ir > W || ic.Y + ir < 0 || ic.Y - ir > H) continue;
                dc.DrawEllipse(null, IslandPen, ic, ir, ir);
                isl.Label ??= Text(isl.Name + "  ·  " + StyleNames[isl.Style], 24, LabelBrush, dpi);
                dc.DrawText(isl.Label, new Point(ic.X - isl.Label.Width / 2, ic.Y - ir - 34));
            }

            // mini harita: tüm düğümler + görünen bölge
            double mw = 170, mh = 110, mx0 = W - mw - 12, my0 = H - mh - 12;
            double ms = Math.Min(mw / Math.Max(1, _maxX - _minX), mh / Math.Max(1, _maxY - _minY)) * 0.9;
            Point M(double x, double y) => new Point(mx0 + mw / 2 + (x - (_minX + _maxX) / 2) * ms, my0 + mh / 2 + (y - (_minY + _maxY) / 2) * ms);
            dc.DrawRoundedRectangle(MapBg, MapPen, new Rect(mx0, my0, mw, mh), 6, 6);
            dc.PushClip(new RectangleGeometry(new Rect(mx0, my0, mw, mh)));
            foreach (Node mn in _nodes) dc.DrawEllipse(NodeBrush, null, M(mn.X, mn.Y), 1.5, 1.5);
            dc.DrawRectangle(null, MapViewPen, new Rect(M(-cx / _z, -cy / _z), M((W - cx) / _z, (H - cy) / _z)));
            dc.Pop();

            // üst bilgi: modlar, sonuç satırı, üzerine gelinen klasörün yolu
            string modes = string.Join("  ·  ", ModeNames.Select((m, i) => i == _mode ? "[F" + (i + 1) + " " + m + "]" : "F" + (i + 1) + " " + m));
            if (_mode == 3) modes += "   ara: " + _query + "|";
            FormattedText ht = Text(modes + "   ·   Home: tümünü göster   Esc: temizle   ·   F7 kamera" + (_cam != null ? " (açık, F8 kalibre)" : "") + "   ·   quadtree: " + _drawn + "/" + _nodes.Count, 14, LabelBrush, dpi);
            dc.DrawText(ht, new Point((W - ht.Width) / 2, 10));
            if (_info.Length > 0)
            {
                FormattedText it = Text(_info, 14, new SolidColorBrush(Color.FromArgb(220, col.R, col.G, col.B)), dpi);
                dc.DrawText(it, new Point((W - it.Width) / 2, 34));
            }
            string hp = _hoverFile ?? (_hover >= 0 ? _nodes[_hover].Path : null);
            if (hp != null)
            {
                FormattedText pt = Text(hp, 13, PathBrush, dpi);
                dc.DrawText(pt, new Point((W - pt.Width) / 2, 58));
            }
        }

        // Windows 11 tarzı klasör: arka gövde + sekme + açık renkli ön yüz
        private static void DrawFolder(DrawingContext dc, Point p, double w, double h, Color? tint, bool hover)
        {
            double x = p.X - w / 2, y = p.Y - h / 2;
            Brush back = FolderBack, front = FolderFront;
            if (tint is Color t)
            {
                back = new SolidColorBrush(Mix(t, Colors.Black, 0.35));
                front = new LinearGradientBrush(Mix(t, Colors.White, 0.35), t, 90);
            }
            dc.DrawRoundedRectangle(back, null, new Rect(x, y, w * 0.42, h * 0.45), 3, 3);          // sekme
            dc.DrawRoundedRectangle(back, null, new Rect(x, y + h * 0.12, w, h * 0.88), 3, 3);      // arka gövde
            dc.DrawRoundedRectangle(front, null, new Rect(x, y + h * 0.30, w, h * 0.70), 3, 3);     // ön yüz
            if (hover) dc.DrawRoundedRectangle(null, HoverPen, new Rect(x - 2, y - 2, w + 4, h + 4), 4, 4);
        }

        private static FormattedText Text(string s, double size, Brush b, double dpi) =>
            new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, b, dpi);

        // ---------- Klavye ----------
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key >= Key.F1 && e.Key <= Key.F6) SetMode(e.Key - Key.F1);
            else if (e.Key == Key.Back && _mode == 3 && _query.Length > 0) { _query = _query.Substring(0, _query.Length - 1); Search(); }
            else if (e.Key == Key.F7) ToggleCamera();
            else if (e.Key == Key.F8) _cam?.Recalibrate();
            else if (e.Key == Key.Escape) SetMode(_mode);
            else if (e.Key == Key.Home) FitAll();
            base.OnKeyDown(e);
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            if (_mode == 3 && e.Text.Length > 0 && !char.IsControl(e.Text[0])) { _query += e.Text; Search(); }
            base.OnTextInput(e);
        }

        // ---------- Fare: sürükle = kaydır, tekerlek = yakınlaştır, tık = moda göre işlem, çift tık = klasörü aç ----------
        private int HitNode(Point p)
        {
            double cx = ActualWidth / 2 + _ox, cy = ActualHeight / 2 + _oy, best = 22 * 22;
            int b = -1;
            var near = new List<int>();
            _qt.Query((p.X - 22 - cx) / _z, (p.Y - 22 - cy) / _z, (p.X + 22 - cx) / _z, (p.Y + 22 - cy) / _z, near);
            foreach (int i in near)
            {
                double dx = _nodes[i].X * _z + cx - p.X, dy = _nodes[i].Y * _z + cy - p.Y, d = dx * dx + dy * dy;
                if (d < best) { best = d; b = i; }
            }
            return b;
        }

        private static Point FilePoint(Point p, int f, double z)
        {
            double ang = f * 2.399963, rad = (24 + 9 * Math.Sqrt(f)) * Math.Min(z, 2);   // ayçiçeği (altın açı) dizilimi
            return new Point(p.X + Math.Cos(ang) * rad, p.Y + Math.Sin(ang) * rad);
        }
        private Point FilePoint(Point p, int f) => FilePoint(p, f, _z);

        private string HitFile(Point m)
        {
            if (_z <= 1.1) return null;
            double cx = ActualWidth / 2 + _ox, cy = ActualHeight / 2 + _oy;
            foreach (Node n in _nodes)
            {
                if (n.Files == null) continue;
                var p = new Point(n.X * _z + cx, n.Y * _z + cy);
                if (Math.Abs(p.X - m.X) > 230 || Math.Abs(p.Y - m.Y) > 230) continue;
                for (int f = 0; f < n.Files.Length; f++)
                {
                    Point q = FilePoint(p, f);
                    if ((q.X - m.X) * (q.X - m.X) + (q.Y - m.Y) * (q.Y - m.Y) < 49) return n.Files[f];
                }
            }
            return null;
        }

        private static void OpenFile(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private static void OpenFolder(string path)
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            _drag = true; _moved = 0; _vx = _vy = 0;
            _last = e.GetPosition(this);
            CaptureMouse();
            if (e.ClickCount == 2)
            {
                string file = HitFile(_last);
                if (file != null) OpenFile(file);
                else { int h = HitNode(_last); if (h >= 0) OpenFolder(_nodes[h].Path); }
            }
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point p = e.GetPosition(this);
            _mouse = p;
            if (_drag)
            {
                _vx = p.X - _last.X; _vy = p.Y - _last.Y;
                _ox += _vx; _oy += _vy;
                _moved += Math.Abs(_vx) + Math.Abs(_vy);
                _last = p;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _drag = false;
            ReleaseMouseCapture();
            if (_moved < 6) { int h = HitNode(e.GetPosition(this)); if (h >= 0) OnNodeClick(h); }
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            double nz = Math.Clamp(_z * (e.Delta > 0 ? 1.12 : 0.89), 0.08, 3);
            Point p = e.GetPosition(this);
            double cx = p.X - ActualWidth / 2, cy = p.Y - ActualHeight / 2;
            _ox = cx - (cx - _ox) * nz / _z;
            _oy = cy - (cy - _oy) * nz / _z;
            _z = nz;
            base.OnMouseWheel(e);
        }
    }
}