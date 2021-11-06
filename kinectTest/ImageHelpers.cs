using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.ComponentModel;
using System.Runtime.InteropServices;

using Emgu.CV;

namespace kinectTest
{
    public static class ImageHelpers
    {
        // 【深度指距离摄像机的距离，越远深度就越大】
        //最大深度，大于此深度摄像头掌握不到 
        private const int MaxDepthDistance = 4000;
        //最小深度，小于此深度摄像头检测不到
        private const int MinDepthDistance = 850;
        //最大深度偏移量 
        private const int MaxDepthDistanceOffset = 8000 / 56;
        //定义初始化背景的次数
        private const int processingBackgroundTimes = 60;
        //计算初始化背景的次数
        private static int nPprocessingBackgroundTimes = 0;
        //存储背景深度的数组
        private static double[] backgroundDepths;
        private static double[,] backgroundDepthsVariation;

        //private static Boolean flag = true;
        private static double[] threadhold;
        //private const int minDepth = 10;
        //private const int maxDepth = 3000;

        // PlayerIndexBitmaskWith的值为3，定义了获取深度数据值需要向右移动的位数（前三位为游戏开发者索引）
        private const int PLAYERINDEXBITMASKWIDTH = 3;

        /// <summary>
        /// 初始化背景时，将此深度帧的像素数据总长度赋值到背景深度数组的总长度中。
        /// </summary>
        /// <param name="image"></param>
        public static void InitializeBackground(this DepthFrame image)
        {
            backgroundDepths = new double[image.FrameDescription.LengthInPixels];
            // backgroundDepthsVariation[i,j]中,i是像素的长度，j是第j次背景的数据
            backgroundDepthsVariation = new double[image.FrameDescription.LengthInPixels, processingBackgroundTimes];
            threadhold = new double[image.FrameDescription.LengthInPixels];
        }

        /// <summary>
        /// 准备背景，将设定次数内的平均深度作为背景深度
        /// 把滑块选中的最小值和最大值之间的depth选出
        /// </summary>
        /// <param name="image"></param>
        /// <param name="min">滑块设定的最小深度值</param>
        /// <param name="max">滑块设定的最大深度值</param>
        /// <returns></returns>
        public static void PrepareBackgroundDepth(this DepthFrame image, int min = 20, int max = 1000)
        {
            //int width = image.Width;
            //int height = image.Height;
            //深度帧数据长度
            uint length = image.FrameDescription.LengthInPixels;

            //var depthFrame = image.Image.Bits;
            //将深度帧中数据转化为数组并复制到rawDepthData数组中
            ushort[] rawDepthData = new ushort[length];
            //image.CopyPixelDataTo(rawDepthData);
            image.CopyFrameDataToArray(rawDepthData);

            //定义像素为长乘宽乘4
            //var pixels = new byte[height * width * 4];

            ////蓝色、绿色、红色（为了还原RGB图像）  常见的编排顺序 BGR
            //const int BlueIndex = 0;
            //const int GreenIndex = 1;
            //const int RedIndex = 2;
            //循环深度索引和颜色索引
            for (int depthIndex = 0; depthIndex < length; depthIndex++)
            {

                //计算由两个深度字节表示的距离
                // PlayerIndexBitmaskWith的值为3，定义了获取深度数据值需要向右移动的位数
                int depth = rawDepthData[depthIndex] >> PLAYERINDEXBITMASKWIDTH;
                //if (depth <= 10)
                //{
                //    for(int searchIndex = depthIndex;searchIndex < image.PixelDataLength; searchIndex++)
                //    {
                //        if(searchIndex == image.PixelDataLength - 1)
                //        {
                //            searchIndex = 0;
                //        }
                //        else
                //        {
                //            searchIndex++;
                //        }
                //        if (rawDepthData[searchIndex] > 10)
                //        {
                //            depth = rawDepthData[searchIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                //            break;
                //        }

                //    }
                //}

                backgroundDepthsVariation[depthIndex, nPprocessingBackgroundTimes] = depth; // 深度下标和处理次数
                backgroundDepths[depthIndex] += depth;  // 深度和数组

            }
            nPprocessingBackgroundTimes++;

        }

        /// <summary>
        /// 平均每次我们进行处理的背景深度 【这里也可以进行优化，我觉得可以适当比平均值小一点，以降低噪声，比如平均值-5这样子】
        /// </summary>
        /// <param name="image"></param>
        /// <param name="processingTimes"></param>
        public static void averageBackground(this DepthFrame image, int processingTimes)
        {
            //循环深度索引
            for (int depthIndex = 0; depthIndex < image.FrameDescription.LengthInPixels; depthIndex++)
            {
                //取平均
                backgroundDepths[depthIndex] = backgroundDepths[depthIndex] / processingTimes;
            }

            for (int depthIndex = 0; depthIndex < image.FrameDescription.LengthInPixels; depthIndex++)
            {
                for (int backgroundIndex = 0; backgroundIndex < processingBackgroundTimes; backgroundIndex++)
                {
                    threadhold[depthIndex] += Math.Pow(backgroundDepthsVariation[depthIndex, backgroundIndex] - backgroundDepths[depthIndex], 2);
                }
                // 计算深度数据关于处理次数的方差
                threadhold[depthIndex] /= processingBackgroundTimes;
            }
            //清空该数组以便接下来可能的重新初始化背景操作
            nPprocessingBackgroundTimes = 0;
            Array.Clear(backgroundDepthsVariation, 0, 60);

        }

