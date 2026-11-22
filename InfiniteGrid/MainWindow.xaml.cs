using System.IO;
using System.Windows;

namespace InfiniteGrid
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Klasörler komut satırından (Lively "Launch options") verilebilir; yoksa dört ana klasör taranır.
            string[] args = Environment.GetCommandLineArgs().Skip(1).Where(Directory.Exists).ToArray();
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] folders = args.Length > 0 ? args : new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Path.Combine(profile, "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            };

            var view = new GraphView();
            view.LoadHome(folders);
            if (Environment.GetCommandLineArgs().Contains("--camera")) view.ToggleCamera();   // Lively'de klavye yok: kamerayı başlangıçta aç
            Content = view;
            Loaded += (s, e) => view.Focus();
        }
    }
}