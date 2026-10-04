// C# 7.3 / .NET Framework 4.8
using System;

namespace MeteorDetection
{
    /// <summary>
    /// 1軸・等速直線運動モデル（状態 = 位置, 速度）の2状態カルマンフィルタ。
    /// x軸・y軸それぞれ独立に持つことで、2次元の等速運動追跡を軽量に実現する
    /// （フル4x4共分散を持つ結合カルマンフィルタより計算量が小さい）。
    /// </summary>
    public sealed class Kalman1D
    {
        public double Pos { get; private set; }
        public double Vel { get; private set; }

        // 誤差共分散 (2x2)
        private double _p00, _p01, _p10, _p11;

        private readonly double _qPos; // プロセスノイズ強度（位置）
        private readonly double _qVel; // プロセスノイズ強度（速度）
        private readonly double _rMeas; // 観測ノイズ分散

        /// <param name="pos">初期位置</param>
        /// <param name="vel">初期速度</param>
        /// <param name="posVar">初期位置分散</param>
        /// <param name="velVar">初期速度分散（流星は速度不明なので大きめに）</param>
        /// <param name="qPos">プロセスノイズ（位置）。値が大きいほど観測を信頼</param>
        /// <param name="qVel">プロセスノイズ（速度）。値が大きいほど速度変化に追従しやすい</param>
        /// <param name="rMeas">観測ノイズ分散（検出位置のばらつき、px^2）</param>
        public Kalman1D(double pos, double vel, double posVar, double velVar,
            double qPos, double qVel, double rMeas)
        {
            Pos = pos;
            Vel = vel;
            _p00 = posVar; _p01 = 0; _p10 = 0; _p11 = velVar;
            _qPos = qPos; _qVel = qVel; _rMeas = rMeas;
        }

        /// <summary>dt秒だけ状態を予測（観測なし）で進める。</summary>
        public void Predict(double dt)
        {
            if (dt <= 0) return;

            Pos += Vel * dt;

            // F = [[1,dt],[0,1]] による P' = F P F^T + Q
            double p00 = _p00 + dt * (_p10 + _p01) + dt * dt * _p11 + _qPos * dt;
            double p01 = _p01 + dt * _p11;
            double p10 = _p10 + dt * _p11;
            double p11 = _p11 + _qVel * dt;

            _p00 = p00; _p01 = p01; _p10 = p10; _p11 = p11;
        }

        /// <summary>位置の観測値 z で更新する。</summary>
        public void Update(double z)
        {
            double y = z - Pos;         // 残差(イノベーション)
            double s = _p00 + _rMeas;   // 残差の共分散
            if (s <= 0) return;

            double k0 = _p00 / s; // 位置ゲイン
            double k1 = _p10 / s; // 速度ゲイン

            Pos += k0 * y;
            Vel += k1 * y;

            double p00 = (1 - k0) * _p00;
            double p01 = (1 - k0) * _p01;
            double p10 = _p10 - k1 * _p00;
            double p11 = _p11 - k1 * _p01;

            _p00 = p00; _p01 = p01; _p10 = p10; _p11 = p11;
        }
    }
}
