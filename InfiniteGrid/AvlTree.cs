using System.Globalization;

namespace InfiniteGrid
{
    public class AvlTree
    {
        private class AvlNode
        {
            public string Key;
            public NodeItem Value;
            public AvlNode Left, Right;
            public int Height = 1;
        }

        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
        private AvlNode _root;

        public int Count { get; private set; }
        public int Height => H(_root);

        private static string Norm(string s) => s.ToLower(Tr);
        private static int H(AvlNode n) => n == null ? 0 : n.Height;
        private static void Update(AvlNode n) => n.Height = 1 + Math.Max(H(n.Left), H(n.Right));
        private static int Balance(AvlNode n) => n == null ? 0 : H(n.Left) - H(n.Right);

        private static AvlNode RotateRight(AvlNode y)
        {
            AvlNode x = y.Left;
            y.Left = x.Right;
            x.Right = y;
            Update(y);
            Update(x);
            return x;
        }

        private static AvlNode RotateLeft(AvlNode x)
        {
            AvlNode y = x.Right;
            x.Right = y.Left;
            y.Left = x;
            Update(x);
            Update(y);
            return y;
        }

        public void Insert(string key, NodeItem value) => _root = Insert(_root, Norm(key), value);

        private AvlNode Insert(AvlNode n, string key, NodeItem value)
        {
            if (n == null)
            {
                Count++;
                return new AvlNode { Key = key, Value = value };
            }

            int c = string.CompareOrdinal(key, n.Key);
            if (c < 0) n.Left = Insert(n.Left, key, value);
            else if (c > 0) n.Right = Insert(n.Right, key, value);
            else { n.Value = value; return n; }

            Update(n);
            int b = Balance(n);

            if (b > 1 && string.CompareOrdinal(key, n.Left.Key) < 0)
                return RotateRight(n);
            if (b < -1 && string.CompareOrdinal(key, n.Right.Key) > 0)
                return RotateLeft(n);
            if (b > 1 && string.CompareOrdinal(key, n.Left.Key) > 0)
            {
                n.Left = RotateLeft(n.Left);
                return RotateRight(n);
            }
            if (b < -1 && string.CompareOrdinal(key, n.Right.Key) < 0)
            {
                n.Right = RotateRight(n.Right);
                return RotateLeft(n);
            }
            return n;
        }

        public NodeItem Search(string key)
        {
            key = Norm(key);
            AvlNode cur = _root;
            while (cur != null)
            {
                int c = string.CompareOrdinal(key, cur.Key);
                if (c == 0) return cur.Value;
                cur = c < 0 ? cur.Left : cur.Right;
            }
            return null;
        }

        public List<NodeItem> StartsWith(string prefix)
        {
            var result = new List<NodeItem>();
            Collect(_root, Norm(prefix), result);
            return result;
        }

        private void Collect(AvlNode n, string prefix, List<NodeItem> result)
        {
            if (n == null) return;
            int c = string.CompareOrdinal(n.Key, prefix);
            bool starts = n.Key.StartsWith(prefix, StringComparison.Ordinal);

            if (c >= 0) Collect(n.Left, prefix, result);
            if (starts) result.Add(n.Value);
            if (c < 0 || starts) Collect(n.Right, prefix, result);
        }

        public List<NodeItem> InOrder()
        {
            var list = new List<NodeItem>();
            InOrder(_root, list);
            return list;
        }

        private void InOrder(AvlNode n, List<NodeItem> list)
        {
            if (n == null) return;
            InOrder(n.Left, list);
            list.Add(n.Value);
            InOrder(n.Right, list);
        }
    }
}