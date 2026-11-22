namespace InfiniteGrid
{
    public class Graph
    {
        private readonly Dictionary<string, List<string>> _adj = new();

        public void AddNode(string id)
        {
            if (!_adj.ContainsKey(id)) _adj[id] = new List<string>();
        }

        public void AddEdge(string a, string b)
        {
            AddNode(a);
            AddNode(b);
            _adj[a].Add(b);
            _adj[b].Add(a);
        }

        public List<string> Bfs(string start)
        {
            var order = new List<string>();
            var visited = new HashSet<string> { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                order.Add(cur);
                foreach (string next in _adj[cur])
                    if (visited.Add(next))
                        queue.Enqueue(next);
            }
            return order;
        }

        public List<string> Dfs(string start)
        {
            var order = new List<string>();
            var visited = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                string cur = stack.Pop();
                if (!visited.Add(cur)) continue;
                order.Add(cur);

                for (int i = _adj[cur].Count - 1; i >= 0; i--)
                    if (!visited.Contains(_adj[cur][i]))
                        stack.Push(_adj[cur][i]);
            }
            return order;
        }

        public List<string> ShortestPath(string from, string to)
        {
            var parent = new Dictionary<string, string> { [from] = null };
            var queue = new Queue<string>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                if (cur == to) break;
                foreach (string next in _adj[cur])
                {
                    if (parent.ContainsKey(next)) continue;
                    parent[next] = cur;
                    queue.Enqueue(next);
                }
            }

            var path = new List<string>();
            if (!parent.ContainsKey(to)) return path;

            for (string c = to; c != null; c = parent[c]) path.Add(c);
            path.Reverse();
            return path;
        }
    }
}
