// C# 7.3 / .NET Framework 4.8
// 全天魚眼カメラ (fθ方式, ほぼ天頂向き) の 地平座標 <-> CCD座標 変換クラス
//
// 座標の約束
//   地平座標 : Az = 北を0°として東回り(北0, 東90, 南180, 西270) [deg]
//              Alt = 地平線0°, 天頂90° [deg]
//              (大気差は含まない。必要なら呼び出し側で補正する)
//   CCD座標  : ピクセル中心を整数とし、左上ピクセルの中心が (0,0)。
//              x は右向き、y は下向きに増加。2608x2608 なら画像中心は (1303.5, 1303.5)。
//   理想カメラ(誤差ゼロ) : 光軸=天頂、画像の上=北。
//              天頂を見上げているので、画像の右=西、左=東 になる(天球図と同じ見え方)。
//              (画像を左右反転して保存している場合は MirrorX = true にする)
//
// 誤差の表現
//   TiltXDeg, TiltYDeg : 光軸の天頂からのズレを表す小さな回転角 (カメラ座標系 X軸/Y軸まわり)
//   RollDeg            : 光軸まわりの回転誤差 (画像の「上」が北からどれだけ回っているか)
//   実際の光軸方向は GetOpticalAxis() で (Az, Alt) として取得できる。
//
// 収差補正 (fθ からのズレ)
//   r = f * ( θ + K1*θ^3 + K2*θ^5 )      θ: 光軸からの角度[rad]
//   K1 = K2 = 0 なら純粋な fθ (r = f*θ)。

using System;

namespace AllSkyCamera
{
    /// <summary>恒星などの観測点 (収差係数・姿勢の推定用)</summary>
    public struct StarObservation
    {
        public double AzDeg;   // 計算で得た(または既知の)方位
        public double AltDeg;  // 計算で得た(または既知の)高度
        public double X;       // 画像上で測定した重心 x
        public double Y;       // 画像上で測定した重心 y

        public StarObservation(double azDeg, double altDeg, double x, double y)
        {
            AzDeg = azDeg; AltDeg = altDeg; X = x; Y = y;
        }
    }

    public sealed class FisheyeCameraModel
    {
        private const double Deg2Rad = Math.PI / 180.0;
        private const double Rad2Deg = 180.0 / Math.PI;

        // ---- パラメータ ------------------------------------------------------

        /// <summary>画像幅 [px]</summary>
        public int Width { get; }
        /// <summary>画像高さ [px]</summary>
        public int Height { get; }

        private double _cx, _cy, _f, _tiltX, _tiltY, _roll, _k1, _k2;
        private bool _dirty = true;
        private readonly double[] _m = new double[9]; // 地平(E,N,U) -> カメラ(右,下,前) の回転行列

        /// <summary>光学中心 x [px]</summary>
        public double CenterX { get { return _cx; } set { _cx = value; } }
        /// <summary>光学中心 y [px]</summary>
        public double CenterY { get { return _cy; } set { _cy = value; } }
        /// <summary>焦点距離 [px/rad] (= 焦点距離[mm] / 画素ピッチ[mm])</summary>
        public double FocalLengthPx { get { return _f; } set { _f = value; } }

        /// <summary>光軸のズレ (カメラX軸まわり) [deg]</summary>
        public double TiltXDeg { get { return _tiltX; } set { _tiltX = value; _dirty = true; } }
        /// <summary>光軸のズレ (カメラY軸まわり) [deg]</summary>
        public double TiltYDeg { get { return _tiltY; } set { _tiltY = value; _dirty = true; } }
        /// <summary>光軸まわりの回転誤差 [deg]</summary>
        public double RollDeg { get { return _roll; } set { _roll = value; _dirty = true; } }

        /// <summary>fθからのズレ 3次項係数</summary>
        public double K1 { get { return _k1; } set { _k1 = value; } }
        /// <summary>fθからのズレ 5次項係数</summary>
        public double K2 { get { return _k2; } set { _k2 = value; } }

        /// <summary>画像を左右反転している場合 true</summary>
        public bool MirrorX { get; set; }

        /// <summary>変換を有効とする光軸からの最大角 [deg]。185°レンズなら 92.5。</summary>
        public double MaxThetaDeg { get; set; } = 92.5;

        // ---- コンストラクタ --------------------------------------------------

        /// <param name="width">画像幅 (例 2608)</param>
        /// <param name="height">画像高さ (例 2608)</param>
        /// <param name="focalLengthPx">焦点距離 [px/rad] の初期値</param>
        public FisheyeCameraModel(int width, int height, double focalLengthPx)
        {
            Width = width;
            Height = height;
            _cx = (width - 1) * 0.5;
            _cy = (height - 1) * 0.5;
            _f = focalLengthPx;
        }

