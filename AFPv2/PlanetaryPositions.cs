using System;

namespace AstroCalc
{
    /// <summary>
    /// 太陽・月・惑星の位置計算クラス
    ///
    /// アルゴリズム:
    ///   太陽     : Meeus "Astronomical Algorithms" 第25章 低精度太陽位置式
    ///              （太陽赤経・赤緯として一般に数十秒角程度の精度）
    ///   月       : Meeus 第47章 ELP2000-82B の主要項を抜粋した短縮版
    ///              （振幅の大きい項のみを採用。目安精度 ~0.01〜0.05°）
    ///   水星〜土星: JPL(Standish, 1992)「主要惑星の近似位置のためのケプラー軌道要素」
    ///              （有効期間 目安 1800〜2050年）+ ケプラー方程式による楕円軌道計算。
    ///              VSOP87D の主要項短縮版ではなく、平均軌道要素方式を採用している点に注意。
    ///              精度は角分〜十数角分程度（木星・土星でやや悪化）であり、
    ///              VSOP87D 短縮版で期待される 0.001〜0.003° の精度には達しない。
    ///              より高精度が必要な場合は、正式な VSOP87D 係数表を別途組み込むこと。
    ///
    /// 返り値: 「その時刻の視赤道座標」（視黄道座標 → 真の黄道傾斜角で変換）
    ///         AstronomicalCoordinates クラスで地平座標に変換する際は
    ///         ApplyPrecessionNutation を通す必要がある。
    ///         ただし惑星位置の用途では通常「視赤道座標 ≒ J2000」として
    ///         AstronomicalCoordinates.EquatorialToHorizontal に渡して問題ない
    ///         （歳差補正は内部で行われる）。
    /// </summary>
    public static class PlanetaryPositions
    {
        #region 定数

        private const double Rad = Math.PI / 180.0;
        private const double Deg = 180.0 / Math.PI;
        private const double J2000 = 2451545.0;

        #endregion

        // =========================================================================
        // 公開 API
        // =========================================================================

        /// <summary>太陽の視赤経・赤緯（視赤道座標、度）</summary>
        public static void GetSun(double jd, out double ra, out double dec)
        {
            SunEcliptic(jd, out double lon, out double lat, out _);
            EclipticToEquatorial(lon, lat, jd, out ra, out dec);
        }

        /// <summary>月の視赤経・赤緯（視赤道座標、度）</summary>
        public static void GetMoon(double jd, out double ra, out double dec)
        {
            MoonEcliptic(jd, out double lon, out double lat, out _);
            EclipticToEquatorial(lon, lat, jd, out ra, out dec);
        }

        /// <summary>水星の赤経・赤緯（視赤道座標、度）</summary>
        public static void GetMercury(double jd, out double ra, out double dec)
            => PlanetGeoEquatorial(jd, Planet.Mercury, out ra, out dec);

        /// <summary>金星の赤経・赤緯（視赤道座標、度）</summary>
        public static void GetVenus(double jd, out double ra, out double dec)
            => PlanetGeoEquatorial(jd, Planet.Venus, out ra, out dec);

        /// <summary>火星の赤経・赤緯（視赤道座標、度）</summary>
        public static void GetMars(double jd, out double ra, out double dec)
            => PlanetGeoEquatorial(jd, Planet.Mars, out ra, out dec);

        /// <summary>木星の赤経・赤緯（視赤道座標、度）</summary>
        public static void GetJupiter(double jd, out double ra, out double dec)
            => PlanetGeoEquatorial(jd, Planet.Jupiter, out ra, out dec);

        /// <summary>土星の赤経・赤緯（視赤道座標、度）</summary>
        public static void GetSaturn(double jd, out double ra, out double dec)
            => PlanetGeoEquatorial(jd, Planet.Saturn, out ra, out dec);

