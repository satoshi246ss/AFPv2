using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Blob;
using MeteorDetection;

namespace AFPv2
{
    partial class Form1
    {
        MeteorDetection.MeteorFrameDetector framedetector;
        TrackManager tracker;
        DetectionRecorder recorder;
        MeteorDetectionPipeline pipeline;
        private long totalDetections = 0;

        // 動画を開いてフレームごとにパイプラインへ送り、1/2サイズで表示し検出矩形/中心を描画する
        private void ProcessFrameWithPipelineInit(string path= @"D:\img_data\data")
        {
            framedetector = new MeteorFrameDetector
            {
                BinFactor = 2,      // 2608x2608 -> 1304x1304 で検出処理（CPU負荷を約1/4に）
                ThresholdK = 20.0,
                MinAreaPx = 2,
                MaxAreaPx = 600,
                DilateSize = 3
            };

            var trackerCfg = new TrackManagerConfig
            {
                GateRadiusPx = 30,
                //MinHitsToConfirm = 4,
                MaxMissesTentative = 2,
                MaxMissesConfirmed = 6,
                MinSpeedPxPerSec = 30,
                MaxSpeedPxPerSec = 1000
            };

            var reference = CloudMaskReference.FromFile(@"D:\img_data\data\ref\clear_sky_mean.png"); // 晴天の積算平均フレーム

            var cloudeCfg = new CloudMaskConfig
            {
                OpeningSizePx = 0,
                ClosingSizePx = 25,
                //ThresholdK = 2.0,
                //MinThreshold = 30,
                MarginPx = 15,
                MinCloudAreaPx = 50,
                //Reference = reference,   // これを設定すると有効。nullなら従来どおり
                UseFovCircle = true
            };           

            // 雲・月マスクを有効化する。
            // null の場合は MeteorFrameDetector 側で雲マスク処理が無効になるため、
            // 動画検出時は既定値の CloudMaskConfig を明示的に設定する。
            if (framedetector.CloudMaskConfig == null)
            {
                framedetector.CloudMaskConfig = new MeteorDetection.CloudMaskConfig();
            }
            framedetector.CloudMaskUpdateIntervalFrames = Math.Max(1, framedetector.CloudMaskUpdateIntervalFrames);

            tracker = new TrackManager(trackerCfg);

            recorder = null;
            try
            {
                string tmpDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "detecttmp");
                System.IO.Directory.CreateDirectory(tmpDir);
                recorder = new DetectionRecorder(tmpDir);
                recorder.StartSession(path);
            }
            catch { recorder = null; }

            pipeline = new MeteorDetectionPipeline(framedetector, tracker, recorder, null, this.fish2);

            MakeSmallMask(img_mask, img_mask8u_s, framedetector);
            //Cv2.ImShow("test mask", img_mask); Cv2.WaitKey();
            //Cv2.ImShow("test small mask", img_mask8u_s); Cv2.WaitKey();
        }
        // <summary>
        // フル解像度のマスクを縮小して検出に適用（detector.BinFactor に合わせる）
        // in:img_mask
        // </summary>
        public void MakeSmallMask(Mat maskFull, Mat maskSmall ,MeteorDetection.MeteorFrameDetector detector)
        {

            // マスクを縮小して検出に適用（detector.BinFactor に合わせる）
            //Mat maskSmall = null;
            int bf = Math.Max(1, detector.BinFactor);
            var smallSize = new OpenCvSharp.Size(Math.Max(1, maskFull.Width / bf), Math.Max(1, maskFull.Height / bf));
            if (maskFull != null)
            {
                //maskSmall = new Mat();
                Cv2.Resize(maskFull, maskSmall, smallSize, 0, 0, InterpolationFlags.Nearest);
                if (maskSmall.Type() != MatType.CV_8U)
                {
                    var tmp = new Mat();
                    Cv2.CvtColor(maskSmall, tmp, ColorConversionCodes.BGR2GRAY);
                    maskSmall.Dispose();
                    maskSmall = tmp;
                }
            }

        }

