using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System.Runtime.InteropServices;

namespace AFPv2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

#if DEBUG
        // Disables heartbeat on GEV cameras so debugging does not incur timeout errors
        static int DisableHeartbeat(IManagedCamera cam, INodeMap nodeMap, INodeMap nodeMapTLDevice)
        {
            Console.WriteLine("Checking device type to see if we need to disable the camera's heartbeat...\n\n");

            //
            // Write to boolean node controlling the camera's heartbeat
            //
            // *** NOTES ***
            // This applies only to GEV cameras and only applies when in DEBUG mode.
            // GEV cameras have a heartbeat built in, but when debugging applications the
            // camera may time out due to its heartbeat. Disabling the heartbeat prevents
            // this timeout from occurring, enabling us to continue with any necessary debugging.
            // This procedure does not affect other types of cameras and will prematurely exit
            // if it determines the device in question is not a GEV camera.
            //
            // *** LATER ***
            // Since we only disable the heartbeat on GEV cameras during debug mode, it is better
            // to power cycle the camera after debugging. A power cycle will reset the camera
            // to its default settings.
            //
            IEnum iDeviceType = nodeMapTLDevice.GetNode<IEnum>("DeviceType");
            IEnumEntry iDeviceTypeGEV = iDeviceType.GetEntryByName("GigEVision");
            // We first need to confirm that we're working with a GEV camera
            if (iDeviceType != null && iDeviceType.IsReadable)
            {
                if (iDeviceType.Value == iDeviceTypeGEV.Value)
                {
                    Console.WriteLine(
                        "Working with a GigE camera. Attempting to disable heartbeat before continuing...\n\n");
                    IBool iGEVHeartbeatDisable = nodeMap.GetNode<IBool>("GevGVCPHeartbeatDisable");
                    if (iGEVHeartbeatDisable == null || !iGEVHeartbeatDisable.IsWritable)
                    {
                        Console.WriteLine(
                            "Unable to disable heartbeat on camera. Continuing with execution as this may be non-fatal...");
                    }
                    else
                    {
                        iGEVHeartbeatDisable.Value = true;
                        Console.WriteLine("WARNING: Heartbeat on GigE camera disabled for the rest of Debug Mode.");
                        Console.WriteLine(
                            "         Power cycle camera when done debugging to re-enable the heartbeat...");
                    }
                }
                else
                {
                    Console.WriteLine("Camera does not use GigE interface. Resuming normal execution...\n\n");
                }
            }
            else
            {
                Console.WriteLine("Unable to access TL device nodemap. Aborting...");
                return -1;
            }

            return 0;
        }