        /// <summary>天体名を指定して赤経・赤緯を取得</summary>
        public static void GetBody(string name, double jd, out double ra, out double dec)
        {
            switch (name.ToLowerInvariant())
            {
                case "sun":     case "太陽":   GetSun    (jd, out ra, out dec); break;
                case "moon":    case "月":     GetMoon   (jd, out ra, out dec); break;
                case "mercury": case "水星":   GetMercury(jd, out ra, out dec); break;
                case "venus":   case "金星":   GetVenus  (jd, out ra, out dec); break;
                case "mars":    case "火星":   GetMars   (jd, out ra, out dec); break;
                case "jupiter": case "木星":   GetJupiter(jd, out ra, out dec); break;
                case "saturn":  case "土星":   GetSaturn (jd, out ra, out dec); break;
                default: throw new ArgumentException($"未知の天体: {name}");
            }
        }

        // =========================================================================
        // 共通ユーティリティ
        // =========================================================================

        private static double JulianCenturies(double jd) => (jd - J2000) / 36525.0;

        private static double NormalizeDegrees(double deg)
        {
            double d = deg % 360.0;
            if (d < 0) d += 360.0;
            return d;
        }

        private static double NormalizeSignedDegrees(double deg)
        {
            double d = deg % 360.0;
            if (d > 180.0) d -= 360.0;
            if (d < -180.0) d += 360.0;
            return d;
        }

        /// <summary>平均黄道傾斜角 ε0（度）。Meeus (22.2)</summary>
        private static double MeanObliquity(double t)
        {
            // IAU 1980 近似式。 84381.448" - 46.8150"T - 0.00059"T^2 + 0.001813"T^3
            double arcsec = 84381.448 - 46.8150 * t - 0.00059 * t * t + 0.001813 * t * t * t;
            return arcsec / 3600.0;
        }

        /// <summary>章動（黄経方向 Δψ、黄道傾斜方向 Δε）を度単位で返す。Meeus 第22章 簡易4項式</summary>
        private static void Nutation(double t, out double deltaPsiDeg, out double deltaEpsDeg)
        {
            double omega = NormalizeDegrees(125.04452 - 1934.136261 * t) * Rad;
            double lSun = NormalizeDegrees(280.4665 + 36000.7698 * t) * Rad;
            double lMoon = NormalizeDegrees(218.3165 + 481267.8813 * t) * Rad;

            double dPsiArcsec = -17.20 * Math.Sin(omega)
                                 - 1.32 * Math.Sin(2 * lSun)
                                 - 0.23 * Math.Sin(2 * lMoon)
                                 + 0.21 * Math.Sin(2 * omega);

            double dEpsArcsec = 9.20 * Math.Cos(omega)
                                 + 0.57 * Math.Cos(2 * lSun)
                                 + 0.10 * Math.Cos(2 * lMoon)
                                 - 0.09 * Math.Cos(2 * omega);

            deltaPsiDeg = dPsiArcsec / 3600.0;
            deltaEpsDeg = dEpsArcsec / 3600.0;
        }

        /// <summary>
        /// 視黄道座標（黄経・黄緯、度）を、その時刻の真の黄道傾斜角を用いて
        /// 視赤道座標（赤経・赤緯、度）に変換する。
        /// lon には呼び出し側であらかじめ章動 Δψ を加算しておくこと（Sun/Moon/Planet 各実装で対応済み）。
        /// </summary>
        private static void EclipticToEquatorial(double lonDeg, double latDeg, double jd, out double ra, out double dec)
        {
            double t = JulianCenturies(jd);
            double eps0 = MeanObliquity(t);
            Nutation(t, out _, out double dEps);
            double eps = (eps0 + dEps) * Rad;

            double lonRad = lonDeg * Rad;
            double latRad = latDeg * Rad;

            double y = Math.Sin(lonRad) * Math.Cos(eps) - Math.Tan(latRad) * Math.Sin(eps);
            double x = Math.Cos(lonRad);
            double raDeg = NormalizeDegrees(Math.Atan2(y, x) * Deg);

            double sinDec = Math.Sin(latRad) * Math.Cos(eps) + Math.Cos(latRad) * Math.Sin(eps) * Math.Sin(lonRad);
            double decDeg = Math.Asin(Clamp(sinDec, -1.0, 1.0)) * Deg;

            ra = raDeg;
            dec = decDeg;
        }

