﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using OpenCvSharp;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System.Windows.Forms;
using System.Runtime.InteropServices;



//=============================================================================
// Copyright (c) 2001-2019 FLIR Systems, Inc. All Rights Reserved.
//
// This software is the confidential and proprietary information of FLIR
// Integrated Imaging Solutions, Inc. ("Confidential Information"). You
// shall not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into
// with FLIR Integrated Imaging Solutions, Inc. (FLIR).
//
// FLIR MAKES NO REPRESENTATIONS OR WARRANTIES ABOUT THE SUITABILITY OF THE
// SOFTWARE, EITHER EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE, OR NON-INFRINGEMENT. FLIR SHALL NOT BE LIABLE FOR ANY DAMAGES
// SUFFERED BY LICENSEE AS A RESULT OF USING, MODIFYING OR DISTRIBUTING
// THIS SOFTWARE OR ITS DERIVATIVES.
//=============================================================================

namespace AFPv2
{
    //class PointGreyCamera
    public partial class Form1 : Form
    {
        const int pgr_WIDTH  = 4096;
        const int pgr_HEIGHT = 3000;
        // set image size
        private const int AllocSize = pgr_WIDTH * pgr_HEIGHT * 1; //IMX352 mono

        // For old PGR
        public double pgr_frame_rate_pre = 0;
        public double alpha_pgr_frame_rate = 0.99;
        public double pgr_image_expo_us { get; set; }
        public double pgr_image_gain { get; set; }
        public double pgr_temparature { get; set; }
        public double pgr_framerate { get; set; }
        public long pgr_chunk_frameID { get; set; }
        public long pgr_image_frame_count { get; set; }
        public long pgr_StreamFailedBufferCount { get; set; }
        public bool pgr_post_save = false;

        public IManagedCamera cam_iel;
        public INodeMap nodeMap_iel, nodeMap_str;

        // Retrieve singleton reference to system object
        internal ManagedSystem pgr_system;// = new ManagedSystem();
        // Retrieve list of cameras from the system
        ManagedCameraList camList;// = pgr_system.GetCameras();
        IManagedCamera imanagedCamera;

        // This class defines the properties, parameters, and the event handler itself.
        // Take a moment to notice what parts of the class are mandatory, and
        // what have been added for demonstration purposes. First, any class
        // used to define image event handlers must inherit from ManagedImageEventHandler.
        // Second, the method signature of OnImageEvent() must also be
        // consistent and follow the override keyword. Everything else,
        // including the constructor, properties, body of OnImageEvent(), and
        // other functions, is particular to the example.
        class ImageEventListener : ManagedImageEventHandler
        {
            object lockObject = new object();
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            private string deviceSerialNumber;
            public const int NumImages = 30 * 60 * 60 * 24 * 7; //kari 1week bun
            public int imageCnt;
            // The constructor retrieves the serial number and initializes the
            // image counter to 0.
            public ImageEventListener(IManagedCamera cam)
            {
                // Initialize image counter to 0
                imageCnt = 0;
                // Retrieve device serial number
                INodeMap nodeMap = cam.GetTLDeviceNodeMap();
                deviceSerialNumber = "";
                IString iDeviceSerialNumber = nodeMap.GetNode<IString>("DeviceSerialNumber");
                if (iDeviceSerialNumber != null && iDeviceSerialNumber.IsReadable)
                {
                    deviceSerialNumber = iDeviceSerialNumber.Value;
                }
            }

            //MarshalクラスのCopyメソッドで一括コピー
            private static void MarshalCopy(IntPtr src, IntPtr dest)
            {
                byte[] temp = new byte[AllocSize];
                Marshal.Copy(src, temp, 0, AllocSize);
                Marshal.Copy(temp, 0, dest, AllocSize);
            }

            //UnmanagedMemoryStreamでbyte配列経由で一括コピー
            private unsafe static void UnsafeStream(IntPtr src, IntPtr dest)
            {
                using (UnmanagedMemoryStream streamSrc = new UnmanagedMemoryStream((byte*)src, AllocSize))
                using (UnmanagedMemoryStream streamDst = new UnmanagedMemoryStream((byte*)dest, AllocSize, AllocSize, FileAccess.Write))
                {
                    byte[] temp = new byte[AllocSize];
                    streamSrc.Read(temp, 0, AllocSize);
                    streamDst.Write(temp, 0, AllocSize);
                }
            }

            // UnmanagedMemoryStreamでCopyToで一括コピー
            private unsafe static void CopyTo(IntPtr src, IntPtr dest, int aSize)
            {
                using (UnmanagedMemoryStream streamSrc = new UnmanagedMemoryStream((byte*)src, aSize))
                using (UnmanagedMemoryStream streamDst = new UnmanagedMemoryStream((byte*)dest, aSize, aSize, FileAccess.Write))
                {
                    streamSrc.CopyTo(streamDst);
                }
            }

            // kernel32.dllのCopyMemory関数を利用して一括コピー
            [DllImport("kernel32.dll")]
            private static extern void CopyMemory(IntPtr dst, IntPtr src, int size);
            private static void CopyMemory(IntPtr src, IntPtr dest)
            {
                CopyMemory(src, dest, AllocSize);
            }

