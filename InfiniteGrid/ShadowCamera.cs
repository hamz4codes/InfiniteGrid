using OpenCvSharp;
using System.Runtime.InteropServices;

namespace InfiniteGrid
{
    // Kameradan kare okur, boş odanın referans karesiyle farkını alıp vücut maskesi üretir (arka plan çıkarma).
    // Ayrı bir iş parçacığında çalışır; arayüz TryGet ile en son maskeyi alır.
    public class ShadowCamera : IDisposable
    {
        public const int W = 320, H = 240;
        private VideoCapture _cap;
        private Thread _thread;
        private volatile bool _run, _recal = true;
        private readonly object _lock = new();
        private readonly byte[] _mask = new byte[W * H];
        private bool _fresh;

        public bool Start()
        {
            try
            {
                _cap = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
                if (!_cap.IsOpened()) return false;
            }
            catch { return false; }
            _run = true;
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
            return true;
        }

        // Yeni referans kare al (kameranın önü boşken çağır).
        public void Recalibrate() => _recal = true;

        public bool TryGet(byte[] dst)
        {
            lock (_lock)
            {
                if (!_fresh) return false;
                Buffer.BlockCopy(_mask, 0, dst, 0, _mask.Length);
                _fresh = false;
                return true;
            }
        }

        private void Loop()
        {
            using var frame = new Mat();
            using var small = new Mat();
            using var gray = new Mat();
            using var bg = new Mat();
            using var diff = new Mat();
            int warm = 0;
            while (_run)
            {
                if (!_cap.Read(frame) || frame.Empty()) { Thread.Sleep(10); continue; }
                Cv2.Resize(frame, small, new Size(W, H));
                Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.GaussianBlur(gray, gray, new Size(7, 7), 0);

                if (_recal)                       // pozlama otursun diye 20 kare bekle, sonra referansı al
                {
                    if (++warm >= 20) { gray.CopyTo(bg); warm = 0; _recal = false; }
                    continue;
                }

                Cv2.Absdiff(gray, bg, diff);                          // referanstan farklı yerler
                Cv2.Threshold(diff, diff, 28, 255, ThresholdTypes.Binary);
                Cv2.Dilate(diff, diff, null, iterations: 2);          // delikleri kapat
                Cv2.GaussianBlur(diff, diff, new Size(15, 15), 0);    // yumuşak gölge kenarı
                Cv2.Flip(diff, diff, FlipMode.Y);                     // ayna görüntüsü

                lock (_lock) { Marshal.Copy(diff.Data, _mask, 0, _mask.Length); _fresh = true; }
            }
        }

        public void Dispose()
        {
            _run = false;
            _thread?.Join(500);
            _cap?.Dispose();
        }
    }
}