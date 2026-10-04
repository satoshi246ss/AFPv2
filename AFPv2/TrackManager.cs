// C# 7.3 / .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace MeteorDetection
{
    public sealed class TrackManagerConfig
    {
        /// <summary>対応付けを許容する最大距離（予測位置からの半径）[px]</summary>
        public double GateRadiusPx = 60;

        /// <summary>暫定トラックを「流星候補」として確定するために必要な連続検出数</summary>
        public int MinHitsToConfirm = 3;

        /// <summary>未確定トラックを破棄するまでに許容する連続未検出回数</summary>
        public int MaxMissesTentative = 2;

        /// <summary>確定トラックを終了とみなすまでに許容する連続未検出回数</summary>
        public int MaxMissesConfirmed = 4;

        /// <summary>この速度[px/s]を超える対応付けは誤対応とみなし許容しない</summary>
        public double MaxSpeedPxPerSec = 4000;

        /// <summary>この速度[px/s]未満は静止光源・瞬きなどとみなし確定させない</summary>
        public double MinSpeedPxPerSec = 20;
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

        /// <summary>確定済みトラックの位置が更新された（＝途中経過を送るタイミング）</summary>
        public event Action<MeteorTrack> TrackUpdated;

        /// <summary>確定済みトラックが終了した（＝検知終了を送るタイミング）</summary>
        public event Action<MeteorTrack> TrackEnded;

        public TrackManager(TrackManagerConfig cfg = null)
        {
            _cfg = cfg ?? new TrackManagerConfig();
        }

        public IReadOnlyList<MeteorTrack> ActiveTracks => _tracks;

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

                if (bestIdx >= 0 && bestDist <= _cfg.GateRadiusPx)
                {
                    var d = unmatched[bestIdx];
                    tr.Update(d.Center, t, d.PeakValue);
                    unmatched.RemoveAt(bestIdx);

                    if (!tr.Confirmed && tr.Hits >= _cfg.MinHitsToConfirm)
                    {
                        double speed = Length(tr.VelocityPxPerSec);
                        if (speed >= _cfg.MinSpeedPxPerSec && speed <= _cfg.MaxSpeedPxPerSec)
                        {
                            tr.Confirm();
                            TrackConfirmed?.Invoke(tr);
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
    }
}