        /// <summary>
        /// 2608x2608 用の初期値。焦点距離は仮値 (2.7mm / 3.45um = 782.6 px/rad)。
        /// 実測で必ず更新すること。
        /// </summary>
        public static FisheyeCameraModel CreateDefault()
        {
            return new FisheyeCameraModel(2608, 2608, 2.7 / 0.00345);
        }

        // ---- 1. 地平座標 -> CCD座標 -----------------------------------------

        /// <summary>地平座標(Az,Alt)[deg] から CCD座標(x,y)[px] へ変換する。</summary>
        /// <returns>視野外(光軸から MaxThetaDeg 超)なら false</returns>
        public bool HorizontalToPixel(double azDeg, double altDeg, out double x, out double y)
        {
            UpdateMatrix();

            double az = azDeg * Deg2Rad, alt = altDeg * Deg2Rad;
            double ca = Math.Cos(alt);
            // 地平座標の単位ベクトル (E, N, U)
            double hE = ca * Math.Sin(az);
            double hN = ca * Math.Cos(az);
            double hU = Math.Sin(alt);

            // カメラ座標 (右, 下, 前)
            double cx = _m[0] * hE + _m[1] * hN + _m[2] * hU;
            double cy = _m[3] * hE + _m[4] * hN + _m[5] * hU;
            double cz = _m[6] * hE + _m[7] * hN + _m[8] * hU;

            double sinTheta = Math.Sqrt(cx * cx + cy * cy);
            double theta = Math.Atan2(sinTheta, cz);

            if (theta > MaxThetaDeg * Deg2Rad)
            {
                x = double.NaN; y = double.NaN;
                return false;
            }

            double r = _f * ThetaToRho(theta);

            double dx, dy;
            if (sinTheta < 1e-12) { dx = 0; dy = 0; }
            else { dx = r * cx / sinTheta; dy = r * cy / sinTheta; }

            x = _cx + (MirrorX ? -dx : dx);
            y = _cy + dy;
            return true;
        }

        // ---- 2. CCD座標 -> 地平座標 -----------------------------------------

        /// <summary>CCD座標(x,y)[px] から 地平座標(Az,Alt)[deg] へ変換する。Az は 0〜360。</summary>
        /// <returns>光軸から MaxThetaDeg を超える点なら false</returns>
        public bool PixelToHorizontal(double x, double y, out double azDeg, out double altDeg)
        {
            UpdateMatrix();

            double dx = x - _cx;
            double dy = y - _cy;
            if (MirrorX) dx = -dx;

            double r = Math.Sqrt(dx * dx + dy * dy);
            double theta = RhoToTheta(r / _f);

            if (double.IsNaN(theta) || theta > MaxThetaDeg * Deg2Rad)
            {
                azDeg = double.NaN; altDeg = double.NaN;
                return false;
            }

            double sinT = Math.Sin(theta), cosT = Math.Cos(theta);
            double cx, cy;
            if (r < 1e-12) { cx = 0; cy = 0; }
            else { cx = sinT * dx / r; cy = sinT * dy / r; }
            double cz = cosT;

            // 逆回転 (直交行列なので転置)
            double hE = _m[0] * cx + _m[3] * cy + _m[6] * cz;
            double hN = _m[1] * cx + _m[4] * cy + _m[7] * cz;
            double hU = _m[2] * cx + _m[5] * cy + _m[8] * cz;

            altDeg = Math.Asin(Clamp(hU, -1.0, 1.0)) * Rad2Deg;
            double az = Math.Atan2(hE, hN) * Rad2Deg;
            if (az < 0) az += 360.0;
            azDeg = az;
            return true;
        }

        // ---- 3. 光軸・回転誤差 ------------------------------------------------

        /// <summary>現在のパラメータでの実際の光軸方向を地平座標で返す (天頂近傍では Az は不安定)。</summary>
        public void GetOpticalAxis(out double azDeg, out double altDeg)
        {
            UpdateMatrix();
            // 光軸(カメラ座標の (0,0,1)) を地平座標に戻す = 行列の3行目
            double hE = _m[6], hN = _m[7], hU = _m[8];
            altDeg = Math.Asin(Clamp(hU, -1.0, 1.0)) * Rad2Deg;
            double az = Math.Atan2(hE, hN) * Rad2Deg;
            if (az < 0) az += 360.0;
            azDeg = az;
        }