        private static double Clamp(double v, double min, double max)
            => v < min ? min : (v > max ? max : v);

        // =========================================================================
        // 太陽 (Meeus 第25章)
        // =========================================================================

        private static void SunEcliptic(double jd, out double lonDeg, out double latDeg, out double rAu)
        {
            double t = JulianCenturies(jd);

            double l0 = NormalizeDegrees(280.46646 + 36000.76983 * t + 0.0003032 * t * t);
            double m = NormalizeDegrees(357.52911 + 35999.05029 * t - 0.0001537 * t * t);
            double mRad = m * Rad;

            double e = 0.016708634 - 0.000042037 * t - 0.0000001267 * t * t;

            double c = (1.914602 - 0.004817 * t - 0.000014 * t * t) * Math.Sin(mRad)
                     + (0.019993 - 0.000101 * t) * Math.Sin(2 * mRad)
                     + 0.000289 * Math.Sin(3 * mRad);

            double trueLon = l0 + c;
            double trueAnomaly = m + c;

            double omega = 125.04 - 1934.136 * t;
            double apparentLon = NormalizeDegrees(trueLon - 0.00569 - 0.00478 * Math.Sin(omega * Rad));

            double r = (1.000001018 * (1 - e * e)) / (1 + e * Math.Cos(trueAnomaly * Rad));

            lonDeg = apparentLon;
            latDeg = 0.0; // 太陽の黄緯は最大でも約1.2秒角のため低精度計算では0とみなす
            rAu = r;
        }

        // =========================================================================
        // 月 (Meeus 第47章 ELP2000-82B 主要項短縮版)
        // =========================================================================

        // (D, M, M', F, 黄経振幅[1e-6°], 距離振幅[1e-3 km])
        // 振幅の大きい項を抜粋（フルテーブルは全60項）。M を含む項の振幅は呼び出し側で E^|M係数| を乗じる。
        private static readonly int[][] MoonLR = new int[][]
        {
            //  D,  M, M', F,   Σl,      Σr
            new[]{ 0,  0,  1,  0, 6288774, -20905355 },
            new[]{ 2,  0, -1,  0, 1274027,  -3699111 },
            new[]{ 2,  0,  0,  0,  658314,  -2955968 },
            new[]{ 0,  0,  2,  0,  213618,   -569925 },
            new[]{ 0,  1,  0,  0, -185116,     48888 },
            new[]{ 0,  0,  0,  2, -114332,     -3149 },
            new[]{ 2,  0, -2,  0,   58793,    246158 },
            new[]{ 2, -1, -1,  0,   57066,   -152138 },
            new[]{ 2,  0,  1,  0,   53322,   -170733 },
            new[]{ 2, -1,  0,  0,   45758,   -204586 },
            new[]{ 0,  1, -1,  0,  -40923,   -129620 },
            new[]{ 1,  0,  0,  0,  -34720,    108743 },
            new[]{ 0,  1,  1,  0,  -30383,    104755 },
            new[]{ 2,  0,  0, -2,   15327,     10321 },
            new[]{ 0,  0,  1,  2,  -12528,         0 },
            new[]{ 0,  0,  1, -2,   10980,     79661 },
            new[]{ 4,  0, -1,  0,   10675,    -34782 },
            new[]{ 0,  0,  3,  0,   10034,    -23210 },
            new[]{ 4,  0, -2,  0,    8548,    -21636 },
            new[]{ 2,  1, -1,  0,   -7888,     24208 },
            new[]{ 2,  1,  0,  0,   -6766,     30824 },
            new[]{ 1,  0, -1,  0,   -5163,     -8379 },
            new[]{ 1,  1,  0,  0,    4987,    -16675 },
            new[]{ 2, -1,  1,  0,    4036,    -12831 },
            new[]{ 2,  0,  2,  0,    3994,    -10445 },
            new[]{ 4,  0,  0,  0,    3861,    -11650 },
            new[]{ 2,  0, -3,  0,    3665,     14403 },
            new[]{ 0,  1, -2,  0,   -2689,     -7003 },
            new[]{ 2,  0, -1,  2,   -2602,         0 },
            new[]{ 2, -1, -2,  0,    2390,     10056 },
        };

