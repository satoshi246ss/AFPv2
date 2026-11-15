// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace MeteorDetection
{
    /// <summary>雲（月を含む）マスク生成のパラメータ。</summary>
    public sealed class CloudMaskConfig
    {
        // ---- 前処理 ----------------------------------------------------------

        /// <summary>恒星除去用オープニング(収縮→膨張)のカーネルサイズ [px]。
        /// 恒星PSFより大きく、想定する最小の雲パッチより小さく設定する。0/1で無効。</summary>
        public int OpeningSizePx = 15;

        /// <summary>オープニング後に掛けるガウシアンぼかしのカーネルサイズ [px]（量子化・ノイズ抑制）。0/1で無効。</summary>
        public int BlurSizePx = 5;

        // ---- 晴天レベル(ベースライン)推定 -----------------------------------

        /// <summary>晴天レベル推定用のブロックサイズ [px]（背景画像の解像度基準）。
        /// 小さいほど空の明るさの勾配(地平線付近の明るさ等)に追従するが、
        /// ブロック全体が雲に覆われると晴天レベルを誤る。</summary>
        public int BlockSizePx = 64;

        /// <summary>各ブロック内で「晴天レベル」とみなす輝度のパーセンタイル(0〜1)。
        /// 雲は晴天より明るいので、低めのパーセンタイルほど晴天側を拾える。
        /// 0.30 なら、ブロックの7割が雲でなければ晴天レベルを捉えられる。</summary>
        public double ClearLevelPercentile = 0.30;

        /// <summary>全ブロックの晴天レベルから「全体の晴天レベル」を求める際のパーセンタイル(0〜1)。</summary>
        public double GlobalClearBlockPercentile = 0.25;

        /// <summary>各ブロックの晴天レベルが「全体の晴天レベル」を超えて良い上限 [輝度]。
        /// ブロック全体が雲に覆われて晴天レベルが雲の明るさに引き上げられるのを防ぐ。
        /// 地平線付近の光害による空の明るさの上昇より大きな値にすること。</summary>
        public double MaxBaselineRise = 30.0;//30

        /// <summary>晴天レベルのブロックマップに掛ける最小値フィルタの半径 [ブロック数]。
        /// 周囲のより暗い(＝晴れた)ブロックに合わせて、雲に覆われたブロックの値を下げる。0で無効。</summary>
        public int BaselineMinFilterBlocks = 1;

        /// <summary>ブロック内の有効画素(魚眼円内)がこの割合未満のブロックは統計に使わない(0〜1)。</summary>
        public double MinValidBlockFraction = 0.3;

        // ---- 二値化しきい値 ---------------------------------------------------

        /// <summary>しきい値 = 晴天レベル + max(MinOffset, OffsetSigma × 晴天ノイズσ)。
        /// 晴天ノイズσの何倍だけ晴天レベルの上に置くか。小さいほど薄い雲まで拾い、誤マスクも増える。</summary>
        public double OffsetSigma = 6.0;//3

        /// <summary>晴天レベルからの最小オフセット [輝度]。8bit量子化やσの過小推定に対する下限。</summary>
        public double MinOffset = 30.0;//3

        // ---- 後処理 -----------------------------------------------------------

        /// <summary>断片化した雲領域を連結するクロージング(膨張→収縮)のカーネルサイズ [px]。0/1で無効。</summary>
        public int ClosingSizePx = 25;

        /// <summary>雲の輪郭の安全マージン [px]。雲の縁は2値化境界より外側に薄く広がっているため、外側に広げて除外する。</summary>
        public int MarginPx = 15;

        /// <summary>これ未満の面積 [px^2] の孤立領域は雲とみなさない。</summary>
        public int MinCloudAreaPx = 50;

        // ---- 魚眼の有効円(任意) ----------------------------------------------

        /// <summary>trueなら魚眼の像円の外側を統計から除外し、マスクでも無効(0)にする。
        /// 円の外側の暗い画素が晴天レベル推定を狂わせるのを防ぐ。</summary>
        public bool UseFovCircle = true;

        /// <summary>像円の中心X(画像幅に対する比率)</summary>
        public double FovCenterXRatio = 0.5;

        /// <summary>像円の中心Y(画像高さに対する比率)</summary>
        public double FovCenterYRatio = 0.5;

        /// <summary>像円の半径(画像幅に対する比率)。像の縁のケラレを避けるためやや小さめに設定する。</summary>
        public double FovRadiusRatio = 0.45;//defult: 0.45; // 画像幅の45% = 画角90°の魚眼でケラレを避けるためやや小さめ
    }

    /// <summary>雲マスク生成時の診断情報（ログ・パラメータ調整用）。</summary>
    public sealed class CloudMaskInfo
    {
        /// <summary>雲・月と判定された領域の割合(0〜1)。魚眼円を指定した場合は円内に対する割合。</summary>
        public double CoverageRatio;
        /// <summary>全体の晴天レベル（輝度）</summary>
        public double GlobalClearLevel;
        /// <summary>推定した晴天ノイズσ（輝度）</summary>
        public double NoiseSigma;
        /// <summary>晴天レベルに加えたオフセット（輝度）</summary>
        public double Offset;
        /// <summary>晴天レベル推定に使えたブロック数</summary>
        public int UsedBlocks;
        /// <summary>全ブロック数</summary>
        public int TotalBlocks;
    }

    /// <summary>
    /// 背景画像（AccumulateWeightedで生成した輝度平均）から雲・月を検出し、
    /// 流星検出で使う「有効領域マスク」(255=検出対象, 0=雲/月または魚眼円の外)を生成する。
    ///
    /// 考え方：
    ///  1. オープニングで恒星(点光源)を除去し、面的な明るさだけを残す
    ///  2. 画像をブロックに分け、各ブロックの低パーセンタイルから「晴天レベル」を求め、
    ///     空の明るさの勾配(地平線付近の光害など)に追従する滑らかなベースラインマップを作る
    ///  3. しきい値 = ベースライン + 小さなオフセット（晴天の少し上）で二値化
    ///  4. クロージング、小領域除去、安全マージンで整形
    /// </summary>
    public static class CloudMaskGenerator
    {
        public static Mat GenerateValidMask(Mat background, CloudMaskConfig cfg = null,
            Action<double> cloudCoverageRatio = null, Action<CloudMaskInfo> info = null)
        {
            if (background == null) throw new ArgumentNullException(nameof(background));
            cfg = cfg ?? new CloudMaskConfig();

            var temps = new List<IDisposable>();
            Mat T(Mat m) { temps.Add(m); return m; }

            try
            {
                Mat bg8 = background;
                if (background.Type() != MatType.CV_8U)
                {
                    bg8 = T(new Mat());
                    background.ConvertTo(bg8, MatType.CV_8U);
                }

                int W = bg8.Width, H = bg8.Height;
                int total = W * H;

                // ---- 魚眼有効円 ------------------------------------------------
                Mat fov = null;
                byte[] fovBytes = null;
                if (cfg.UseFovCircle)
                {
                    fov = T(new Mat(bg8.Size(), MatType.CV_8U, Scalar.All(0)));
                    Cv2.Circle(fov,
                        new Point((int)Math.Round(W * cfg.FovCenterXRatio), (int)Math.Round(H * cfg.FovCenterYRatio)),
                        (int)Math.Round(W * cfg.FovRadiusRatio), Scalar.All(255), -1, LineTypes.Link8);
                    fovBytes = new byte[total];
                    Marshal.Copy(fov.Data, fovBytes, 0, total);
                }

                // ---- 1. 前処理: 恒星除去 + 平滑化 -------------------------------
                Mat work = bg8;
                if (cfg.OpeningSizePx > 1)
                {
                    var k = T(Cv2.GetStructuringElement(MorphShapes.Ellipse,
                        new Size(cfg.OpeningSizePx, cfg.OpeningSizePx)));
                    var o = T(new Mat());
                    Cv2.MorphologyEx(work, o, MorphTypes.Open, k);
                    work = o;
                }
                if (cfg.BlurSizePx > 1)
                {
                    int ks = cfg.BlurSizePx | 1; // 奇数化
                    var b = T(new Mat());
                    Cv2.GaussianBlur(work, b, new Size(ks, ks), 0);
                    work = b;
                }
                if (!work.IsContinuous()) work = T(work.Clone());

                byte[] data = new byte[total];
                Marshal.Copy(work.Data, data, 0, total);

                // ---- 2. ブロックごとの晴天レベル・ノイズ推定 ---------------------
                int B = Math.Max(8, cfg.BlockSizePx);
                int bw = (W + B - 1) / B, bh = (H + B - 1) / B;
                var level = new double[bw * bh];
                var valid = new bool[bw * bh];
                var sigmaList = new List<double>();
                var levelList = new List<double>();
                var hist = new int[256];

                for (int by = 0; by < bh; by++)
                {
                    int y0 = by * B, y1 = Math.Min(H, y0 + B);
                    for (int bx = 0; bx < bw; bx++)
                    {
                        int x0 = bx * B, x1 = Math.Min(W, x0 + B);
                        Array.Clear(hist, 0, 256);
                        int count = 0;
                        for (int y = y0; y < y1; y++)
                        {
                            int row = y * W;
                            for (int x = x0; x < x1; x++)
                            {
                                int idx = row + x;
                                if (fovBytes != null && fovBytes[idx] == 0) continue;
                                hist[data[idx]]++;
                                count++;
                            }
                        }

                        int bi = by * bw + bx;
                        double blockArea = (double)(x1 - x0) * (y1 - y0);
                        if (count < 16 || count < cfg.MinValidBlockFraction * blockArea) continue;

                        double pClear = HistPercentile(hist, count, cfg.ClearLevelPercentile);
                        double p50 = HistPercentile(hist, count, 0.50);
                        double p16 = HistPercentile(hist, count, 0.1587); // 正規分布の-1σ

                        level[bi] = pClear;
                        valid[bi] = true;
                        levelList.Add(pClear);
                        sigmaList.Add(Math.Max(0.0, p50 - p16));
                    }
                }

                // 統計が取れない(魚眼円が極端に小さい等)場合は、全域有効で返す
                if (levelList.Count == 0)
                {
                    var allValid = new Mat(bg8.Size(), MatType.CV_8U, Scalar.All(255));
                    if (fov != null) Cv2.BitwiseAnd(allValid, fov, allValid);
                    cloudCoverageRatio?.Invoke(0.0);
                    return allValid;
                }

                double globalClear = ListPercentile(levelList, cfg.GlobalClearBlockPercentile);
                // 晴天ノイズσ: 晴れたブロックほどσが小さいため、下位25%点(外れ値に頑健)を採用
                double sigma = ListPercentile(sigmaList, 0.25);
                double offset = Math.Max(cfg.MinOffset, cfg.OffsetSigma * sigma);

                // ---- 3. ベースラインマップ (ブロック→全画素へ補間) ----------------
                var thr = new float[bw * bh];
                double cap = globalClear + cfg.MaxBaselineRise;
                for (int i = 0; i < thr.Length; i++)
                {
                    double bl = valid[i] ? Math.Min(level[i], cap) : globalClear;
                    thr[i] = (float)(bl + offset); // しきい値 = 晴天レベル + オフセット
                }

                Mat small = T(new Mat(bh, bw, MatType.CV_32F));
                Marshal.Copy(thr, 0, small.Data, thr.Length);

                if (cfg.BaselineMinFilterBlocks > 0)
                {
                    int ke = cfg.BaselineMinFilterBlocks * 2 + 1;
                    var kern = T(Cv2.GetStructuringElement(MorphShapes.Rect, new Size(ke, ke)));
                    Cv2.Erode(small, small, kern); // 周囲の最も暗い(晴れた)ブロックに合わせる
                }
                if (bw >= 3 && bh >= 3)
                    Cv2.GaussianBlur(small, small, new Size(3, 3), 0);

                Mat thrFull = T(new Mat());
                Cv2.Resize(small, thrFull, new Size(W, H), 0, 0, InterpolationFlags.Linear);

                // ---- 4. 二値化 (画素値 > しきい値 を雲候補とする) -----------------
                Mat img32 = T(new Mat());
                work.ConvertTo(img32, MatType.CV_32F);

                Mat cloudBin = T(new Mat());
                Cv2.Compare(img32, thrFull, cloudBin, CmpType.GT);

                if (fov != null) Cv2.BitwiseAnd(cloudBin, fov, cloudBin);

                // 断片化した雲を連結
                if (cfg.ClosingSizePx > 1)
                {
                    var ck = T(Cv2.GetStructuringElement(MorphShapes.Ellipse,
                        new Size(cfg.ClosingSizePx, cfg.ClosingSizePx)));
                    Cv2.MorphologyEx(cloudBin, cloudBin, MorphTypes.Close, ck);
                }

                // ---- 5. 小領域除去（雲内部の晴れ間は保持される） -------------------
                Mat labels = T(new Mat());
                Mat stats = T(new Mat());
                Mat cents = T(new Mat());
                int n = Cv2.ConnectedComponentsWithStats(cloudBin, labels, stats, cents);

                var keep = new bool[n];
                for (int i = 1; i < n; i++)
                    keep[i] = stats.At<int>(i, (int)ConnectedComponentsTypes.Area) >= cfg.MinCloudAreaPx;

                var lab = new int[total];
                Marshal.Copy(labels.Data, lab, 0, total);
                var cloudBytes = new byte[total];
                for (int i = 0; i < total; i++)
                    if (keep[lab[i]]) cloudBytes[i] = 255;

                Mat cloudMask = T(new Mat(bg8.Size(), MatType.CV_8U));
                Marshal.Copy(cloudBytes, 0, cloudMask.Data, total);

                // ---- 雲占有率 (安全マージン適用前) --------------------------------
                double coverage;
                if (fov != null)
                {
                    int fovPx = Cv2.CountNonZero(fov);
                    coverage = fovPx > 0 ? (double)Cv2.CountNonZero(cloudMask) / fovPx : 0.0;
                }
                else
                {
                    coverage = (double)Cv2.CountNonZero(cloudMask) / total;
                }
                cloudCoverageRatio?.Invoke(coverage);

                // ---- 6. 安全マージン: 雲領域を外側へ広げる -------------------------
                if (cfg.MarginPx > 1)
                {
                    var mk = T(Cv2.GetStructuringElement(MorphShapes.Ellipse,
                        new Size(cfg.MarginPx, cfg.MarginPx)));
                    Cv2.Dilate(cloudMask, cloudMask, mk);
                }

                var validMask = new Mat();
                Cv2.BitwiseNot(cloudMask, validMask);
                if (fov != null) Cv2.BitwiseAnd(validMask, fov, validMask);

                if (info != null)
                {
                    info(new CloudMaskInfo
                    {
                        CoverageRatio = coverage,
                        GlobalClearLevel = globalClear,
                        NoiseSigma = sigma,
                        Offset = offset,
                        UsedBlocks = levelList.Count,
                        TotalBlocks = bw * bh
                    });
                }

                return validMask;
            }
            finally
            {
                foreach (var d in temps) d.Dispose();
            }
        }

        /// <summary>256階調ヒストグラムからパーセンタイルを求める（各階調を幅1の区間として補間）。</summary>
        private static double HistPercentile(int[] hist, int count, double p)
        {
            double target = p * count;
            double cum = 0;
            for (int v = 0; v < 256; v++)
            {
                double next = cum + hist[v];
                if (hist[v] > 0 && next >= target)
                {
                    double frac = (target - cum) / hist[v];
                    return v - 0.5 + frac;
                }
                cum = next;
            }
            return 255.0;
        }

        private static double ListPercentile(List<double> values, double p)
        {
            var a = values.ToArray();
            Array.Sort(a);
            if (a.Length == 1) return a[0];
            double pos = Math.Max(0.0, Math.Min(1.0, p)) * (a.Length - 1);
            int i0 = (int)Math.Floor(pos);
            int i1 = Math.Min(a.Length - 1, i0 + 1);
            double f = pos - i0;
            return a[i0] * (1 - f) + a[i1] * f;
        }
    }
}
