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
        private StreamWriter _trackEventLog;

        public DetectionRecorder(string baseDir)
        {
            _imageDir = Path.Combine(baseDir, "images");
            _dataDir = Path.Combine(baseDir, "data");
            Directory.CreateDirectory(_imageDir);
            Directory.CreateDirectory(_dataDir);
        }

        /// <summary>動画1本分の処理ログを開始する。</summary>
        public void StartSession(string videoPath)
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            _trackEventLog = new StreamWriter(Path.Combine(_dataDir, "track_events_" + stamp + ".csv"), false);
            _trackEventLog.WriteLine("event,timestamp_utc,track_id,confirmed,hits,misses,first_seen_utc,last_seen_utc,pos_x,pos_y,vel_x_px_s,vel_y_px_s,peak_value,history_count,video_path");
            _trackEventLog.WriteLine("# video=" + Escape(videoPath));
        }

        /// <summary>トラックのライフサイクルを記録する。CONFIRMED時は履歴全体も別ログへ詳細出力する。</summary>
        public void LogTrackEvent(string eventName, MeteorTrack track, DateTime timestampUtc)
        {
            if (_trackEventLog == null || track == null) return;
            var p = track.Position;
            var v = track.VelocityPxPerSec;
            _trackEventLog.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1:O},{2},{3},{4},{5},{6:O},{7:O},{8:F3},{9:F3},{10:F3},{11:F3},{12:F3},{13}",
                eventName, timestampUtc, track.Id, track.Confirmed, track.Hits, track.Misses,
                track.FirstSeen, track.LastSeen, p.X, p.Y, v.X, v.Y, track.PeakValue, track.History.Count));
            _trackEventLog.Flush();

            if (eventName == "CONFIRMED")
            {
                string path = Path.Combine(_dataDir, string.Format("track{0:D4}_confirmed_detail.csv", track.Id));
                using (var w = new StreamWriter(path, false))
                {
                    w.WriteLine("track_id,history_index,timestamp_utc,pos_x,pos_y");
                    for (int i = 0; i < track.History.Count; i++)
                    {
                        var h = track.History[i];
                        w.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "{0},{1},{2:O},{3:F3},{4:F3}", track.Id, i, h.T, h.Pos.X, h.Pos.Y));
                    }
                }
            }
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
        public void LogDetection(MeteorTrack track, int frameIndex, DateTime t, Point2f pixelPos, double azDeg, double altDeg)
        {
            if (!_openLogs.TryGetValue(track.Id, out StreamWriter w))
            {
                string path = Path.Combine(_dataDir, $"track{track.Id:D4}.csv");
                w = new StreamWriter(path, false);
                w.WriteLine("frame_index,timestamp_utc,pixel_x,pixel_y,az_deg,alt_deg,peak_value");
                _openLogs[track.Id] = w;
            }
            w.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1:O},{2:F2},{3:F2},{4:F3},{5:F3},{6:F1}",
                frameIndex, t, pixelPos.X, pixelPos.Y, azDeg, altDeg, track.PeakValue));
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
            // Ensure top-left is within [0, w-1]/[0, h-1]
            int x0 = Math.Max(0, Math.Min(r.X, Math.Max(0, w - 1)));
            int y0 = Math.Max(0, Math.Min(r.Y, Math.Max(0, h - 1)));
            // Ensure bottom-right is within [0, w]/[0, h]
            int x1 = Math.Max(0, Math.Min(w, r.X + r.Width));
            int y1 = Math.Max(0, Math.Min(h, r.Y + r.Height));

            // Fix degenerate or inverted rectangles
            if (x1 <= x0) x1 = Math.Min(w, x0 + 1);
            if (y1 <= y0) y1 = Math.Min(h, y0 + 1);

            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        public void Dispose()
        {
            foreach (var w in _openLogs.Values) w.Dispose();
            _openLogs.Clear();
            if (_trackEventLog != null) _trackEventLog.Dispose();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\"", "\"\"");
        }
    }
}
