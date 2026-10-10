// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
using System;
using OpenCvSharp;

namespace MeteorDetection
{
    /// <summary>星マスク生成のパラメータ。</summary>
    public sealed class StarMaskConfig
    {
        /// <summary>恒星検出の動的しきい値 = 背景の平均 + ThresholdK * 標準偏差</summary>
        public double ThresholdK = 5.0;

        /// <summary>動的しきい値の下限（8bit階調換算の輝度）</summary>
        public double MinThreshold = 15.0;

        /// <summary>恒星とみなす最小面積 [px^2]（背景画像の解像度基準）。ノイズ除去用</summary>
        public int MinStarAreaPx = 1;

        /// <summary>恒星とみなす最大面積 [px^2]。これを超える明部は雲・光害などとして除外対象にしない</summary>
        public int MaxStarAreaPx = 60;

        /// <summary>各恒星の中心から除外する半径 [px]（瞬きによる輝度変化・測光中心の揺れ・
        /// 背景の実効時定数内でのわずかな日周運動ズレを吸収するための余裕）</summary>
        public int ExclusionRadiusPx = 4;

        /// <summary>1回のマスク生成で処理する恒星数の上限（処理時間の安全弁。天の川方向などで極端に
        /// 星数が多い場合に備える）</summary>
        public int MaxStars = 3000;
    }

    /// <summary>
    /// 背景画像（AccumulateWeightedで生成した輝度平均）から恒星・惑星位置を検出し、
    /// 流星検出で使う「有効領域マスク」(255=検出対象, 0=恒星近傍につき除外)を生成する。
    /// 背景画像の統計から動的しきい値で明部を抽出する点、連結成分抽出を使う点は
    /// MeteorFrameDetectorの流星検出ロジックと同じ考え方で、実装・計算コストも同程度に軽量。
    /// </summary>
    public static class StarMaskGenerator
    {
        /// <summary>
        /// 有効領域マスクを生成する。
        /// </summary>
        /// <param name="background">AccumulateWeighted()で更新された背景画像。CV_8UまたはCV_32F。</param>
        /// <param name="cfg">パラメータ。省略時は既定値を使用。</param>
        /// <param name="starCount">検出した恒星数（除外半径適用前のブロブ数）を受け取る出力。不要ならnull可。</param>
        public static Mat GenerateValidMask(Mat background, StarMaskConfig cfg = null, Action<int> starCount = null)
        {
            if (background == null) throw new ArgumentNullException(nameof(background));
            cfg = cfg ?? new StarMaskConfig();

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
                Cv2.MeanStdDev(bg8, out Scalar mean, out Scalar std);
                double thresh = Math.Max(cfg.MinThreshold, mean.Val0 + cfg.ThresholdK * std.Val0);

                using (var starBin = new Mat())
                {
                    Cv2.Threshold(bg8, starBin, thresh, 255, ThresholdTypes.Binary);

                    using (var labels = new Mat())
                    using (var stats = new Mat())
                    using (var centroids = new Mat())
                    {
                        int n = Cv2.ConnectedComponentsWithStats(starBin, labels, stats, centroids);

                        var validMask = new Mat(bg8.Size(), MatType.CV_8U, Scalar.All(255));

                        int count = 0;
                        for (int i = 1; i < n && count < cfg.MaxStars; i++) // ラベル0=背景
                        {
                            int area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                            if (area < cfg.MinStarAreaPx || area > cfg.MaxStarAreaPx) continue;

                            double cx = centroids.At<double>(i, 0);
                            double cy = centroids.At<double>(i, 1);

                            Cv2.Circle(validMask,
                                new Point((int)Math.Round(cx), (int)Math.Round(cy)),
                                cfg.ExclusionRadiusPx, Scalar.All(0), -1, LineTypes.Link8);

                            count++;
                        }

                        starCount?.Invoke(count);
                        return validMask;
                    }
                }
            }
            finally
            {
                if (disposeBg8) bg8.Dispose();
            }
        }
    }
}