#endif

        private void Button1_Click(object sender, EventArgs e)
        {
            // 画像の読み込み
            using (Mat mat = new Mat(@"C:\Users\root\Pictures\MT3Color_aliment.jpg"))
            {
                // 画像をウィンドウに表示
                Cv2.ImShow("sample_show", mat);
            }
        }

        // set image size
        private const int AllocSize = 4096 * 3000 * 1;
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
        private unsafe static void CopyTo(IntPtr src, IntPtr dest)
        {
            using (UnmanagedMemoryStream streamSrc = new UnmanagedMemoryStream((byte*)src, AllocSize))
            using (UnmanagedMemoryStream streamDst = new UnmanagedMemoryStream((byte*)dest, AllocSize, AllocSize, FileAccess.Write))
            {
                streamSrc.CopyTo(streamDst);
            }
        }

        // kernel32.dllのCopyMemory関数を利用して一括コピー
        [DllImport("kernel32.dll")]        private static extern void CopyMemory(IntPtr dst, IntPtr src, int size);
        private static void CopyMemory(IntPtr src, IntPtr dest)
        {
            CopyMemory(src, dest, AllocSize);
        }


        // This function acquires and saves 10 images from a device.
        static int AcquireImages(IManagedCamera cam, INodeMap nodeMap, INodeMap nodeMapTLDevice)
        {
            int result = 0;

            Console.WriteLine("\n*** IMAGE ACQUISITION ***\n");

            try
            {
                //
                // Set acquisition mode to continuous
                //
                // *** NOTES ***
                // Because the example acquires and saves 10 images, setting
                // acquisition mode to continuous lets the example finish. If
                // set to single frame or multiframe (at a lower number of
                // images), the example would just hang. This is because the
                // example has been written to acquire 10 images while the
                // camera would have been programmed to retrieve less than that.
                //
                // Setting the value of an enumeration node is slightly more
                // complicated than other node types. Two nodes are required:
                // first, the enumeration node is retrieved from the nodemap and
                // second, the entry node is retrieved from the enumeration node.
                // The symbolic of the entry node is then set as the new value
                // of the enumeration node.
                //
                // Notice that both the enumeration and entry nodes are checked
                // for availability and readability/writability. Enumeration
                // nodes are generally readable and writable whereas entry
                // nodes are only ever readable.
                //
                // Retrieve enumeration node from nodemap
                IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");
                if (iAcquisitionMode == null || !iAcquisitionMode.IsWritable)
                {
                    Console.WriteLine("Unable to set acquisition mode to continuous (node retrieval). Aborting...\n");
                    return -1;
                }

                // Retrieve entry node from enumeration node
                IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("Continuous");
                if (iAcquisitionModeContinuous == null || !iAcquisitionMode.IsReadable)
                {
                    Console.WriteLine(
                        "Unable to set acquisition mode to continuous (enum entry retrieval). Aborting...\n");
                    return -1;
                }

                // Set symbolic from entry node as new value for enumeration node
                iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;

                Console.WriteLine("Acquisition mode set to continuous...");

#if DEBUG
                Console.WriteLine("\n\n*** DEBUG ***\n\n");
                // If using a GEV camera and debugging, should disable heartbeat first to prevent further issues

                if (DisableHeartbeat(cam, nodeMap, nodeMapTLDevice) != 0)
                {
                    return -1;
                }

                Console.WriteLine("\n\n*** END OF DEBUG ***\n\n");
#endif
                //
                // Begin acquiring images
                //
                // *** NOTES ***
                // What happens when the camera begins acquiring images depends
                // on which acquisition mode has been set. Single frame captures
                // only a single image, multi frame catures a set number of
                // images, and continuous captures a continuous stream of images.
                // Because the example calls for the retrieval of 10 images,
                // continuous mode has been set for the example.
                //
                // *** LATER ***
                // Image acquisition must be ended when no more images are needed.
                //
                cam.BeginAcquisition();

                Console.WriteLine("Acquiring images...");

                //
                // Retrieve device serial number for filename
                //
                // *** NOTES ***
                // The device serial number is retrieved in order to keep
                // different cameras from overwriting each other's images.
                // Grabbing image IDs and frame IDs make good alternatives for
                // this purpose.
                //
                String deviceSerialNumber = "";

                IString iDeviceSerialNumber = nodeMapTLDevice.GetNode<IString>("DeviceSerialNumber");
                if (iDeviceSerialNumber != null && iDeviceSerialNumber.IsReadable)
                {
                    deviceSerialNumber = iDeviceSerialNumber.Value;

                    Console.WriteLine("Device serial number retrieved as {0}...", deviceSerialNumber);
                }
                Console.WriteLine();

                // Retrieve, convert, and save images
                const int NumImages = 1000;

                for (int imageCnt = 0; imageCnt < NumImages; imageCnt++)
                {
                    try
                    {
                        //
                        // Retrieve next received image
                        //
                        // *** NOTES ***
                        // Capturing an image houses images on the camera buffer.
                        // Trying to capture an image that does not exist will
                        // hang the camera.
                        //
                        // Using-statements help ensure that images are released.
                        // If too many images remain unreleased, the buffer will
                        // fill, causing the camera to hang. Images can also be
                        // released manually by calling Release().
                        //
                        using (IManagedImage rawImage = cam.GetNextImage(1000))
                        {
                            //
                            // Ensure image completion
                            //
                            // *** NOTES ***
                            // Images can easily be checked for completion. This
                            // should be done whenever a complete image is
                            // expected or required. Alternatively, check image
                            // status for a little more insight into what
                            // happened.
                            //
                            if (rawImage.IsIncomplete)
                            {
                                Console.WriteLine("Image incomplete with image status {0}...", rawImage.ImageStatus);
                            }
                            else
                            {
                                //
                                // Print image information; width and height
                                // recorded in pixels
                                //
                                // *** NOTES ***
                                // Images have quite a bit of available metadata
                                // including CRC, image status, and offset
                                // values to name a few.
                                //
                                uint width = rawImage.Width;

                                uint height = rawImage.Height;

                                Console.WriteLine(
                                    "Grabbed image {0}, width = {1}, height = {2}", imageCnt, width, height);

                                // 画像の切り抜き
                                //Mat mat1 = new Mat(100, 200, MatType.CV_64FC1);
                                using (Mat mat2 = new Mat((int)height, (int)width, MatType.CV_8U)) //mono8
                                {
                                    try
                                    {
                                        //camera.QueueFrame(frame);

                                        //System.Object lockThis = new System.Object();
                                        //lock (lockThis)
                                        lock (rawImage)
                                        {
                                            //imgdata_push_FIFO(frame.Buffer);

                                            //img_dmk は使わず、直接imgdata.imgにコピー (0.3ms)
                                            // DataPtr  intptr
                                            //System.Runtime.InteropServices.Marshal.Copy(rawImage.DataPtr, 0, mat2.Data, rawImage.DataSize);

                                            // unsafeバージョン(0.2-0.3ms)
                                            //  unsafe
                                            // {
                                            //      fixed (byte* pbytes = frame.Buffer)
                                            //      {
                                            //MarshalCopy(rawImage.DataPtr, mat2.Data);//OK
                                            //CopyMemory(rawImage.DataPtr, mat2.Data);//, (int)rawImage.DataSize); 上手くいかない。
                                            CopyTo(rawImage.DataPtr, mat2.Data);//OK
                                                                                //      }
                                                                                // }
                                        }
                                    }
                                    catch (SpinnakerException ex)
                                    {
                                        Console.WriteLine("Spin. Error {0}", ex);
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.Data.ToString();
                                        Console.WriteLine("Error {0}", str);
                                        //Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, str });
                                    }

                                    Cv2.ImShow("grayscale_show", mat2);
                                }

                                //
                                // Convert image to mono 8
                                //
                                // *** NOTES ***
                                // Images can be converted between pixel formats
                                // by using the appropriate enumeration value.
                                // Unlike the original image, the converted one
                                // does not need to be released as it does not
                                // affect the camera buffer.
                                //
                                // Using statements are a great way to ensure code
                                // stays clean and avoids memory leaks.
                                // leaks.
                                //
                                using (IManagedImage convertedImage = rawImage.Convert(PixelFormatEnums.Mono8))
                                {
                                    // Create a unique filename
                                    String filename = "Acquisition-CSharp-";
                                    if (deviceSerialNumber != "")
                                    {
                                        filename = filename + deviceSerialNumber + "-";
                                    }
                                    filename = filename + imageCnt + ".jpg";

                                    //
                                    // Save image
                                    //
                                    // *** NOTES ***
                                    // The standard practice of the examples is
                                    // to use device serial numbers to keep
                                    // images of one device from overwriting
                                    // those of another.
                                    //

                                    ///convertedImage.Save(filename);

                                    Console.WriteLine("Image saved at {0}\n", filename);
                                }
                            }
                        }
                    }
                    catch (SpinnakerException ex)
                    {
                        Console.WriteLine("Error: {0}", ex.Message);
                        result = -1;
                    }
                }

                //
                // End acquisition
                //
                // *** NOTES ***
                // Ending acquisition appropriately helps ensure that devices
                // clean up properly and do not need to be power-cycled to
                // maintain integrity.
                //
                cam.EndAcquisition();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
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

        // This function acts as the body of the example; please see
        // NodeMapInfo_CSharp example for more in-depth comments on setting up
        // cameras.
        int RunSingleCamera(IManagedCamera cam)
        {
            int result = 0;

            try
            {
                // Retrieve TL device nodemap and print device information
                INodeMap nodeMapTLDevice = cam.GetTLDeviceNodeMap();

                result = PrintDeviceInfo(nodeMapTLDevice);

                // Initialize camera
                cam.Init();

                // Retrieve GenICam nodemap
                INodeMap nodeMap = cam.GetNodeMap();

                // Acquire images
                result = result | AcquireImages(cam, nodeMap, nodeMapTLDevice);

                // Deinitialize camera
                cam.DeInit();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }

        private void Button2_Click(object sender, EventArgs e)
        {

            // Example entry point; please see Enumeration_CSharp example for more
            // in-depth comments on preparing and cleaning up the system.
            //static int Main(string[] args)
            {
                int result = 0;

                //Program program = new Program();

                // Since this application saves images in the current folder
                // we must ensure that we have permission to write to this folder.
                // If we do not have permission, fail right away.
                FileStream fileStream;
                try
                {
                    fileStream = new FileStream(@"test.txt", FileMode.Create);
                    fileStream.Close();
                    File.Delete("test.txt");
                }
                catch
                {
                    Console.WriteLine("Failed to create file in current folder. Please check permissions.");
                    Console.WriteLine("Press enter to exit...");
                    Console.ReadLine();
                    return;// -1;
                }

                // Retrieve singleton reference to system object
                ManagedSystem system = new ManagedSystem();

                // Print out current library version
                LibraryVersion spinVersion = system.GetLibraryVersion();
                Console.WriteLine(
                    "Spinnaker library version: {0}.{1}.{2}.{3}\n\n",
                    spinVersion.major,
                    spinVersion.minor,
                    spinVersion.type,
                    spinVersion.build);

                // Retrieve list of cameras from the system
                ManagedCameraList camList = system.GetCameras();

                Console.WriteLine("Number of cameras detected: {0}\n\n", camList.Count);

                // Finish if there are no cameras
                if (camList.Count == 0)
                {
                    // Clear camera list before releasing system
                    camList.Clear();

                    // Release system
                    system.Dispose();

                    Console.WriteLine("Not enough cameras!");
                    Console.WriteLine("Done! Press Enter to exit...");
                    Console.ReadLine();

                    return;// -1;
                }

                //
                // Run example on each camera
                //
                // *** NOTES ***
                // Cameras can either be retrieved as their own IManagedCamera
                // objects or from camera lists using the [] operator and an index.
                //
                // Using-statements help ensure that cameras are disposed of when
                // they are no longer needed; otherwise, cameras can be disposed of
                // manually by calling Dispose(). In C#, if cameras are not disposed
                // of before the system is released, the system will do so
                // automatically.
                //
                int index = 0;

                foreach (IManagedCamera managedCamera in camList) using (managedCamera)
                    {
                        Console.WriteLine("Running example for camera {0}...", index);

                        try
                        {
                            // Run example
                            result = result | RunSingleCamera(managedCamera);
                        }
                        catch (SpinnakerException ex)
                        {
                            Console.WriteLine("Error: {0}", ex.Message);
                            result = -1;
                        }

                        Console.WriteLine("Camera {0} example complete...\n", index++);
                    }

                // Clear camera list before releasing system
                camList.Clear();

                // Release system
                system.Dispose();

                Console.WriteLine("\nDone! Press Enter to exit...");
                Console.ReadLine();

                return; // result;
            }
        } 
    

        private void Button3_Click(object sender, EventArgs e)
        {
            // 画像の読み込み
            using (Mat mat = new Mat(@"C:\Users\root\Pictures\MT3Color_aliment.jpg"))
            // 画像の切り抜き
            using (Mat mat2 = mat.Clone(new Rect(934, 138, 200, 150)))
            // 検索対象の画像とテンプレート画像
            //using (Mat mat = new Mat(@"D:\cs_source\img\neko.jpg"))
            using (Mat temp = new Mat(@"output.jpg"))
            using (Mat result = new Mat())
            {
                Cv2.ImWrite(@"output.jpg", mat2);
                Cv2.ImShow("grayscale_show", mat2);

                // テンプレートマッチ
                Cv2.MatchTemplate(mat, temp, result, TemplateMatchModes.CCoeffNormed);

                // 類似度が最大/最小となる画素の位置を調べる
                OpenCvSharp.Point minloc, maxloc;
                double minval, maxval;
                Cv2.MinMaxLoc(result, out minval, out maxval, out minloc, out maxloc);

                // しきい値で判断
                var threshold = 0.9;
                if (maxval >= threshold)
                {

                    // 最も見つかった場所に赤枠を表示
                    Rect rect = new Rect(maxloc.X, maxloc.Y, temp.Width, temp.Height);
                    Cv2.Rectangle(mat, rect, new OpenCvSharp.Scalar(0, 0, 255), 2);

                    // ウィンドウに画像を表示
                    Cv2.ImShow("template1_show", mat);

                }
                else
                {
                    // 見つからない
                    MessageBox.Show("見つかりませんでした");
                }

            }


        }

        private void Button4_Click(object sender, EventArgs e)
        {
            //PointGreyCamera pg = new PointGreyCamera();
            string[] weekDays = new string[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
                        
            PointGreyCamera.initPGRcamera(weekDays);
        }
    }
}
