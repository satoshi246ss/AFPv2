// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace MeteorDetection
{
    /// <summary>雲（月を含む）マスク生成のパラメータ。</summary>
    public sealed class CloudMaskConfig
    {
        /// <summary>恒星除去用オープニング(収縮→膨張)のカーネルサイズ [px]。
        /// 恒星の見かけの大きさ(PSF)より大きく、想定する最小の雲パッチより小さく設定する。
        /// 0にするとオープニングを行わない（恒星も含めた全ての明部を雲候補扱いにする）。</summary>
        public int OpeningSizePx = 15;

        /// <summary>雲候補抽出の動的しきい値 = (オープニング後画像の)平均 + ThresholdK * 標準偏差</summary>
        public double ThresholdK = 3.0;

        /// <summary>動的しきい値の下限（8bit階調換算の輝度）</summary>
        public double MinThreshold = 10.0;

        /// <summary>断片化した雲領域を連結するクロージング(膨張→収縮)のカーネルサイズ [px]。
        /// 0にするとクロージングを行わない。</summary>
        public int ClosingSizePx = 25;

        /// <summary>最終マスクに持たせる安全マージン [px]。雲の輪郭や月のハロー(滲み)は
        /// 2値化の境界より実際には広がっていることが多いため、外側に余裕を持たせて除外する。</summary>
        public int MarginPx = 15;

        /// <summary>これ未満の面積 [px^2] の孤立領域は雲とみなさず除外しない（誤判定抑制）。
        /// オープニング・クロージング後に残る小さなノイズ的領域を想定し、恒星除外に使う
        /// 面積よりも十分大きい値を設定する。</summary>
        public int MinCloudAreaPx = 50;
    }

    /// <summary>
    /// 背景画像（AccumulateWeightedで生成した輝度平均）から雲・月を検出し、
    /// 流星検出で使う「有効領域マスク」(255=検出対象, 0=雲/月につき除外)を生成する。
    ///
    /// 考え方：恒星は点光源（小さく局在）、雲・月は面的に広がった明部という違いを利用する。
    /// まずモルフォロジー・オープニングで恒星などの点状の明部を取り除き、
    /// 残った「面的な明るさ」だけを雲・月候補として動的しきい値で抽出する。
    /// 月はオープニングのカーネルより十分大きいため除去されず、そのまま雲候補として
    /// （非常に高いコントラストで）検出される。
    /// </summary>
    public static class CloudMaskGenerator
    {
        /// <summary>
        /// 有効領域マスクを生成する。
        /// </summary>
        /// <param name="background">AccumulateWeighted()で更新された背景画像。CV_8UまたはCV_32F。</param>
        /// <param name="cfg">パラメータ。省略時は既定値を使用。</param>
        /// <param name="cloudCoverageRatio">検出された雲・月領域が画面全体に占める割合(0〜1)を
        /// 受け取る出力。不要ならnull可（曇天判定・ログ記録等に利用可能）。</param>
        public static Mat GenerateValidMask(Mat background, CloudMaskConfig cfg = null,
            Action<double> cloudCoverageRatio = null)
        {
            if (background == null) throw new ArgumentNullException(nameof(background));
            cfg = cfg ?? new CloudMaskConfig();

            Mat bg8 = background;
            bool disposeBg8 = false;
            if (background.Type() != MatType.CV_8U)
            {
                bg8 = new Mat();
                background.ConvertTo(bg8, MatType.CV_8U);
                disposeBg8 = true;
            }

            try
            {
                // 1. 恒星(点光源)を除去し、面的な明るさ成分だけを残す
                Mat opened;
                bool disposeOpened;
                if (cfg.OpeningSizePx > 0)
                {
                    var openKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse,
                        new Size(cfg.OpeningSizePx, cfg.OpeningSizePx));
                    opened = new Mat();
                    Cv2.MorphologyEx(bg8, opened, MorphTypes.Open, openKernel);
                    disposeOpened = true;
                }
                else
                {
                    opened = bg8;
                    disposeOpened = false;
                }

                Mat validMask;
                try
                {
                    // 2. 動的しきい値で雲・月候補を抽出
                    Cv2.MeanStdDev(opened, out Scalar mean, out Scalar std);
                    double thresh = Math.Max(cfg.MinThreshold, mean.Val0 + cfg.ThresholdK * std.Val0);

                    using (var cloudBin = new Mat())
                    {
                        Cv2.Threshold(opened, cloudBin, thresh, 255, ThresholdTypes.Binary);

                        // 3. 断片化した雲領域をつなげる
                        if (cfg.ClosingSizePx > 0)
                        {
                            var closeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse,
                                new Size(cfg.ClosingSizePx, cfg.ClosingSizePx));
                            Cv2.MorphologyEx(cloudBin, cloudBin, MorphTypes.Close, closeKernel);
                        }

                        // 4. 小面積の孤立領域を除去しつつ、有効領域マスクへ書き出す
                        Cv2.FindContours(cloudBin, out Point[][] contours, out HierarchyIndex[] _,
                            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                        validMask = new Mat(bg8.Size(), MatType.CV_8U, Scalar.All(255));
                        long cloudPixels = 0;
                        for (int i = 0; i < contours.Length; i++)
                        {
                            double area = Cv2.ContourArea(contours[i]);
                            if (area < cfg.MinCloudAreaPx) continue;
                            cloudPixels += (long)area;
                            Cv2.DrawContours(validMask, contours, i, Scalar.All(0), -1);
                        }

                        cloudCoverageRatio?.Invoke((double)cloudPixels / (bg8.Width * (double)bg8.Height));
                    }

                    // 5. 安全マージン：有効領域側を収縮させ、雲/月の除外範囲を外側に広げる
                    if (cfg.MarginPx > 0)
                    {
                        var marginKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse,
                            new Size(cfg.MarginPx, cfg.MarginPx));
                        Cv2.Erode(validMask, validMask, marginKernel);
                    }
                }
                finally
                {
                    if (disposeOpened) opened.Dispose();
                }

                return validMask;
            }
            finally
            {
                if (disposeBg8) bg8.Dispose();
            }
        }
    }
}
