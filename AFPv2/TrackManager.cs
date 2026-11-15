// C# 7.3 / .NET Framework 4.8
using NLog;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MeteorDetection
{
    public sealed class TrackManagerConfig
    {
        /// <summary>対応付けを許容する最大距離（予測位置からの半径）[px]</summary>
        public double GateRadiusPx = 30;//AI:60　599px/s→20px/frame(30fps) 

        /// <summary>暫定トラックを「流星候補」として確定するために必要な連続検出数</summary>
        public int MinHitsToConfirm = 8;//AI:3

        /// <summary>暫定トラックを「流星候補」として確定するために必要な最大距離（予測位置からの半径）[px]</summary>
        public double MaxErrRadiusPx = 1.5;//追加　流星は等速直線運動に近似できるため。

        /// <summary>未確定トラックを破棄するまでに許容する連続未検出回数</summary>
        public int MaxMissesTentative = 2;

        /// <summary>確定トラックを終了とみなすまでに許容する連続未検出回数</summary>
        public int MaxMissesConfirmed = 6;

        /// <summary>この速度[px/s]を超える対応付けは誤対応とみなし許容しない</summary>
        public double MaxSpeedPxPerSec = 1000; //H=100km v=72km/s 角速度=72/100*180/pi=41.3deg/s 2608px/180deg=14.5px/deg 41.3*14.5=599px/s　→ 余裕を見て1000px/sまで許容(AI:4000)

        /// <summary>この速度[px/s]未満は静止光源・瞬きなどとみなし確定させない</summary>
        public double MinSpeedPxPerSec = 30; //AI:20   video 2Hzなので1/15
    }

    /// <summary>
    /// 各フレームの検出結果をトラックに対応付け、暫定→確定→終了のライフサイクルを管理する。
    /// 同時に存在する流星候補は通常ごく少数のため、貪欲最近傍法で十分な性能が出る
    /// （ハンガリアン法などは過剰性能でありCPU負荷の面でも不要）。
    /// </summary>
    public sealed class TrackManager
    {
        private readonly TrackManagerConfig _cfg;
        private readonly List<MeteorTrack> _tracks = new List<MeteorTrack>();
        private int _nextId = 1;

        /// <summary>暫定トラックが流星候補として確定した（＝追尾カメラへ導入指令を送るタイミング）</summary>
        public event Action<MeteorTrack> TrackConfirmed;

        /// <summary>UI などの外部ロギング用。TrackConfirmed の発火時に共通で通知する。</summary>
        public static Action<MeteorTrack> ConfirmedTrackLogHandler { get; set; }

        /// <summary>確定済みトラックの位置が更新された（＝途中経過を送るタイミング）</summary>
        public event Action<MeteorTrack> TrackUpdated;

        /// <summary>確定済みトラックが終了した（＝検知終了を送るタイミング）</summary>
        public event Action<MeteorTrack> TrackEnded;

        public TrackManager(TrackManagerConfig cfg = null)
        {
            _cfg = cfg ?? new TrackManagerConfig();
        }

        public IReadOnlyList<MeteorTrack> ActiveTracks => _tracks;

        public int TotalTrackConfirmedCount = 0;

        /// <summary>対応付けを許容する最大距離（予測位置からの半径）[px] 時間関数　</summary>
        public double tolerancePx(int tn, double err_max, double err_min = 3.0, int err_min_tn = 4)
        {
            double ans = err_max - ((err_max - err_min) / (err_min_tn - 1)) * (tn - 1);
            ans = Math.Max(ans, err_min);
            return (ans);
        }

        /// <summary>1フレーム分の検出結果を与えてトラックを更新する。</summary>
        public void Step(List<Detection> detections, DateTime t)
        {
            foreach (var tr in _tracks) tr.Predict(t);

            var unmatched = new List<Detection>(detections);

            // 確定トラックを優先して対応付ける（確定トラックの継続性を優先）
            foreach (var tr in _tracks.OrderByDescending(x => x.Confirmed))
            {
                var pred = tr.Position;
                int bestIdx = -1;
                double bestDist = double.MaxValue;

                for (int i = 0; i < unmatched.Count; i++)
                {
                    double dx = unmatched[i].Center.X - pred.X;
                    double dy = unmatched[i].Center.Y - pred.Y;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < bestDist) { bestDist = d; bestIdx = i; }
                }

                //if (bestIdx >= 0 && bestDist <= _cfg.GateRadiusPx)
                if (bestIdx >= 0 && bestDist <= tolerancePx(tr.Hits, _cfg.GateRadiusPx))
                {
                    var d = unmatched[bestIdx];
                    tr.Update(d.Center, t, d.PeakValue);
                    unmatched.RemoveAt(bestIdx);
                    Console.WriteLine($"bIdx:{bestIdx} bDist:{bestDist:F2} tr.Hits:{tr.Hits} ");//debug

                    if (!tr.Confirmed && tr.Hits >= _cfg.MinHitsToConfirm)
                    {
                        double speed = Length(tr.VelocityPxPerSec);
                        Console.WriteLine($"speed:{speed:F2} Min:{_cfg.MinSpeedPxPerSec} Max:{_cfg.MaxSpeedPxPerSec} ");//debug
                        if (speed >= _cfg.MinSpeedPxPerSec && speed <= _cfg.MaxSpeedPxPerSec && bestDist <= _cfg.MaxErrRadiusPx)
                        {
                            tr.Confirm();
                            TrackConfirmed?.Invoke(tr);
                            ConfirmedTrackLogHandler?.Invoke(tr);
                            TotalTrackConfirmedCount++;
                            var lsm_error = LSMerror(tr.History);
                            Console.WriteLine($"Track {tr.Id} confirmed at {t:HH:mm:ss.fff}, speed={speed:F1}px/s, LSM error={lsm_error:F2}px");
                            foreach (var dp in tr.History)
                            {
                                //Console.WriteLine($"{dp.T} {dp.Pos.X:F1},{dp.Pos.Y:F1}");
                            }
                        }
                    }
                    else if (tr.Confirmed)
                    {
                        TrackUpdated?.Invoke(tr);
                    }
                }
                else
                {
                    tr.MarkMissed();
                }
            }

            // 対応付かなかった検出から新規の暫定トラックを生成
            foreach (var d in unmatched)
                _tracks.Add(new MeteorTrack(_nextId++, d.Center, t, d.PeakValue));

            // 未検出が続いたトラックの終了・破棄
            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                var tr = _tracks[i];
                int maxMiss = tr.Confirmed ? _cfg.MaxMissesConfirmed : _cfg.MaxMissesTentative;
                if (tr.Misses > maxMiss)
                {
                    if (tr.Confirmed)
                    {
                        tr.Finish();
                        TrackEnded?.Invoke(tr);
                    }
                    _tracks.RemoveAt(i);
                }
            }
        }

        private static double Length(Point2f p) => Math.Sqrt(p.X * (double)p.X + p.Y * (double)p.Y);


        /// <summary>
        /// 最小二乗法による誤差(pixel単位)の計算
        /// </summary>
        public double LSMerror(List<(DateTime T, Point2f Pos)> history)
        {
            if (history == null || history.Count < 2)
                return 0.0;
            long startTicks = history[0].T.Ticks;
            var ticks = history.Select(h => (double)(h.T.Ticks - startTicks)).ToList();
            var xs = history.Select(h => (double)h.Pos.X).ToList();
            var ys = history.Select(h => (double)h.Pos.Y).ToList();
            var (mx, bx) = LeastSquares(ticks, xs);
            var (my, by) = LeastSquares(ticks, ys);

            double sumSquaredError = 0.0;
            foreach (var (T, Pos) in history)
            {
                double predictedX = mx * (T.Ticks - startTicks) + bx;
                double predictedY = my * (T.Ticks - startTicks) + by;
                double errorX = Pos.X - predictedX;
                double errorY = Pos.Y - predictedY;
                sumSquaredError += errorX * errorX + errorY * errorY;
                Console.WriteLine($"{T.Ticks - startTicks:D8} {Pos.X:F1},{Pos.Y:F1} -> {predictedX:F1},{predictedY:F1} (err: {errorX:F1}, {errorY:F1})");
            }
            return Math.Sqrt(sumSquaredError / history.Count);
        }
        /// <summary>
        /// 最小二乗法による単回帰の計算
        /// </summary>
        /// <param name="xValues">X座標のリスト</param>
        /// <param name="yValues">Y座標のリスト</param>
        /// <returns>(傾き m, 切片 b)</returns>
        public (double m, double b) LeastSquares(IList<double> xValues, IList<double> yValues)
        {
            if (xValues == null || yValues == null)
                throw new ArgumentNullException("入力リストが null です。");

            if (xValues.Count != yValues.Count)
                throw new ArgumentException("X と Y の要素数が一致していません。");

            int n = xValues.Count;
            if (n < 2)
                throw new ArgumentException("少なくとも2点以上のデータが必要です。");

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

            for (int i = 0; i < n; i++)
            {
                double x = xValues[i];
                double y = yValues[i];

                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            // 傾き m と切片 b の計算式
            double m = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            double b = (sumY - m * sumX) / n;

            return (m, b);
        }
    }
    }