        // (D, M, M', F, 黄緯振幅[1e-6°])
        private static readonly int[][] MoonB = new int[][]
        {
            new[]{ 0,  0,  0,  1, 5128122 },
            new[]{ 0,  0,  1,  1,  280602 },
            new[]{ 0,  0,  1, -1,  277693 },
            new[]{ 2,  0,  0, -1,  173237 },
            new[]{ 2,  0, -1,  1,   55413 },
            new[]{ 2,  0, -1, -1,   46271 },
            new[]{ 2,  0,  0,  1,   32573 },
            new[]{ 0,  0,  2,  1,   17198 },
            new[]{ 2,  0,  1, -1,    9266 },
            new[]{ 0,  0,  2, -1,    8822 },
            new[]{ 2, -1,  0, -1,    8216 },
            new[]{ 2,  0, -2, -1,    4324 },
            new[]{ 2,  0,  1,  1,    4200 },
            new[]{ 2,  1,  0, -1,   -3359 },
            new[]{ 2, -1, -1,  1,    2463 },
            new[]{ 2, -1,  0,  1,    2211 },
            new[]{ 2, -1, -1, -1,    2065 },
            new[]{ 0,  1, -1, -1,   -1870 },
            new[]{ 4,  0, -1, -1,    1828 },
            new[]{ 0,  1,  0,  1,   -1794 },
        };

        private static void MoonEcliptic(double jd, out double lonDeg, out double latDeg, out double distKm)
        {
            double t = JulianCenturies(jd);
            double t2 = t * t, t3 = t2 * t, t4 = t3 * t;

            double lp = NormalizeDegrees(218.3164477 + 481267.88123421 * t - 0.0015786 * t2 + t3 / 538841.0 - t4 / 65194000.0);
            double d = NormalizeDegrees(297.8501921 + 445267.1114034 * t - 0.0018819 * t2 + t3 / 545868.0 - t4 / 113065000.0);
            double m = NormalizeDegrees(357.5291092 + 35999.0502909 * t - 0.0001536 * t2 + t3 / 24490000.0);
            double mp = NormalizeDegrees(134.9633964 + 477198.8675055 * t + 0.0087414 * t2 + t3 / 69699.0 - t4 / 14712000.0);
            double f = NormalizeDegrees(93.2720950 + 483202.0175233 * t - 0.0036539 * t2 - t3 / 3526000.0 + t4 / 863310000.0);

            double e = 1.0 - 0.002516 * t - 0.0000074 * t2;

            double sumL = 0.0, sumR = 0.0, sumB = 0.0;

            foreach (var term in MoonLR)
            {
                double arg = (term[0] * d + term[1] * m + term[2] * mp + term[3] * f) * Rad;
                double eFactor = EPow(e, term[1]);
                sumL += term[4] * eFactor * Math.Sin(arg);
                sumR += term[5] * eFactor * Math.Cos(arg);
            }

            foreach (var term in MoonB)
            {
                double arg = (term[0] * d + term[1] * m + term[2] * mp + term[3] * f) * Rad;
                double eFactor = EPow(e, term[1]);
                sumB += term[4] * eFactor * Math.Sin(arg);
            }

            // 付加項（Meeus 47章、A1〜A3 による補正）
            double a1 = NormalizeDegrees(119.75 + 131.849 * t);
            double a2 = NormalizeDegrees(53.09 + 479264.290 * t);
            double a3 = NormalizeDegrees(313.45 + 481266.484 * t);

            sumL += 3958 * Math.Sin(a1 * Rad) + 1962 * Math.Sin((lp - f) * Rad) + 318 * Math.Sin(a2 * Rad);

            sumB += -2235 * Math.Sin(lp * Rad) + 382 * Math.Sin(a3 * Rad)
                    + 175 * Math.Sin((a1 - f) * Rad) + 175 * Math.Sin((a1 + f) * Rad)
                    + 127 * Math.Sin((lp - mp) * Rad) - 115 * Math.Sin((lp + mp) * Rad);

            Nutation(t, out double deltaPsi, out _);

            lonDeg = NormalizeDegrees(lp + sumL / 1e6 + deltaPsi);
            latDeg = sumB / 1e6;
            distKm = 385000.56 + sumR / 1e3;
        }

