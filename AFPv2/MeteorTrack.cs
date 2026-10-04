// C# 7.3 / .NET Framework 4.8
using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace MeteorDetection
{
    /// <summary>流星・火球候補の1トラック。x軸/y軸独立のKalman1Dで位置・速度を推定する。</summary>
    public sealed class MeteorTrack
    {
        public int Id { get; }
        public int Hits { get; private set; } = 1;
        public int Misses { get; private set; } = 0;
        public bool Confirmed { get; private set; }
        public bool Finished { get; private set; }
        public DateTime FirstSeen { get; }
        public DateTime LastSeen { get; private set; }
        public double PeakValue { get; private set; }

        /// <summary>(時刻, 検出位置[px]) の履歴。保存・解析用。</summary>
        public List<(DateTime T, Point2f Pos)> History { get; } = new List<(DateTime, Point2f)>();

        private readonly Kalman1D _kx, _ky;
        private DateTime _lastTime;

        public MeteorTrack(int id, Point2f initPos, DateTime t, double peakValue)
        {
            Id = id;
            FirstSeen = LastSeen = _lastTime = t;
            PeakValue = peakValue;

            // 初期速度0、速度の初期不確かさは大きく（流星は方向・速度不明のため）
            _kx = new Kalman1D(initPos.X, 0, posVar: 9, velVar: 2500, qPos: 4, qVel: 400, rMeas: 4);
            _ky = new Kalman1D(initPos.Y, 0, posVar: 9, velVar: 2500, qPos: 4, qVel: 400, rMeas: 4);

            History.Add((t, initPos));
        }

        /// <summary>現在の推定位置 [px]（最終更新時刻での値）。</summary>
        public Point2f Position => new Point2f((float)_kx.Pos, (float)_ky.Pos);

        /// <summary>現在の推定速度 [px/s]。</summary>
        public Point2f VelocityPxPerSec => new Point2f((float)_kx.Vel, (float)_ky.Vel);

        /// <summary>時刻tまで観測なしで予測を進める（対応付け前に毎フレーム呼ぶ）。</summary>
        public void Predict(DateTime t)
        {
            double dt = (t - _lastTime).TotalSeconds;
            if (dt <= 0) return;
            _kx.Predict(dt);
            _ky.Predict(dt);
            _lastTime = t;
        }

        /// <summary>観測(検出位置)でフィルタを更新する。</summary>
        public void Update(Point2f pos, DateTime t, double peakValue)
        {
            _kx.Update(pos.X);
            _ky.Update(pos.Y);
            Hits++;
            Misses = 0;
            LastSeen = t;
            if (peakValue > PeakValue) PeakValue = peakValue;

            History.Add((t, pos));
            if (History.Count > 300) History.RemoveAt(0); // メモリ上限（約10秒分@30fps）
        }

        public void MarkMissed() => Misses++;
        public void Confirm() => Confirmed = true;
        public void Finish() => Finished = true;
    }
}