            //
            void write_image(IManagedImage image, int imageCnt)
            {
                // Convert image
                using (IManagedImage convertedImage = image.Convert(PixelFormatEnums.Mono8, ColorProcessingAlgorithm.HQ_LINEAR))
                {
                    // Print image information
                    Console.WriteLine(
                        "Grabbed image {0}, width = {1}, height = {2}",
                        imageCnt,
                        convertedImage.Width,
                        convertedImage.Height);
                    // Create unique filename in order to save file
                    String filename = "rImageEvents-CSharp-";
                    filename = filename + imageCnt + ".jpg";
                    // Save image
                    //convertedImage.Save(filename);
                    image.Save(filename);
                    Console.WriteLine("Image saved at {0}\n", filename);
                }
            }
            //
            // This function converts between Spinnaker::ImagePtr container to cv::Mat container used in OpenCV.
            //
            Mat spinnakerWrapperToCvMat(IManagedImage mimg)
            {
                try
                {
                    uint XPadding = mimg.XPadding;// imagePtr->GetXPadding();
                    uint YPadding = mimg.YPadding;// imagePtr->GetYPadding();
                    uint rowsize = mimg.Width;    // Ptr->GetWidth();
                    uint colsize = mimg.Height;   // Ptr->GetHeight();

                    // Image data contains padding. When allocating cv::Mat container size, you need to account for the X,Y
                    // image data padding.
                    return new Mat((int)(colsize + YPadding), (int)(rowsize + XPadding), MatType.CV_8U, mimg.DataPtr, mimg.Stride);
                }
                catch (Exception ex)
                {
                    string str = ex.Data.ToString();
                    Console.WriteLine("Exception: Error {0}", str);
                    throw;
                }
            }
            void spinnakerWrapperToCvMat(IManagedImage mimg, IntPtr ptr)
            {
                try
                {
                    uint XPadding = mimg.XPadding;// imagePtr->GetXPadding();
                    uint YPadding = mimg.YPadding;// imagePtr->GetYPadding();
                    uint rowsize = mimg.Width;    // Ptr->GetWidth();
                    uint colsize = mimg.Height;   // Ptr->GetHeight();

                    // Image data contains padding. When allocating cv::Mat container size, you need to account for the X,Y
                    // image data padding.
                    //        return new Mat((int)(colsize + YPadding), (int)(rowsize + XPadding), MatType.CV_8U, mimg.DataPtr, mimg.Stride);
                }
                catch (Exception ex)
                {
                    string str = ex.Data.ToString();
                    Console.WriteLine("Exception: Error {0}", str);
                    throw;
                }
            }
            // This method defines an image event. In it, the image that
            // triggered the event is converted and saved before incrementing
            // the count. Please see Acquisition_CSharp example for more
            // in-depth comments on the acquisition of images.
            override protected void OnImageEvent(ManagedImage image)
            {
                if (imageCnt < NumImages)
                {
                    //Console.WriteLine("Image event occurred...");
                    if (image.IsIncomplete)
                    {
                        Console.WriteLine("Image incomplete with image status {0}...\n", image.ImageStatus);
                    }
                    else
                    {
                        //uint width = image.Width;
                        //uint height = image.Height;
                        int asize = (int)(image.Width * image.Height * 1);
                        //Mat mat1 = new Mat(100, 200, MatType.CV_64FC1);
                        //using (Mat mat2 = new Mat((int)height, (int)width, MatType.CV_8U)) //mono8
                        // Convert image
                        //using (IManagedImage convertedImage = image.Convert(PixelFormatEnums.Mono8, ColorProcessingAlgorithm.HQ_LINEAR))
                        //{                            
                        try
                        {
                            lock (imgdata_static.img)
                            {
                                sw.Reset();
                                sw.Start();
                                //imgdata_push_FIFO(frame.Buffer);

                                //img_dmk は使わず、直接imgdata.imgにコピー (0.3ms)
                                // DataPtr  intptr
                                //System.Runtime.InteropServices.Marshal.Copy(image.DataPtr, 0, mat2.Data, image.DataSize);

                                // unsafeバージョン(0.2-0.3ms)
                                //  unsafe
                                // {
                                //      fixed (byte* pbytes = frame.Buffer)
                                //      {
                                //      }
                                // }
                                //MarshalCopy(image.DataPtr, imgdata_static.img.Data);//OK 5ms
                                //CopyMemory(image.DataPtr, imgdata_static.img.Data);//, (int)image.DataSize); 上手くいかない。1.2ms

                                CopyTo(image.DataPtr, imgdata_static.img.Data, asize);//OK 1.5ms
                                sw.Stop();
                                //imgdata_static.img.SaveImage("mats0.jpg");
                                Interlocked.Increment(ref imgdata_static_flag);
                                Interlocked.Increment(ref frame_id);
                                Interlocked.Exchange(ref pgr_frame_id,GetChunkData_frameID(image));
                                Interlocked.Exchange(ref pgr_timestamp, GetChunkData_timestamp(image));
                                //CopyTo(image.DataPtr, mat2.Data, asize);//OK

                                //var mat2 = spinnakerWrapperToCvMat(image);
                                //imgdata_static.img = mat2.Clone();
                                //imgdata_static.img.SaveImage("mats1.jpg");
                                //mat2.SaveImage("mat2.jpg");

                                long microseconds = sw.ElapsedTicks / (System.Diagnostics.Stopwatch.Frequency / (1000L * 1000L));
                                Console.WriteLine("OnImageEvent CopyTo : " + frame_id.ToString() +": ("+ sw.ElapsedMilliseconds.ToString()+")ms "+microseconds );
                            }
                        }
                        catch (SpinnakerException ex)
                        {
                            logger.Error(ex.Data.ToString());
                            Console.WriteLine("SpinnakerException: Error {0}", ex);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex.Data.ToString());
                            string str = ex.Data.ToString();
                            Console.WriteLine("Exception: Error {0}", str);
                            throw;
                            //Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, str });
                        }
                        //}
                        //write_image(image, imageCnt); 
                        // Must manually release the image to prevent buffers on the camera stream from filling up
                        image.Release();
                        // Increment image counter
                        imageCnt++;
                    }
                }
            }
        }