        /// <summary>e の |n| 乗（n=0,±1,±2 用の軽量ヘルパー）。月の摂動項で M の係数に応じて乗じる補正。</summary>
        private static double EPow(double e, int n)
        {
            int an = Math.Abs(n);
            if (an == 0) return 1.0;
            if (an == 1) return e;
            return e * e; // an >= 2 のケースはこのテーブルでは最大2
        }

        // =========================================================================
        // 惑星 (JPL Standish 1992 平均軌道要素 + ケプラー方程式)
        // =========================================================================

        private enum Planet { Mercury, Venus, Mars, Jupiter, Saturn }

        /// <summary>平均軌道要素とその世紀あたりの変化率（J2000.0 元期、有効期間目安 1800〜2050年）</summary>
        private struct OrbitalElements
        {
            public double A0, ADot;         // 軌道長半径 [AU]
            public double E0, EDot;         // 離心率
            public double I0, IDot;         // 軌道傾斜角 [deg]
            public double L0, LDot;         // 平均黄経 [deg]
            public double PeriDeg0, PeriDot; // 近日点黄経 ϖ [deg]
            public double NodeDeg0, NodeDot; // 昇交点黄経 Ω [deg]

            public OrbitalElements(double a0, double aDot, double e0, double eDot,
                                    double i0, double iDot, double l0, double lDot,
                                    double peri0, double periDot, double node0, double nodeDot)
            {
                A0 = a0; ADot = aDot;
                E0 = e0; EDot = eDot;
                I0 = i0; IDot = iDot;
                L0 = l0; LDot = lDot;
                PeriDeg0 = peri0; PeriDot = periDot;
                NodeDeg0 = node0; NodeDot = nodeDot;
            }
        }

        private static readonly OrbitalElements EarthElements = new OrbitalElements(
            1.00000261, 0.00000562,
            0.01671123, -0.00004392,
            -0.00001531, -0.01294668,
            100.46457166, 35999.37244981,
            102.93768193, 0.32327364,
            0.0, 0.0);

        private static readonly System.Collections.Generic.Dictionary<Planet, OrbitalElements> Elements =
            new System.Collections.Generic.Dictionary<Planet, OrbitalElements>
        {
            [Planet.Mercury] = new OrbitalElements(
                0.38709927, 0.00000037,
                0.20563593, 0.00001906,
                7.00497902, -0.00594749,
                252.25032350, 149472.67411175,
                77.45779628, 0.16047689,
                48.33076593, -0.12534081),

            [Planet.Venus] = new OrbitalElements(
                0.72333566, 0.00000390,
                0.00677672, -0.00004107,
                3.39467605, -0.00078890,
                181.97909950, 58517.81538729,
                131.60246718, 0.00268329,
                76.67984255, -0.27769418),

            [Planet.Mars] = new OrbitalElements(
                1.52371034, 0.00001847,
                0.09339410, 0.00007882,
                1.84969142, -0.00813131,
                -4.55343205, 19140.30268499,
                -23.94362959, 0.44441088,
                49.55953891, -0.29257343),

            [Planet.Jupiter] = new OrbitalElements(
                5.20288700, -0.00011607,
                0.04838624, -0.00013253,
                1.30439695, -0.00183714,
                34.39644051, 3034.74612775,
                14.72847983, 0.21252668,
                100.47390909, 0.20469106),

            [Planet.Saturn] = new OrbitalElements(
                9.53667594, -0.00125060,
                0.05386179, -0.00050991,
                2.48599187, 0.00193609,
                49.95424423, 1222.49362201,
                92.59887831, -0.41897216,
                113.66242448, -0.28867794),
        };