        // / <summary>
        // フレームをパイプラインで処理する
        // </summary>
        // <param name="frame">入力フレーム CV_8U</param>
        // <param name="pipeline">メテオ検出パイプライン</param>
        // <param name="detector">メテオフレーム検出器</param>
        public void ProcessFrameWithPipeline(Mat frame, Mat maskSmall)//, MeteorDetectionPipeline pipeline, MeteorDetection.MeteorFrameDetector detector)
        {
            if (pipeline == null || framedetector == null)
            {
                Console.WriteLine("Pipeline or detector is not initialized.");
                //MessageBox.Show("Pipeline or detector is not initialized.");
                return;
            }

            // パイプライン処理（内部で Detector->Tracker->Recorder/UDP を行う）
            DateTime frameTimestamp = DateTime.UtcNow;
            var detections = pipeline.ProcessFrameR(frame, maskSmall, frameTimestamp);
            int currentDetections = detections?.Count ?? 0;
            totalDetections += currentDetections;
            fifo.set_detections(detections, frame_id);
            if (currentDetections > 0)
            {
                Console.WriteLine("detections:{0:D3} total:{1:D6}", currentDetections, totalDetections);
            }

            var tracks = pipeline.Tracker.ActiveTracks;
            fifo.set_tracks(tracks, frame_id);

            if (frame_id % 30 != 0)
            {
                return;
            }
            //return;

            // 同じ検出を取得して表示用に使う（NG オフライン処理なので重複しても可）
            //var detections = detector.Process(frame, maskSmall);

            // 表示用に1/2サイズへ縮小
            int w2 = Math.Max(1, frame.Width / 2);
            int h2 = Math.Max(1, frame.Height / 2);
            using (var disp = new Mat(h2,w2,MatType.CV_8UC3))
            using( var framehalf = new Mat())
            {
                Cv2.Resize(frame, framehalf, new OpenCvSharp.Size(w2, h2), 0, 0, InterpolationFlags.Area);
                Cv2.CvtColor(framehalf, disp, ColorConversionCodes.GRAY2BGR);

                // 統合マスクの除外領域（固定・星・雲）を半透明の薄赤色で重ねる。
                if (showMaskOverlay)
                {
                    using (var validMask = framedetector.GetCombinedValidMask())
                    using (var maskFull = new Mat())
                    using (var maskDisplay = new Mat())
                    using (var redOverlay = new Mat(disp.Size(), disp.Type(), new Scalar(0, 0, 255)))
                    {
                        if (validMask != null)
                        {
                            Cv2.BitwiseNot(validMask, maskFull);
                            Cv2.Resize(maskFull, maskDisplay, disp.Size(), 0, 0, InterpolationFlags.Nearest);
                            using (var tinted = new Mat())
                            {
                                Cv2.BitwiseAnd(redOverlay, redOverlay, tinted, maskDisplay);
                                Cv2.AddWeighted(disp, 0.72, tinted, 0.28, 0, disp);
                            }
                        }
                    }
                }

                // 検出結果をオーバーレイ描画（座標は1/2にスケール）
                foreach (var d in detections)
                {
                    var cx = (int)(d.Center.X * 0.5);
                    var cy = (int)(d.Center.Y * 0.5);
                    var rx = (int)(d.BBox.X * 0.5);
                    var ry = (int)(d.BBox.Y * 0.5);
                    var rw = Math.Max(1, (int)(d.BBox.Width * 0.5));
                    var rh = Math.Max(1, (int)(d.BBox.Height * 0.5));

                    //Cv2.Rectangle(disp, new OpenCvSharp.Rect(rx, ry, rw, rh), new Scalar(0, 255, 0), 2);
                    // 中心点は塗りつぶしなしの赤い円（輪郭のみ）
                    Cv2.Circle(disp, new OpenCvSharp.Point(cx, cy), 5, new Scalar(0, 0, 255), 1);
                    Cv2.PutText(disp, d.PeakValue.ToString("F0"), new OpenCvSharp.Point(cx + 6, cy - 6), HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 1);
                }

                // トラッカー結果を描画（履歴ポリライン、推定位置円、ID、速度矢印）
                try
                {
                    //var tracks = pipeline.Tracker.ActiveTracks;
                    if (tracks != null)
                    {
                        foreach (var tr in tracks)
                        {
                            // 推定位置と速度を取得
                            var pos = tr.Position;
                            var vel = tr.VelocityPxPerSec;
                            var vel_xy = Math.Sqrt(vel.X * vel.X + vel.Y*vel.Y);

                            // スケール（表示は1/2サイズ）
                            int tx = (int)(pos.X * 0.5);
                            int ty = (int)(pos.Y * 0.5);

                            // 履歴を線で描画
                            if (tr.History != null && tr.History.Count >= 2)
                            {
                                var pts = new System.Collections.Generic.List<OpenCvSharp.Point>();
                                foreach (var h in tr.History)
                                {
                                    // History の要素は (DateTime T, OpenCvSharp.Point2f Pos) の値タプルなので null チェック不要
                                    if (((h.Pos.X - pos.X) * (h.Pos.X - pos.X) + (h.Pos.Y - pos.Y) * (h.Pos.Y - pos.Y)) > 16) // 近傍の履歴点は描画しない（重なって見えないため）
                                    {
                                        pts.Add(new OpenCvSharp.Point((int)(h.Pos.X * 0.5), (int)(h.Pos.Y * 0.5)));
                                    }
                                }
                                if (pts.Count >= 2) Cv2.Polylines(disp, new[] { pts.ToArray() }, false, new Scalar(255, 200, 0), 2);
                            }

                            // 推定位置の円（確定済みは緑、暫定は青）
                            var col = (tr.Confirmed) ? new Scalar(0, 255, 0) : new Scalar(255, 0, 0);
                            Cv2.Circle(disp, new OpenCvSharp.Point(tx, ty), 7, col, 1);

                            // ID を表示
                            Cv2.PutText(disp, $"ID:{tr.Id}(v:{vel_xy:F1})", new OpenCvSharp.Point(tx + 6, ty + 6), HersheyFonts.HersheySimplex, 0.5, col, 1);

                            // 速度ベクトルを描画（短い矢印）
                            double vx = vel.X * 0.5 * 0.1; // 画面スケールと表示縮尺の調整
                            double vy = vel.Y * 0.5 * 0.1;
                            int offset = 10; // 矢印の始点を円の中心から少しずらす
                            var endp = new OpenCvSharp.Point((int)(tx + vx), (int)(ty + vy)+offset);
                            Cv2.ArrowedLine(disp, new OpenCvSharp.Point(tx, ty+offset), endp, col, 1);
                        }
                    }
                }
                catch { }

                // 画面内にフレーム番号と検出数を表示
                string txtFrame = $"Frame: {frame_id}";
                string txtDet = $"Detections: {detections?.Count ?? 0} / Total: {totalDetections}";
                int confirmedTrackCount = 0;
                try
                {
                    confirmedTrackCount = pipeline.Tracker.TotalTrackConfirmedCount;
                }
                catch { }
                string txtConfirmed = $"Confirmed tracks: {confirmedTrackCount}";
                var pt1 = new OpenCvSharp.Point(10, 24);
                var pt2 = new OpenCvSharp.Point(10, 52);
                var ptConfirmed = new OpenCvSharp.Point(10, 80);
                // 検出パラメータ表示
                string txtParams = $"Bin:{framedetector.BinFactor} K:{framedetector.ThresholdK:F2} MinTh:{framedetector.MinThreshold:F0} MinA:{framedetector.MinAreaPx} Dil:{framedetector.DilateSize}";
                var pt3 = new OpenCvSharp.Point(10, 108);

                var cloudInfo = framedetector.LastCloudMaskInfo;
                string txtCloud = (cloudInfo != null)
                    ? $"Cloud: cov={cloudInfo.CoverageRatio:F3} clear={cloudInfo.GlobalClearLevel:F1} sig={cloudInfo.NoiseSigma:F1} off={cloudInfo.Offset:F1} blk={cloudInfo.UsedBlocks}/{cloudInfo.TotalBlocks}"
                    : "Cloud: n/a";
                var ptCloud = new OpenCvSharp.Point(10, 136);

                // 黒で下書き（アウトライン）
                Cv2.PutText(disp, txtFrame, pt1, HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 0), 3);
                Cv2.PutText(disp, txtDet, pt2, HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 0), 3);
                Cv2.PutText(disp, txtConfirmed, ptConfirmed, HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 0), 3);
                Cv2.PutText(disp, txtParams, pt3, HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 0), 3);
                Cv2.PutText(disp, txtCloud, ptCloud, HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 0, 0), 3);
                // 白で前景文字
                Cv2.PutText(disp, txtFrame, pt1, HersheyFonts.HersheySimplex, 0.8, new Scalar(255, 255, 255), 1);
                Cv2.PutText(disp, txtDet, pt2, HersheyFonts.HersheySimplex, 0.8, new Scalar(255, 255, 255), 1);
                Cv2.PutText(disp, txtConfirmed, ptConfirmed, HersheyFonts.HersheySimplex, 0.8, new Scalar(255, 255, 255), 1);
                Cv2.PutText(disp, txtParams, pt3, HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 1);
                Cv2.PutText(disp, txtCloud, ptCloud, HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);

                Cv2.ImShow("MeteorDetection - Video", disp);
                Cv2.WaitKey(1);
            }
            // maskSmall を解放
            //try { maskSmall?.Dispose(); } catch { }


            //int key = Cv2.WaitKey(1);
            //if (key == 27) break; // ESC で中止
            //videoFrameIndex++;
        }
        

        /// <summary>
        /// 最大値サーチ
        /// </summary>
        public void detect()
        {
            //UsageExample.Grab(imgdata.img, UsageExample.queue, UsageExample.cts.Token);
            //UsageExample.Process(UsageExample.pipeline, UsageExample.queue, img_mask8u_s, UsageExample.cts.Token);
            //UsageExample.pipeline.ProcessFrame(imgdata.img, img_mask8u_s, DateTime.UtcNow);

            ProcessFrameWithPipeline(imgdata_static.img, img_mask8u_s);

            return;

            if (appSettings.CamPlatform == Platform.MT2)
            {
                theta_c = -udpkv.cal_mt2_theta(appSettings.Flipmode, appSettings.FlipOn) - appSettings.Theta;
            }

            if (!appSettings.UseDetect) return;
            int th_id = System.Threading.Thread.CurrentThread.ManagedThreadId; Console.WriteLine("detect ThreadID : " + th_id);

            #region 位置検出1(MaxMin)
            // Mask update
            if (imgdata.id % 30 == 0)
            {
                //using (Mat img_avg = img_mask.Clone())
                { 
                try
                {
                        double gain   = 1.0;
                        double offset = 0.0;//-8;
                        double star_thres = 32;
                 //       Cv2.Min(imgdata.img, img_mask, img2); //fixed Mask
                        //Cv.Sub(imgdata.img, img_dark8, img2);
                        //Cv.ConvertScale(imgAvg, img_avg, gain, offset);
                        img_mask2 = imgAvg.ConvertScaleAbs(gain, offset);
                        //using (var img_avg = img_mask2 - img_dark8)
                        using (var img_avg = img_mask2.Clone())
                        using (Mat binary = img_mask2.Clone())
                        using (Mat binaryAdaptive = img_mask2.Clone())
                        {
                            Cv2.Threshold(img_avg, binary, star_thres, 255, ThresholdTypes.BinaryInv);// 4ms
                            Cv2.AdaptiveThreshold(img_avg, binaryAdaptive, 255,
                                AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 19, star_adaptive_threshold);// 9x9 53ms

                            //cvwin.Image = binaryAdaptive;
                            Cv2.Min(binaryAdaptive, binary, binaryAdaptive);
                            //cvwin.Image = binary;
                            Cv2.Min(img_mask, binaryAdaptive, img_mask2);
                            //cvwin.Image = img_mask2;
                            img2 = imgdata.img - img_avg; // Cv2.Sub(imgdata.img, img_avg, img2);
                            //cvwin.Image = img2;

                            //Cv2.ImShow("binaryAdaptive", binaryAdaptive);
                            //Cv2.ImShow("binary", binary);
                            //Cv2.ImShow("img-avg", img2.PyrDown().PyrDown());
                        }
                    } //  ms
                catch (KeyNotFoundException)
                {
                    MessageBox.Show("KeyNotFoundException:211a");
                }
            }

            try
            {
                double minv;
                Point minloc, maxloc;
                //Cv.Smooth(imgdata.img, img2, SmoothType.Median, 5, 0, 0, 0);
                //Cv.Threshold(img2, img2, appSettings.ThresholdBlob, 255, ThresholdType.Binary); //2ms
                Cv2.MinMaxLoc(img2, out minv, out max_val, out minloc, out maxloc, img_mask2);
                gx = maxloc.X; gy = maxloc.Y;
                //Cv.Threshold(imgdata.img, img2, appSettings.ThresholdBlob, 255, ThresholdType.Binary); //2ms  fishはマスクが必要
                //blobs.Label(img2); //3ms
            }//8ms
            catch (KeyNotFoundException)
            {
                MessageBox.Show("KeyNotFoundException:211");
            }
            }
            #endregion

            #region 位置検出2(Blob)
            try
            {
                //Cv.Smooth(imgdata.img, img2, SmoothType.Median, 5, 0, 0, 0);
                //Cv.Threshold(img2, img2, appSettings.ThresholdBlob, 255, ThresholdType.Binary); //2ms
                //Cv.Min(imgdata.img, img_mask, img2);
                Cv2.Threshold(imgdata.img, img2, appSettings.ThresholdBlob, 255, ThresholdTypes.Binary); //2ms  fishはマスクが必要
                SimpleBlobDetector.Params param = new SimpleBlobDetector.Params();
                param.MaxArea = 100000;

                var detector = SimpleBlobDetector.Create(param);
                var keypoints1 = detector.Detect(img2);
                KeyPoint maxkey = new KeyPoint(new Point2f(0, 0), -1);
                foreach (var keyPoint in keypoints1)
                {
                    if (maxkey.Size < keyPoint.Size) maxkey = keyPoint;
                    Console.WriteLine("X: {0}, Y: {1}", keyPoint.Pt.Y, keyPoint.Pt.Y);
                }
                Console.WriteLine("MAX X: {0}, Y: {1} Size: {2}", maxkey.Pt.Y, maxkey.Pt.Y, maxkey.Size);
                maxKeyPoint = maxkey;
                //blobs.Label(img2); //3ms
            }//8ms
            catch (KeyNotFoundException)
            {
                MessageBox.Show("KeyNotFoundException:211");
            }
          if (appSettings.UseDetect) return;//必ずreturn

            try
            {
                if (blobs.Count > 0)
                {
                    int min_area = Math.Max(2, (int)(appSettings.ThresholdMinArea * maxBlob.Area));
                    blobs.FilterByArea(min_area, int.MaxValue); //2ms 面積がmin_area未満のblobを削除
                }
                max_label = 0;
                if (blobs.Count > 0)
                {
                    max_label = pos_mes.mesure(blobs); //4ms
                    //max_label = pos_mes.mesure(keyPoints1); //4ms
                }
            }//1ms
            catch (KeyNotFoundException)
            {
                MessageBox.Show("KeyNotFoundException:213");
            }

            if (max_label > 0 && blobs.ContainsKey(max_label))
            {
                try
                {
                    maxBlob = blobs[max_label];
                }
                catch (KeyNotFoundException)
                {
                    MessageBox.Show("KeyNotFoundException:2171");
                }
                try
                {
                    max_centroid = maxBlob.Centroid;
                }
                catch (KeyNotFoundException)
                {
                    MessageBox.Show("KeyNotFoundException:2172");
                }
                try
                {
                    gx = max_centroid.X;
                    gy = max_centroid.Y;
                    max_val = maxBlob.Area;
                    blob_rect = maxBlob.Rect;
                }
                catch (KeyNotFoundException)
                {
                    MessageBox.Show("KeyNotFoundException:2173");
                }

                // 観測値(kalman)
                measurement.Set<double>(0, 0, (float)(gx - xoa)); //2ms
                measurement.Set<double>(1, 0, (float)(gy - yoa)); //7ms
                if (kalman_id++ == 0)
                {
                    // 初期値設定
                    double errcov = 1.0;
                    kalman.StatePost.Set(0, measurement.At<double>(0));
                    kalman.StatePost.Set(1, measurement.At<double>(1));
                    Cv2.SetIdentity(kalman.ErrorCovPost, new Scalar(errcov));
                }//2ms
                // 修正フェーズ(kalman)
                try
                {
                    correction = kalman.Correct( measurement );
                }
                catch (KeyNotFoundException)
                {
                    MessageBox.Show("KeyNotFoundException:216");
                }

                // 予測フェーズ(kalman)
                try
                {
                    prediction = kalman.Predict();
                    kgx = prediction.At<double>(0,0) + xoa;
                    kgy = prediction.At<double>(0,1) + yoa;
                    kvx = prediction.At<double>(0,2);
                    kvy = prediction.At<double>(0,3);
                } //1ms
                catch (KeyNotFoundException)
                {
                    MessageBox.Show("KeyNotFoundException:215");
                }

                // カルマン　or　観測重心　の選択
                sgx = gx; sgy = gy;
                if ((Math.Abs(kgx - gx) + Math.Abs(kgy - gy) < 15))  // 
                {
                    sgx = kgx;
                    sgy = kgy;
                    //imgSrc.Circle(new CvPoint((int)(prediction.DataArraySingle[0] + xoa), (int)(prediction.DataArraySingle[1] + yoa)), 30, new CvColor(100, 100, 255));
                    //w2.WriteLine("{0:D3} {1:F2} {2:F2} {3:F2} {4:F2} {5} {6} {7}", i, max_centroid.X, max_centroid.Y, prediction.DataArraySingle[0] + xc, prediction.DataArraySingle[1] + yc, vm, dx, dy);
                }
                dx = sgx - appSettings.Xoa;
                dy = sgy - appSettings.Yoa;

                // 目標位置からの誤差(pix)からターゲットの位置を計算
                if (appSettings.CamPlatform == Platform.MT3)
                {
                    try
                    {
                        theta_c = -udpkv.cal_mt3_theta() - appSettings.Theta;
                        udpkv.cxcy2azalt(-dx, -dy, udpkv.az2_c, udpkv.alt2_c, udpkv.mt3mode, theta_c, appSettings.FocalLength, appSettings.Ccdpx, appSettings.Ccdpy, ref az, ref alt);
                        udpkv.cxcy2azalt(-(dx + kvx), -(dy + kvy), udpkv.az2_c, udpkv.alt2_c, udpkv.mt3mode, theta_c, appSettings.FocalLength, appSettings.Ccdpx, appSettings.Ccdpy, ref az1, ref alt1);
                        vaz = udpkv.vaz2_kv + (az1 - az) * appSettings.Framerate;
                        valt = udpkv.valt2_kv + (alt1 - alt) * appSettings.Framerate;

                        daz = az - udpkv.az2_c; dalt = alt - udpkv.alt2_c;             //位置誤差
                        //dvaz = (daz - daz1) / dt; dvalt = (dalt - dalt1) / dt;        //速度誤差
                        //diff_vaz = (az - az_pre1) / dt; diff_valt = (alt - alt_pre1) / dt; //速度差

                        az0 = az; alt0 = alt;
                    }
                    catch (KeyNotFoundException)
                    {
                        MessageBox.Show("KeyNotFoundException:218");
                    }
                }
                else if (appSettings.CamPlatform == Platform.MT2)
                {
                    try
                    {
                        theta_c = -udpkv.cal_mt2_theta(appSettings.Flipmode, appSettings.FlipOn) - appSettings.Theta;
                        udpkv.cxcy2azalt_mt2(+dx, +dy, udpkv.az1_c, udpkv.alt1_c, udpkv.mt2mode, theta_c, appSettings.FocalLength, appSettings.Ccdpx, appSettings.Ccdpy, ref az, ref alt);
                        udpkv.cxcy2azalt_mt2(+(dx + kvx), +(dy + kvy), udpkv.az1_c, udpkv.alt1_c, udpkv.mt2mode, theta_c, appSettings.FocalLength, appSettings.Ccdpx, appSettings.Ccdpy, ref az1, ref alt1);
                        vaz = udpkv.vaz1_kv + (az1 - az) * appSettings.Framerate;
                        valt = udpkv.valt1_kv + (alt1 - alt) * appSettings.Framerate;

                        daz = az - udpkv.az1_c; dalt = alt - udpkv.alt1_c;             //位置誤差
                        az0 = az; alt0 = alt;
                    }
                    catch (KeyNotFoundException)
                    {
                        MessageBox.Show("KeyNotFoundException:218b");
                    }
                }
                // Data send
                if (ImgSaveFlag == TRUE)
                {
                    // 観測目標移動速作成
                    double vk = 1000;  // [pixel/frame]
                    if (kalman_id > 3)
                    {
                        vk = Math.Sqrt(kvx * kvx + kvy * kvy);
                    }
                    // 観測データ送信
                    //Pid_Data_Send(true);
                    short id_short = (short)frame_id;
                    if (id_short < 0) id_short = (short)(-id_short);
                    Pid_Data_Send_KV1000_SpCam2(id_short, daz, dalt, vk); // 32767->7FFF

                    if (Math.Abs(udpkv.vaz2_kv) > 0.1 || Math.Abs(udpkv.valt2_kv) > 0.1)
                    {
                        // 保存時間延長
                        //timerSavePostTime.Stop();
                        timerSavePost.Stop();
                        timerSavePost.Start();
                    }
                }

            }
            else
            {
                if (ImgSaveFlag == TRUE)
                {
                    // 観測データ送信
                    //Pid_Data_Send(false);
                    ////Pid_Data_Send_KV1000_SpCam2((short)(-(id & 32767)), (az - udpkv.az2_c), (alt - udpkv.alt2_c), -1000);
                }
                gx = gy = 0;
                sgx = sgy = 0;
                max_val = 0;
            }

            // ｋｖデータのチェック用
            if (ImgSaveFlag == TRUE)
            {
                    //xpos = ((kd.x1 << 8) + kd.x0) << 4; // <<16 ->256*256  <<8 ->256
                    //ypos = ((kd.y1 << 8) + kd.y0) << 4; // <<16 ->256*256  <<8 ->256
                    string st = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff ") +"("+ udpkv.xpos + " " + udpkv.ypos + ")( " + udpkv.x2pos + " " + udpkv.y2pos + ") " + udpkv.kd.x1 + " " + udpkv.kd.x0 + " " + udpkv.kd.y1 + " " + udpkv.kd.y0 + "\n";
                    logger.Info(st);
            }

            #endregion

            elapsed2 = sw.ElapsedTicks; sw.Stop(); sw.Reset();
            // 処理速度
            double sf = (double)Stopwatch.Frequency / 1000; //msec
            lap0 = (1 - alpha) * lap0 + alpha * elapsed0 / sf;
            lap1 = (1 - alpha) * lap1 + alpha * elapsed1 / sf;
            lap2 = (1 - alpha) * lap2 + alpha * elapsed2 / sf;
            fr_str = String.Format("ID:{0,5:D1} L0:{1,4:F2} L1:{2,4:F2} L2:{3,4:F2}", frame_id, lap0, lap1, lap2);

            // ワイドダイナミックレンジ用設定 Exp 100-1-100-1-
            if (checkBox_DispMode.Checked)
            {
                // IDS
                if (cam_maker == Camera_Maker.IDS)
                {
                  //  statusRet = cam.Timing.Exposure.Get(out gx);
                  //  if (gx > set_exposure - 1)
                  //      statusRet = cam.Timing.Exposure.Set(set_exposure1);
                  //  else
                  //      statusRet = cam.Timing.Exposure.Set(set_exposure);
                }
            }
        }
        public void SaveAvgImage(string fn = "AvgImage")
        {
            using (Mat img_avg = img_mask.Clone())
            {
                Cv2.ConvertScaleAbs(imgAvg, img_avg, 1.0, 0.0);
                img_avg.SaveImage(fn+".png");
            }
        }

        /// <summary>
        /// kalman 初期化ルーチン
        /// </summary>
        /// <param name="elem">読み出した要素</param> 
        private void kalman_init()
        {
            // 初期化(kalman)
            kalman_id = 0;
            Cv2.SetIdentity(kalman.MeasurementMatrix, new Scalar(1.0));
            Cv2.SetIdentity(kalman.ProcessNoiseCov, new Scalar(1e-4));
            Cv2.SetIdentity(kalman.MeasurementNoiseCov, new Scalar(0.001));
            Cv2.SetIdentity(kalman.ErrorCovPost, new Scalar(1.0));
            measurement.SetTo(new Scalar(0.0));

            // 等速直線運動モデル(kalman)
            kalman.TransitionMatrix.Set<double>(0, 0, 1.0f);
            kalman.TransitionMatrix.Set<double>(0, 1, 0.0f);
            kalman.TransitionMatrix.Set<double>(0, 2, 1.0f);
            kalman.TransitionMatrix.Set<double>(0, 3, 0.0f);

            kalman.TransitionMatrix.Set<double>(1, 0, 0.0f);
            kalman.TransitionMatrix.Set<double>(1, 1, 1.0f);
            kalman.TransitionMatrix.Set<double>(1, 2, 0.0f);
            kalman.TransitionMatrix.Set<double>(1, 3, 1.0f);

            kalman.TransitionMatrix.Set<double>(2, 0, 0.0f);
            kalman.TransitionMatrix.Set<double>(2, 1, 0.0f);
            kalman.TransitionMatrix.Set<double>(2, 2, 1.0f);
            kalman.TransitionMatrix.Set<double>(2, 3, 0.0f);

            kalman.TransitionMatrix.Set<double>(3, 0, 0.0f);
            kalman.TransitionMatrix.Set<double>(3, 1, 0.0f);
            kalman.TransitionMatrix.Set<double>(3, 2, 0.0f);
            kalman.TransitionMatrix.Set<double>(3, 3, 1.0f);
        }

    }
}
