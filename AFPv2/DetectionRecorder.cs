// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;

namespace MeteorDetection
{
    /// <summary>
    /// 検知画像（トラックごとのクロップ画像）と検知情報（CSV）をディスクに保存する。
    /// フル解像度フレームをそのまま毎フレーム保存すると容量・I/O負荷が大きいため、
    /// 既定ではトラック周辺をクロップして保存する（SaveFrameのcropRect指定）。
    /// </summary>
    public sealed class DetectionRecorder : IDisposable
    {
        private readonly string _imageDir;
        private readonly string _dataDir;
        private readonly Dictionary<int, StreamWriter> _openLogs = new Dictionary<int, StreamWriter>();

        public DetectionRecorder(string baseDir)
        {
            _imageDir = Path.Combine(baseDir, "images");
            _dataDir = Path.Combine(baseDir, "data");
            Directory.CreateDirectory(_imageDir);
            Directory.CreateDirectory(_dataDir);
        }

        /// <summary>検知画像を保存する。cropRectを指定すればその範囲のみ切り出して保存する。</summary>
        public void SaveFrame(int trackId, DateTime t, Mat frame, Rect? cropRect = null)
        {
            string fn = $"track{trackId:D4}_{t:yyyyMMdd_HHmmss_fff}.png";
            string path = Path.Combine(_imageDir, fn);

            if (cropRect.HasValue)
            {
                var r = ClampRect(cropRect.Value, frame.Width, frame.Height);
                using (var roi = new Mat(frame, r))
                    Cv2.ImWrite(path, roi);
            }
            else
            {
                Cv2.ImWrite(path, frame);
            }
        }

        /// <summary>検知情報（時刻・座標・方位高度・輝度）をトラックごとのCSVに追記する。</summary>
        public void LogDetection(MeteorTrack track, DateTime t, Point2f pixelPos, double azDeg, double altDeg)
        {
            if (!_openLogs.TryGetValue(track.Id, out StreamWriter w))
            {
                string path = Path.Combine(_dataDir, $"track{track.Id:D4}.csv");
                w = new StreamWriter(path, false);
                w.WriteLine("timestamp_utc,pixel_x,pixel_y,az_deg,alt_deg,peak_value");
                _openLogs[track.Id] = w;
            }
            w.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:O},{1:F2},{2:F2},{3:F3},{4:F3},{5:F1}",
                t, pixelPos.X, pixelPos.Y, azDeg, altDeg, track.PeakValue));
            w.Flush();
        }

        /// <summary>トラック終了時にログファイルを閉じる。</summary>
        public void CloseTrack(int trackId)
        {
            if (_openLogs.TryGetValue(trackId, out StreamWriter w))
            {
                w.Dispose();
                _openLogs.Remove(trackId);
            }
        }

        private static Rect ClampRect(Rect r, int w, int h)
        {
            int x0 = Math.Max(0, r.X), y0 = Math.Max(0, r.Y);
            int x1 = Math.Min(w, r.X + r.Width), y1 = Math.Min(h, r.Y + r.Height);
            return new Rect(x0, y0, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0));
        }

        public void Dispose()
        {
            foreach (var w in _openLogs.Values) w.Dispose();
            _openLogs.Clear();
        }
    }
}