        /// <summary>与えられた軌道要素・時刻(ユリウス世紀 t)における太陽中心黄道直交座標 [AU]（J2000分点）</summary>
        private static void HeliocentricPosition(OrbitalElements el, double t, out double x, out double y, out double z)
        {
            double a = el.A0 + el.ADot * t;
            double e = el.E0 + el.EDot * t;
            double i = el.I0 + el.IDot * t;
            double l = NormalizeDegrees(el.L0 + el.LDot * t);
            double peri = NormalizeDegrees(el.PeriDeg0 + el.PeriDot * t);
            double node = NormalizeDegrees(el.NodeDeg0 + el.NodeDot * t);

            double m = NormalizeSignedDegrees(l - peri);
            double omega = peri - node;

            double mRad = m * Rad;
            double eRad = mRad + e * Math.Sin(mRad); // 初期値

            for (int iter = 0; iter < 30; iter++)
            {
                double dE = (mRad - (eRad - e * Math.Sin(eRad))) / (1.0 - e * Math.Cos(eRad));
                eRad += dE;
                if (Math.Abs(dE) < 1e-10) break;
            }

            double xOrb = a * (Math.Cos(eRad) - e);
            double yOrb = a * Math.Sqrt(1.0 - e * e) * Math.Sin(eRad);

            double nodeRad = node * Rad;
            double iRad = i * Rad;
            double omegaRad = omega * Rad;

            double cosO = Math.Cos(nodeRad), sinO = Math.Sin(nodeRad);
            double cosI = Math.Cos(iRad), sinI = Math.Sin(iRad);
            double cosW = Math.Cos(omegaRad), sinW = Math.Sin(omegaRad);

            x = (cosO * cosW - sinO * sinW * cosI) * xOrb + (-cosO * sinW - sinO * cosW * cosI) * yOrb;
            y = (sinO * cosW + cosO * sinW * cosI) * xOrb + (-sinO * sinW + cosO * cosW * cosI) * yOrb;
            z = (sinW * sinI) * xOrb + (cosW * sinI) * yOrb;
        }

        private static void PlanetGeoEquatorial(double jd, Planet planet, out double ra, out double dec)
        {
            double t = JulianCenturies(jd);

            HeliocentricPosition(EarthElements, t, out double xe, out double ye, out double ze);

            OrbitalElements el = Elements[planet];

            // 光行時間補正（1回反復で十分な精度）
            HeliocentricPosition(el, t, out double xp, out double yp, out double zp);
            double xg = xp - xe, yg = yp - ye, zg = zp - ze;
            double delta = Math.Sqrt(xg * xg + yg * yg + zg * zg);
            double tau = 0.0057755183 * delta; // 日

            double t2 = JulianCenturies(jd - tau);
            HeliocentricPosition(el, t2, out xp, out yp, out zp);
            xg = xp - xe; yg = yp - ye; zg = zp - ze;

            double lambda = NormalizeDegrees(Math.Atan2(yg, xg) * Deg);
            double beta = Math.Atan2(zg, Math.Sqrt(xg * xg + yg * yg)) * Deg;

            Nutation(t, out double deltaPsi, out _);
            double lambdaApparent = NormalizeDegrees(lambda + deltaPsi);

            EclipticToEquatorial(lambdaApparent, beta, jd, out ra, out dec);
        }
    }
}