        /// <summary>光軸・回転誤差をまとめて設定する。</summary>
        public void SetPointingError(double tiltXDeg, double tiltYDeg, double rollDeg)
        {
            _tiltX = tiltXDeg; _tiltY = tiltYDeg; _roll = rollDeg;
            _dirty = true;
        }

        // ---- 4. 収差 (fθ からのズレ) ----------------------------------------

        /// <summary>収差係数を設定する。(0,0) で純fθ。</summary>
        public void SetDistortion(double k1, double k2)
        {
            _k1 = k1; _k2 = k2;
        }

        // θ[rad] -> rho (= r/f)
        private double ThetaToRho(double theta)
        {
            double t2 = theta * theta;
            return theta * (1.0 + t2 * (_k1 + t2 * _k2));
        }

        // rho (= r/f) -> θ[rad]  ニュートン法で逆変換
        private double RhoToTheta(double rho)
        {
            if (_k1 == 0.0 && _k2 == 0.0) return rho;

            double theta = rho;
            for (int i = 0; i < 20; i++)
            {
                double t2 = theta * theta;
                double g = theta * (1.0 + t2 * (_k1 + t2 * _k2)) - rho;
                double dg = 1.0 + t2 * (3.0 * _k1 + 5.0 * t2 * _k2);
                if (Math.Abs(dg) < 1e-12) return double.NaN;
                double step = g / dg;
                theta -= step;
                if (Math.Abs(step) < 1e-13) return theta;
            }
            return theta;
        }

        // ---- 5. 係数推定の土台 (将来) ---------------------------------------

        /// <summary>推定対象パラメータ数: cx, cy, f, tiltX, tiltY, roll, k1, k2</summary>
        public const int ParameterCount = 8;

        public double[] GetParameters()
        {
            return new[] { _cx, _cy, _f, _tiltX, _tiltY, _roll, _k1, _k2 };
        }

        public void SetParameters(double[] p)
        {
            if (p == null || p.Length != ParameterCount)
                throw new ArgumentException("パラメータ数が不正です。");
            _cx = p[0]; _cy = p[1]; _f = p[2];
            _tiltX = p[3]; _tiltY = p[4]; _roll = p[5];
            _k1 = p[6]; _k2 = p[7];
            _dirty = true;
        }

        /// <summary>
        /// 1個の恒星について、モデルから予測した画像位置と測定位置の差 (測定 - 予測) [px]。
        /// 最小二乗法(Levenberg-Marquardt, Nelder-Mead 等)の残差関数として使う。
        /// </summary>
        public bool TryResidual(StarObservation s, out double dxPx, out double dyPx)
        {
            double px, py;
            if (!HorizontalToPixel(s.AzDeg, s.AltDeg, out px, out py))
            {
                dxPx = double.NaN; dyPx = double.NaN;
                return false;
            }
            dxPx = s.X - px;
            dyPx = s.Y - py;
            return true;
        }

        /// <summary>複数星の RMS 残差 [px]。使えた星が無ければ NaN。</summary>
        public double ComputeRms(StarObservation[] stars)
        {
            double sum = 0; int n = 0;
            foreach (var s in stars)
            {
                double dx, dy;
                if (TryResidual(s, out dx, out dy)) { sum += dx * dx + dy * dy; n++; }
            }
            return n == 0 ? double.NaN : Math.Sqrt(sum / n);
        }

        // ---- 内部: 回転行列の構築 -------------------------------------------

        private void UpdateMatrix()
        {
            if (!_dirty) return;

            // 理想カメラ (光軸=天頂, 画像上=北) : 地平(E,N,U) -> カメラ(右,下,前)
            //   右 = -E (西), 下 = -N (南), 前 = U
            double[] r0 =
            {
                -1,  0, 0,
                 0, -1, 0,
                 0,  0, 1
            };

            // 誤差回転 E = Rz(roll) * Ry(tiltY) * Rx(tiltX)
            double[] e = Mul(Rz(_roll * Deg2Rad), Mul(Ry(_tiltY * Deg2Rad), Rx(_tiltX * Deg2Rad)));

            double[] m = Mul(e, r0);
            Array.Copy(m, _m, 9);
            _dirty = false;
        }

        private static double[] Rx(double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            return new[] { 1.0, 0, 0, 0, c, -s, 0, s, c };
        }

        private static double[] Ry(double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            return new[] { c, 0, s, 0, 1.0, 0, -s, 0, c };
        }

        private static double[] Rz(double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            return new[] { c, -s, 0, s, c, 0, 0, 0, 1.0 };
        }

        private static double[] Mul(double[] a, double[] b)
        {
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
            return r;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