        /// <summary>
        /// 把滑块选中的最小值和最大值之间的depth选出
        /// </summary>
        /// <param name="image">传入的深度帧</param>
        /// <param name="minDepth">最小深度</param>
        /// <param name="maxDepth">最大深度</param>
        /// <returns></returns>
        public static BitmapSource SliceDepthImage(this DepthFrame image, int minDepth, int maxDepth)
        {
            int width = image.FrameDescription.Width;
            int height = image.FrameDescription.Height;

            //var depthFrame = image.Image.Bits;
            //将深度帧中数据转化为数组并复制到rawDepthData数组中
            ushort[] rawDepthData = new ushort[image.FrameDescription.LengthInPixels];
            image.CopyFrameDataToArray(rawDepthData);
            //if (flag)
            //{
            //    using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\Users\gmf\Desktop\testDepth.txt", true))
            //    {
            //        for (int depthIndex = 0; depthIndex < image.PixelDataLength; depthIndex++)
            //        {
            //            file.Write((rawDepthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth) + " ");
            //        }
            //        file.WriteLine();
            //    }
            //    flag = false;
            //}
            //定义像素为长乘宽乘4，除了BGR之外，还有一个alpha通道
            var pixels = new byte[height * width * 4];

            //蓝色、绿色、红色（为了还原RGB图像）  BGR编排
            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;
            //循环深度索引和颜色索引
            for (int depthIndex = 0, colorIndex = 0;
                depthIndex < rawDepthData.Length && colorIndex < pixels.Length;
                depthIndex++, colorIndex += 4)
            {

                //计算由两个深度字节表示的距离
                int depth = rawDepthData[depthIndex] >> PLAYERINDEXBITMASKWIDTH;
                //double foreground = depth - backgroundDepths[depthIndex];

                //将距离映射到可以用RGB表示的强度
                double foreground = backgroundDepths[depthIndex] - depth;


                //// 以0.005的学习率更新当前像素的背景深度，背景不能一成不变
                if (foreground < threadhold[depthIndex] && foreground > -threadhold[depthIndex])
                {
                    backgroundDepths[depthIndex] -= (foreground * 0.005);
                }

                var intensity = CalculateIntensityFromDistance((int)foreground);
                //*****判断是否大于最小深度小于最大深度*****
                if (foreground > threadhold[depthIndex] && foreground > minDepth && foreground < maxDepth)
                {
                    // 更新rawDepthData数组，将背景色换为前景色
                    rawDepthData[depthIndex] = (ushort)foreground;

                    //对颜色通道应用强度
                    pixels[colorIndex + BlueIndex] = intensity;
                    pixels[colorIndex + GreenIndex] = intensity;
                    pixels[colorIndex + RedIndex] = intensity;
                }
                else
                {
                    rawDepthData[depthIndex] = 0;
                }
            }
            //返回一个位图
            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4);
        }

        /// <summary>
        /// 这里是算背景的，和下一个函数的不同在于我把这里的newMax设为distance（实际距离） - MinDepthDistance
        /// 通过距离计算强度，这将把距离值映射到0 - 255，以便将结果应用到RGB像素。该方法用于背景初始化中
        /// </summary>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static byte BCalculateIntensityFromDistance(int distance)
        {
            int newMax = distance - MinDepthDistance;
            if (newMax > 0)
                return (byte)(255 - (255 * newMax / (MaxDepthDistanceOffset)));
            else
                return (byte)255;
        }

        /// <summary>
        /// 该方法将把距离值映射到0 - 255范围，以便将结果应用到RGB像素。该方法用于后续实时处理过程中
        /// 这里是算普通图像的，和上一个函数的不同在于我把这里的newMax设为distance（实际距离 - 对应像素背景距离）
        /// 计算得的RGB值较上一个函数较大
        /// </summary>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static byte CalculateIntensityFromDistance(int distance)
        {
            int newMax = distance;

            if (newMax > 0)
                return (byte)(255 - (255 * newMax / MaxDepthDistanceOffset));
            else
                return (byte)255;
        }

        /// <summary>
        /// 将System.Media.BitmapImage转化为System.Drawing.Bitmap
        /// </summary>
        /// <param name="bitmapsource"></param>
        /// <returns></returns>
        public static System.Drawing.Bitmap ToBitmap(this BitmapSource bitmapsource)
        {
            System.Drawing.Bitmap bitmap;
            using (var outStream = new MemoryStream())
            {
                //将System.Media.BitmapImage转化为System.Drawing.Bitmap
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
                //返还位图
                return bitmap;
            }
        }

        /// <summary>
        /// 将Image转换为WPF位图源。结果可以在Image.Source的Set属性中使用
        /// </summary>
        /// <param name="image">Emgu CV图片</param>
        /// <returns>返回的结果为位图资源（BitmapSourse）</returns>
        public static BitmapSource ToBitmapSource(IImage image)
        {
            using (System.Drawing.Bitmap source = image.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); //获得Hbitmap

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); //释放HBitmap
                return bs;
            }
        }

        /// <summary>
        /// 释放位图方法
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);
    }
}
