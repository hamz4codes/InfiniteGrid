using System.IO;

namespace InfiniteGrid
{
    public class FolderNode
    {
        public string Name;
        public string Path;
        public List<FolderNode> Children = new();
    }

    public static class FolderTree
    {
        public static FolderNode Build(string path, int maxDepth)
        {
            string name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path;

            var node = new FolderNode { Name = name, Path = path };
            if (maxDepth <= 0) return node;

            try
            {
                foreach (string dir in Directory.GetDirectories(path))
                    node.Children.Add(Build(dir, maxDepth - 1));
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            return node;
        }

        private static bool Match(FolderNode n, string name) =>
            n.Name.Equals(name, StringComparison.OrdinalIgnoreCase);

        public static FolderNode FindDfs(FolderNode root, string name, out int visited)
        {
            visited = 0;
            var stack = new Stack<FolderNode>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                FolderNode n = stack.Pop();
                visited++;
                if (Match(n, name)) return n;
                for (int i = n.Children.Count - 1; i >= 0; i--)
                    stack.Push(n.Children[i]);
            }
            return null;
        }

        public static FolderNode FindBfs(FolderNode root, string name, out int visited)
        {
            visited = 0;
            var queue = new Queue<FolderNode>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                FolderNode n = queue.Dequeue();
                visited++;
                if (Match(n, name)) return n;
                foreach (FolderNode child in n.Children)
                    queue.Enqueue(child);
            }
            return null;
        }
    }
}
