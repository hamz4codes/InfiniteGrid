namespace InfiniteGrid
{
    // Düzlemi özyinelemeli olarak dörde bölen uzamsal indeks.
    // Bir dikdörtgendeki noktaları bulmak: tüm listeyi taramak O(n) yerine ortalama O(log n + k).
    public class QuadTree
    {
        private const int Capacity = 8, MaxDepth = 12;
        private readonly double _x, _y, _w, _h;
        private readonly int _depth;
        private List<(int id, double x, double y)> _items = new();
        private QuadTree[] _kids;

        public QuadTree(double x, double y, double w, double h, int depth = 0)
        {
            _x = x; _y = y; _w = w; _h = h; _depth = depth;
        }

        public void Insert(int id, double px, double py)
        {
            if (_kids != null) { Child(px, py).Insert(id, px, py); return; }
            _items.Add((id, px, py));
            if (_items.Count > Capacity && _depth < MaxDepth) Split();
        }

        private QuadTree Child(double px, double py) =>
            _kids[(px >= _x + _w / 2 ? 1 : 0) + (py >= _y + _h / 2 ? 2 : 0)];

        private void Split()
        {
            double hw = _w / 2, hh = _h / 2;
            _kids = new[]
            {
                new QuadTree(_x, _y, hw, hh, _depth + 1),       new QuadTree(_x + hw, _y, hw, hh, _depth + 1),
                new QuadTree(_x, _y + hh, hw, hh, _depth + 1),  new QuadTree(_x + hw, _y + hh, hw, hh, _depth + 1)
            };
            foreach (var it in _items) Child(it.x, it.y).Insert(it.id, it.x, it.y);
            _items = null;
        }

        // [x0,x1] x [y0,y1] dikdörtgenindeki noktaların kimliklerini result listesine ekler.
        public void Query(double x0, double y0, double x1, double y1, List<int> result)
        {
            if (x1 < _x || x0 > _x + _w || y1 < _y || y0 > _y + _h) return;   // bu bölge hiç kesişmiyor: dalı atla
            if (_kids != null)
            {
                foreach (QuadTree k in _kids) k.Query(x0, y0, x1, y1, result);
                return;
            }
            foreach (var it in _items)
                if (it.x >= x0 && it.x <= x1 && it.y >= y0 && it.y <= y1) result.Add(it.id);
        }
    }
}