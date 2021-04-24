﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Blob;

namespace AFPv2
{
    partial class Form1
    {
        /// <summary>
        /// 最大値サーチ
        /// </summary>
        public void detect()
        {
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
                    Console.WriteLine("X: {0}, Y: {1}", keyPoint.Pt.X, keyPoint.Pt.Y);
                }
                Console.WriteLine("MAX X: {0}, Y: {1} Size: {2}", maxkey.Pt.X, maxkey.Pt.Y, maxkey.Size);
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
        public void SaveAvgImage()
        {
            using (Mat img_avg = img_mask.Clone())
            {
                Cv2.ConvertScaleAbs(imgAvg, img_avg, 1.0, 0.0);
                img_avg.SaveImage("AvgImage.png");
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