        #region chunkdata
        // Use the following enum and global static variable to select whether
        // chunk data is displayed from the image or the nodemap.
        enum chunkDataType
        {
            Image,
            Nodemap
        }

        static chunkDataType chosenChunkData = chunkDataType.Image;

        // This function configures the camera to add chunk data to each image.
        // It does this by enabling each type of chunk data before enabling
        // chunk data mode. When chunk data is turned on, the data is made
        // available in both the nodemap and each image.
        static int ConfigureChunkData(INodeMap nodeMap)
        {
            int result = 0;

            Console.WriteLine("\n*** CONFIGURING CHUNK DATA ***\n");

            try
            {
                //
                // Activate chunk mode
                //
                // *** NOTES ***
                // Once enabled, chunk data will be available at the end of the
                // payload of every image captured until it is disabled. Chunk
                // data can also be retrieved from the nodemap.
                //
                IBool iChunkModeActive = nodeMap.GetNode<IBool>("ChunkModeActive");
                if (iChunkModeActive == null || !iChunkModeActive.IsWritable)
                {
                    Console.WriteLine("Cannot active chunk mode. Aborting...");
                    return -1;
                }

                iChunkModeActive.Value = true;

                Console.WriteLine("Chunk mode activated...");

                //
                // Enable all types of chunk data
                //
                // *** NOTES ***
                // Enabling chunk data requires working with nodes:
                // "ChunkSelector" is an enumeration selector node and
                // "ChunkEnable" is a boolean. It requires retrieving the
                // selector node (which is of enumeration node type), selecting
                // the entry of the chunk data to be enabled, retrieving the
                // corresponding boolean, and setting it to true.
                //
                // In this example, all chunk data is enabled, so these steps
                // are performed in a loop. Once this is complete, chunk mode
                // still needs to be activated.
                //
                // Retrieve selector node
                IEnum iChunkSelector = nodeMap.GetNode<IEnum>("ChunkSelector");
                if (iChunkSelector == null || !iChunkSelector.IsReadable)
                {
                    Console.WriteLine("Chunk selector not available. Aborting...");
                    return -1;
                }

                // Retrieve entries
                EnumEntry[] entries = iChunkSelector.Entries;

                Console.WriteLine("Enabling entries...");

                for (int i = 0; i < entries.Length; i++)
                {
                    // Select entry to be enabled
                    IEnumEntry iChunkSelectorEntry = entries[i];

                    // Go to next node if problem occurs
                    if (!iChunkSelectorEntry.IsAvailable || !iChunkSelectorEntry.IsReadable)
                    {
                        continue;
                    }

                    iChunkSelector.Value = iChunkSelectorEntry.Value;

                    Console.Write("\t{0}: ", iChunkSelectorEntry.Symbolic);

                    // Retrieve corresponding boolean
                    IBool iChunkEnable = nodeMap.GetNode<IBool>("ChunkEnable");

                    // Enable the boolean, thus enabling the corresponding chunk
                    // data
                    if (iChunkEnable == null)
                    {
                        Console.WriteLine("not available");
                        result = -1;
                    }
                    else if (iChunkEnable.Value)
                    {
                        Console.WriteLine("enabled");
                    }
                    else if (iChunkEnable.IsWritable)
                    {
                        iChunkEnable.Value = true;
                        Console.WriteLine("enabled");
                    }
                    else
                    {
                        Console.WriteLine("not writable");
                        result = -1;
                    }
                }
                Console.WriteLine();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }
        // This function disables each type of chunk data before disabling chunk data mode.
        static int DisableChunkData(INodeMap nodeMap)
        {
            int result = 0;

            try
            {
                // Retrieve selector node
                IEnum iChunkSelector = nodeMap.GetNode<IEnum>("ChunkSelector");
                if (iChunkSelector == null || !iChunkSelector.IsReadable)
                {
                    Console.WriteLine("Chunk selector not available. Aborting...");
                    return -1;
                }

                // Retrieve entries
                EnumEntry[] entries = iChunkSelector.Entries;

                Console.WriteLine("Disabling entries...");

                for (int i = 0; i < entries.Length; i++)
                {
                    // Select entry to be disabled
                    IEnumEntry iChunkSelectorEntry = entries[i];

                    // Go to next node if problem occurs
                    if (!iChunkSelectorEntry.IsAvailable || !iChunkSelectorEntry.IsReadable)
                    {
                        continue;
                    }

                    iChunkSelector.Value = iChunkSelectorEntry.Value;

                    Console.Write("\t{0}: ", iChunkSelectorEntry.Symbolic);

                    // Retrieve corresponding boolean
                    IBool iChunkEnable = nodeMap.GetNode<IBool>("ChunkEnable");

                    // Disable the boolean, thus disabling the corresponding chunk
                    // data
                    if (iChunkEnable == null)
                    {
                        Console.WriteLine("not available");
                        result = -1;
                    }
                    else if (!iChunkEnable.Value)
                    {
                        Console.WriteLine("disabled");
                    }
                    else if (iChunkEnable.IsWritable)
                    {
                        iChunkEnable.Value = false;
                        Console.WriteLine("disabled");
                    }
                    else
                    {
                        Console.WriteLine("not writable");
                    }
                }
                Console.WriteLine();

                // Deactivate ChunkMode
                IBool iChunkModeActive = nodeMap.GetNode<IBool>("ChunkModeActive");
                if (iChunkModeActive == null || !iChunkModeActive.IsWritable)
                {
                    Console.WriteLine("Cannot deactive chunk mode. Aborting...");
                    result = -1;
                }

                iChunkModeActive.Value = false;

                Console.WriteLine("Chunk mode deactivated...");
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }
        // This function displays a select amount of chunk data from the image.
        // Unlike accessing chunk data via the nodemap, there is no way to loop
        // through all available data.
        static int DisplayChunkData(IManagedImage managedImage)
        {
            int result = 0;

            Console.WriteLine("Printing chunk data from image...");

            try
            {
                //
                // Retrieve chunk data from image
                //
                // *** NOTES ***
                // When retrieving chunk data from an image, the data is stored
                // in a a ChunkData object and accessed with getter functions.
                //
                ManagedChunkData managedChunkData = managedImage.ChunkData;

                //
                // Retrieve exposure time; exposure time recorded in microseconds
                //
                // *** NOTES ***
                // In C#, Floating point numbers are returned from chunk data
                // objects as a double.
                //
                double exposureTime = managedChunkData.ExposureTime;
                Console.WriteLine("\tExposure time: {0}", exposureTime);

                //
                // Retrieve frame ID
                //
                // *** NOTES ***
                // In C#, Integers are returned as a long.
                //
                long frameID = managedChunkData.FrameID;
                Console.WriteLine("\tFrame ID: {0}", frameID);

                // Retrieve gain; gain recorded in decibels
                double gain = managedChunkData.Gain;
                Console.WriteLine("\tGain: {0}", gain);

                // Retrieve height; height recorded in pixels
                long height = managedChunkData.Height;
                Console.WriteLine("\tHeight: {0}", height);

                // Retrieve offset X; offset X recorded in pixels
                long offsetX = managedChunkData.OffsetX;
                Console.WriteLine("\tOffset X: {0}", offsetX);

                // Retrieve offset Y; offset Y recorded in pixels
                long offsetY = managedChunkData.OffsetY;
                Console.WriteLine("\tOffset Y: {0}", offsetY);

                // Retrieve sequencer set active
                long sequencerSetActive = managedChunkData.SequencerSetActive;
                Console.WriteLine("\tSequencer set active: {0}", sequencerSetActive);

                // Retrieve timestamp
                long timestamp = managedChunkData.Timestamp;
                Console.WriteLine("\tTimestamp: {0}", timestamp);

                // Retrieve width; width recorded in pixels
                long width = managedChunkData.Width;
                Console.WriteLine("\tWidth: {0}", width);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }
        static long GetChunkData_frameID(IManagedImage managedImage)
        {
            long result = -2;
            try
            {
                //
                // Retrieve chunk data from image
                //
                // *** NOTES ***
                // When retrieving chunk data from an image, the data is stored
                // in a a ChunkData object and accessed with getter functions.
                //
                ManagedChunkData managedChunkData = managedImage.ChunkData;

                //
                // Retrieve frame ID
                //
                // *** NOTES ***
                // In C#, Integers are returned as a long.
                //
                result = managedChunkData.FrameID;
                //Console.WriteLine("\tFrame ID: {0}", frameID);

            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }
        static long GetChunkData_timestamp(IManagedImage managedImage)
        {
            long result = -2;
            try
            {
                //
                // Retrieve chunk data from image
                //
                // *** NOTES ***
                // When retrieving chunk data from an image, the data is stored
                // in a a ChunkData object and accessed with getter functions.
                //
                ManagedChunkData managedChunkData = managedImage.ChunkData;

                //
                // Retrieve frame ID
                //
                // *** NOTES ***
                // In C#, Integers are returned as a long.
                //
                result = managedChunkData.Timestamp;
                //Console.WriteLine("\tFrame ID: {0}", frameID);

            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }
        #endregion

        // This function configures the example to execute image events by
        // preparing and registering an image event.
        int ConfigureImageEvents(IManagedCamera cam, ref ImageEventListener imageEventListener)
        {
            int result = 0;
            Console.WriteLine("\n\n*** CONFIGURING IMAGE EVENTS ***\n");
            try
            {
                //
                // Create image event
                //
                // *** NOTES ***
                // The class has been constructed to accept a managed camera
                // in order to allow the saving of images with the device
                // serial number.
                //
                imageEventListener = new ImageEventListener(cam);
                //
                // Register image event handler
                //
                // *** NOTES ***
                // Image events are registered to cameras. If there are
                // multiple cameras, each camera must have the image events
                // registered to it separately. Also, multiple image events may
                // be registered to a single camera.
                //
                // *** LATER ***
                // Image events must be unregistered manually. This must be
                // done prior to releasing the system and while the image
                // events are still in scope.
                //
                cam.RegisterEventHandler(imageEventListener);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }
            return result;
        }
        // This function waits for the appropriate amount of images. Notice
        // that whereas most examples actively retrieve images, the acquisition
        // of images is handled passively in this example.
        int WaitForImages(ref ImageEventListener imageEventListener)
        {
            int result = 0;
            try
            {
                //
                // Wait for images
                //
                // *** NOTES ***
                // In order to passively capture images using image events and
                // automatic polling, the main thread sleeps in increments of
                // 20 ms until 10 images have been acquired and saved.
                //
                const int sleepDuration = 1;
                while (imageEventListener.imageCnt < ImageEventListener.NumImages && pgc_cam_stop_flag == 0)
                {
                    //Console.WriteLine("\t//");
                    //Console.WriteLine("\t// Sleeping for {0} time slices. Grabbing images...", sleepDuration);
                    //Console.WriteLine("\t//");
                    Thread.Sleep(sleepDuration);
                    if (imgdata_static_flag > 0) //画像が準備できるとinc
                    {
                        ++frame_fifo_id;
                        detect();

                        sw.Reset(); sw.Start();
                        imgdata_static_push_FIFO();
                        Interlocked.Exchange(ref imgdata_static_flag, 0);
                        sw.Stop();
                        long microseconds = sw.ElapsedTicks / (System.Diagnostics.Stopwatch.Frequency / (1000L * 1000L));
                        Console.WriteLine("imgdata_static_push_FIFO() : " + pgr_frame_id.ToString() + ": (" + sw.ElapsedMilliseconds.ToString() + ")ms " + microseconds);

                        // update パラメータ
                        if (pgr_frame_id % 32 == 0) // about 1sec
                        {
                            try
                            {
                                IFloat val;
                                IInteger ival;
                                val = nodeMap_iel.GetNode<IFloat>("Gain");
                                if (val == null || !val.IsReadable)
                                {
                                    string s = "Unable to read 'Gain' (node retrieval).\n";
                                    logger.Error(s);
                                    Console.WriteLine(s);
                                }
                                else
                                    pgr_image_gain = val.Value;
                                
                                val = nodeMap_iel.GetNode<IFloat>("DeviceTemperature");
                                if (val == null || !val.IsReadable)
                                {
                                    string s = "Unable to read 'DeviceTemperature' (node retrieval).\n";
                                    logger.Error(s);
                                    Console.WriteLine(s);
                                }
                                else
                                    pgr_temparature = val.Value;
                                
                                val = nodeMap_iel.GetNode<IFloat>("ExposureTime");
                                if (val == null || !val.IsReadable)
                                {
                                    string s = "Unable to read 'ExposureTime' (node retrieval).\n";
                                    logger.Error(s);
                                    Console.WriteLine(s);
                                }
                                else
                                    pgr_image_expo_us = val.Value;

                                val = nodeMap_iel.GetNode<IFloat>("AcquisitionFrameRate");
                                if (val == null || !val.IsReadable)
                                {
                                    string s = "Unable to read 'AcquisitionFrameRate' (node retrieval).\n";
                                    logger.Error(s);
                                    Console.WriteLine(s);
                                }
                                else
                                    pgr_framerate = val.Value;

                                nodeMap_str = cam_iel.GetTLStreamNodeMap();
                                ival = nodeMap_str.GetNode<IInteger>("StreamDeliveredFrameCount");
                                if (ival == null || !ival.IsReadable)
                                {
                                    string s = "Unable to read 'StreamFailedBufferCount' (node retrieval).\n";
                                    logger.Error(s);
                                    Console.WriteLine(s);
                                }
                                else
                                    pgr_image_frame_count = ival.Value;

                                nodeMap_str = cam_iel.GetTLStreamNodeMap();
                                ival = nodeMap_str.GetNode<IInteger>("StreamFailedBufferCount");
                                if (ival == null || !ival.IsReadable)
                                {
                                    string s = "Unable to read 'StreamFailedBufferCount' (node retrieval).\n";
                                    logger.Error(s);
                                    Console.WriteLine(s);
                                }
                                else
                                    pgr_StreamFailedBufferCount = ival.Value;
                                
                            }
                            catch (SpinnakerException ex)
                            {
                                logger.Error(ex.Data.ToString());
                                Console.WriteLine("Spinnaker Error: {0}", ex.Message);
                                result = -1;
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex.Data.ToString());
                                Console.WriteLine("Error: {0}", ex.Message);
                                result = -1;
                            }
                        }
                    }
                    pgc_cam_stop_flag = 0;
                }
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }
            return result;
        }
        // This functions resets the example by unregistering the image event handler.
        int ResetImageEvents(IManagedCamera cam, ref ImageEventListener imageEventListener)
        {
            int result = 0;
            try
            {
                //
                // Unregister image event handler
                //
                // *** NOTES ***
                // It is important to unregister all image event handlers from all
                // cameras they are registered to.
                //
                cam.UnregisterEventHandler(imageEventListener);
                Console.WriteLine("Image events unregistered...\n");
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }
            return result;
        }
        static int PgUserSetLoad(INodeMap nodeMap)
        {
            int result = 0;
            try
            {
                Enumeration icom = nodeMap.GetNode<Enumeration>("UserSetSelector");
                if (icom != null && icom.IsReadable)
                {
                    Console.WriteLine("User Set : "+icom.Value.ToString()+"\n");
                }
                else
                {
                    Console.WriteLine("UserSetSelector not read.");
                }
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            try
            {
                ICommand icom = nodeMap.GetNode<ICommand>("UserSetLoad");
                if ( icom != null && icom.IsWritable)
                {
                    icom.Execute();
                    Console.WriteLine("\n*** User Set Load ***\n");
                }
                else
                {
                    Console.WriteLine("User Set Load not execute.");
                }
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }
            return result;
        }
        static int PgExposureAuto(INodeMap nodeMap,bool mode=true)
        {
            int result = 0;
            try
            {
                // Set Exposure mode to continuous
                IEnum iExposureMode = nodeMap.GetNode<IEnum>("ExposureAuto");
                if (iExposureMode == null || !iExposureMode.IsWritable)
                {
                    Console.WriteLine("Unable to set acquisition mode to continuous (node retrieval). Aborting...\n");
                    return -1;
                }
                string st;
                if (mode == true) {
                    st = "Continuous";
                    result = 1;
                } else
                {
                    st = "Off";
                    result = 0;
                }
                IEnumEntry iExposureModeContinuous = iExposureMode.GetEntryByName(st);
                if (iExposureModeContinuous == null || !iExposureMode.IsReadable)
                {
                    Console.WriteLine(
                        "Unable to set acquisition mode to continuous/off (enum entry retrieval). Aborting...\n");
                    return -1;
                }
                iExposureMode.Value = iExposureModeContinuous.Symbolic;
                Console.WriteLine("Acquisition mode set to {0}...",st);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("SpinnakerException Error1: {0}", ex.Message);
                //throw;
                result = -1;
            }
            return result;
        }
        static int PgSetExposure(INodeMap nodeMap, double val = 33099.59)// val [usec]
        {
            int result = 0;
            try
            {
                // Set Exposure [us]
                IFloat iExposureMode = nodeMap.GetNode<IFloat>("ExposureTime");
                if (iExposureMode == null || !iExposureMode.IsWritable)
                {
                    Console.WriteLine("Unable to set exposure time (node retrieval). Aborting...\n");
                    return -1;
                }
                if (iExposureMode.Max < val) val = iExposureMode.Max;
                if (iExposureMode.Min > val) val = iExposureMode.Min;
                iExposureMode.Value = val;
                Console.WriteLine("Exposure set to {0}...", val);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("SpinnakerException Error1: {0}", ex.Message);
                //throw;
                result = -1;
            }
            return result;
        }
        static int PgGainAuto(INodeMap nodeMap, bool mode = true)
        {
            int result = 0;
            try
            {
                // Set Exposure mode to continuous
                IEnum iExposureMode = nodeMap.GetNode<IEnum>("GainAuto");
                if (iExposureMode == null || !iExposureMode.IsWritable)
                {
                    Console.WriteLine("Unable to set GainAuto mode to continuous (node retrieval). Aborting...\n");
                    return -1;
                }
                string st;
                if (mode == true)
                {
                    st = "Continuous";
                    result = 1;
                }
                else
                {
                    st = "Off";
                    result = 0;
                }
                IEnumEntry iExposureModeContinuous = iExposureMode.GetEntryByName(st);
                if (iExposureModeContinuous == null || !iExposureMode.IsReadable)
                {
                    Console.WriteLine(
                        "Unable to set acquisition mode to continuous/off (enum entry retrieval). Aborting...\n");
                    return -1;
                }
                iExposureMode.Value = iExposureModeContinuous.Symbolic;
                Console.WriteLine("GainAuto mode set to {0}...", st);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("SpinnakerException Error1: {0}", ex.Message);
                //throw;
                result = -1;
            }
            return result;
        }
        static int PgSetGain(INodeMap nodeMap, double val = 40.00)// val [dB]
        {
            int result = 0;
            try
            {
                // Set Exposure [us]
                IFloat iExposureMode = nodeMap.GetNode<IFloat>("Gain");
                if (iExposureMode == null || !iExposureMode.IsWritable)
                {
                    Console.WriteLine("Unable to set gain (node retrieval). Aborting...\n");
                    return -1;
                }
                if (iExposureMode.Max < val) val = iExposureMode.Max;
                if (iExposureMode.Min > val) val = iExposureMode.Min;
                iExposureMode.Value = val;
                Console.WriteLine("Gain set to {0}...", val);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("SpinnakerException Error1: {0}", ex.Message);
                //throw;
                result = -1;
            }
            return result;
        }
        static int PgSetGamma(INodeMap nodeMap, double val = 1.0)
        {
            int result = 0;
            try
            {
                // Set Gamma []
                IFloat iExposureMode = nodeMap.GetNode<IFloat>("Gamma");
                if (iExposureMode == null || !iExposureMode.IsWritable)
                {
                    Console.WriteLine("Unable to set gamma (node retrieval). Aborting...\n");
                    return -1;
                }
                if (iExposureMode.Max < val) val = iExposureMode.Max;
                if (iExposureMode.Min > val) val = iExposureMode.Min;
                iExposureMode.Value = val;
                Console.WriteLine("Gamma set to {0}...", val);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("SpinnakerException Error1: {0}", ex.Message);
                //throw;
                result = -1;
            }
            return result;
        }
        // This function prints the device information of the camera from the
        // transport layer; please see NodeMapInfo_CSharp example for more
        // in-depth comments on printing device information from the nodemap.
        static int PrintDeviceInfo(INodeMap nodeMap)
        {
            int result = 0;
            try
            {
                Console.WriteLine("\n*** DEVICE INFORMATION ***\n");
                ICategory category = nodeMap.GetNode<ICategory>("DeviceInformation");
                if (category != null && category.IsReadable)
                {
                    for (int i = 0; i < category.Children.Length; i++)
                    {
                        Console.WriteLine(
                            "{0}: {1}",
                            category.Children[i].Name,
                            (category.Children[i].IsReadable ? category.Children[i].ToString()
                             : "Node not available"));
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("Device control information not available.");
                }
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }
            return result;
        }
        // This function passively waits for images by calling WaitForImages().
        // Notice that this function is much shorter than the AcquireImages()
        // function of other examples. This is because most of the code has
        // been moved to the image event's OnImageEvent() method.
        int AcquireImages(
            IManagedCamera cam,
            INodeMap nodeMap,
            INodeMap nodeMapTLDevice,
            ref ImageEventListener imageEventListener)
        {
            int result = 0;
            Console.WriteLine("\n*** IMAGE ACQUISITION ***\n");
            try
            {
                // Set acquisition mode to continuous
                IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");
                if (iAcquisitionMode == null || !iAcquisitionMode.IsWritable)
                {
                    Console.WriteLine("Unable to set acquisition mode to continuous (node retrieval). Aborting...\n");
                    return -1;
                }
                IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("Continuous");
                if (iAcquisitionModeContinuous == null || !iAcquisitionMode.IsReadable)
                {
                    Console.WriteLine(
                        "Unable to set acquisition mode to continuous (enum entry retrieval). Aborting...\n");
                    return -1;
                }
                iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;
                Console.WriteLine("Acquisition mode set to continuous...");
                // Begin acquiring images
                cam.BeginAcquisition();
                Console.WriteLine("Acquiring images...");
                // Retrieve images using image event handler
                result = WaitForImages(ref imageEventListener);
                // End acquisition
                cam.EndAcquisition();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("SpinnakerException Error1: {0}", ex.Message);
                //throw;
                result = -1;
            }
            return result;
        }

        // This function acts as the body of the example; please see
        // NodeMapInfo_CSharp example for more in-depth comments on setting up
        // cameras.
        int RunSingleCamera(IManagedCamera cam)
        {
            int result = 0;
            int err = 0;
            try
            {
                // Retrieve TL device nodemap and print device information
                INodeMap nodeMapTLDevice = cam.GetTLDeviceNodeMap();
                result = PrintDeviceInfo(nodeMapTLDevice);
                // Initialize camera
                cam.Init();
                // Retrieve GenICam nodemap
                INodeMap nodeMap = cam.GetNodeMap();
                nodeMap_iel = nodeMap;
                cam_iel = cam;
                // Configure image event handlers
                ImageEventListener imageEventListener = null;
                err = ConfigureImageEvents(cam, ref imageEventListener);
                if (err < 0)
                {
                    return err;
                }
                PgUserSetLoad(nodeMap_iel);// UserSetLoad
                PgSetGamma(nodeMap_iel, 1.0); // gamma=1

                // Configure chunk data
                err = ConfigureChunkData(nodeMap);
                if (err < 0)
                {
                    return err;
                }

                // Acquire images using the image event handler
                result = result | AcquireImages(cam, nodeMap, nodeMapTLDevice, ref imageEventListener);
                // Reset image event handlers
                result = result | ResetImageEvents(cam, ref imageEventListener);

                // Disable chunk data
                err = DisableChunkData(nodeMap);
                if (err < 0)
                {
                    return err;
                }

                // Deinitialize camera
                cam.DeInit();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("pinnakerException Error2: {0}", ex.Message);
                result = -1;
            }
            return result;
        }

        // Example entry point; please see Enumeration_CSharp example for more
        // in-depth comments on preparing and cleaning up the system.
        internal int initPGRcamera()
        {
            int result = 0;
            // Retrieve singleton reference to system object
            pgr_system = new ManagedSystem();
            // Print out current library version
            LibraryVersion spinVersion = pgr_system.GetLibraryVersion();
            Console.WriteLine(
                "Spinnaker library version: {0}.{1}.{2}.{3}\n\n",
                spinVersion.major,
                spinVersion.minor,
                spinVersion.type,
                spinVersion.build);
            // Retrieve list of cameras from the system
            camList = pgr_system.GetCameras();
            Console.WriteLine("Number of cameras detected: {0}\n\n", camList.Count);
            // Finish if there are no cameras
            if (camList.Count == 0)
            {
                // Clear camera list before releasing system
                camList.Clear();
                // Release system
                pgr_system.Dispose();
                Console.WriteLine("Not enough cameras!");
                Console.WriteLine("Done! Press Enter to exit...");
                Console.ReadLine();
                return -1;
            }
            return result;
        }

        internal int OpenPGRcamera() //(IProgress<int> p, CancellationToken cancelToken) //RunPGRcamera()
        {
            initPGRcamera();

            int result = 0;
            // Run example on each camera
            int index = 0;
            foreach (IManagedCamera managedCamera in camList)  using (managedCamera) 
                {
                    Console.WriteLine("Running example for camera {0}...", index);
                    try
                    {
                        // Run example
                        result = result | RunSingleCamera(managedCamera);
                    }
                    catch (SpinnakerException ex)
                    {
                        logger.Error(ex.Data.ToString());
                        Console.WriteLine("Error: {0}", ex.Message);
                        result = -1;
                    }
                    Console.WriteLine("Camera {0} example complete...\n", index++);
                }
            return result;
        }

        internal void ClosePGRcamera()
        {
            Thread.Sleep(2000);
            // Clear camera list before releasing system
            camList.Clear();
            // Release system
            pgr_system.Dispose();
            Console.WriteLine("\nDone!");
            //Console.ReadLine();
            //return result;
        }

        /// <summary>
        /// FIFO pushルーチン for PGC
        /// imgdata_static.img　は　すでにセット済み
        /// </summary>
        private void imgdata_static_push_FIFO()  //2msくらい
        {
            lock (imgdata_static.img)
            {
                // 文字入れ
                //String str = String.Format("ID:{0,6:D1} ", imgdata.id) + imgdata.t.ToString("yyyyMMdd_HHmmss_fff") + String.Format(" ({0,6:F1},{1,6:F1})({2,6:F1})", gx, gy, max_val);
                //img_dmk.PutText(str, new CvPoint(10, 460), font, new Scalar(255, 100, 100));

                //try
                //{
                //Cv.Sub(img_dmk, img_dark8, imgdata.img); // dark減算
                //Cv.Copy(img_dmk, imgdata.img);
                // cam.Information.GetImageInfo(s32MemID, out imageInfo);
                imgdata_static.id = (int)pgr_frame_id;     // (int)imageInfo.FrameNumber;
                imgdata_static.t = DateTime.Now; //imageInfo.TimestampSystem;   //  LiveStartTime.AddSeconds(CurrentBuffer.SampleEndTime);
                imgdata_static.ImgSaveFlag = !(ImgSaveFlag != 0); //int->bool変換
                                                                  //statusRet = cam.Timing.Exposure.Get(out exp);
                imgdata_static.gx = gx;
                imgdata_static.gy = gy;
                imgdata_static.kgx = kgx;
                imgdata_static.kgy = kgy;
                imgdata_static.kvx = kvx;
                imgdata_static.kvy = kvy;
                imgdata_static.vmax = max_val;
                imgdata_static.blobs = blobs;
                imgdata_static.udpkv1 = (Udp_kv)udpkv.Clone();
                imgdata_static.az = az;
                imgdata_static.alt = alt;
                imgdata_static.vaz = vaz;
                imgdata_static.valt = valt;
                imgdata_static.timestamp = pgr_timestamp;
                if (fifo.Count == appSettings.FifoMaxFrame - 1) fifo.EraseLast();
                fifo.InsertFirst(ref imgdata_static);

                /*}
                catch (Exception ex)
                {
                    //匿名デリゲートで表示する
                    this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, ex.ToString() });
                    System.Diagnostics.Trace.WriteLine(ex.Message);
                }*/
                double alfa = 0.05;
                if (pgr_frame_id % 4 == 0) // mabiki
                {
                    //Cv2.RunningAvg(imgdata.img, imgAvg, 0.05); // 6ms
                    //Cv2.AccumulateWeighted(fifo.Last().img, imgAvg, alfa, null);
                    Cv2.AccumulateWeighted(imgdata_static.img, imgAvg, alfa, null); // 6msくらい
                }
            }
        }
 
    }

}


