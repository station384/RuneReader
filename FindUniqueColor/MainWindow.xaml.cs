using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Documents.Serialization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FindUniqueColor
{




    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {



        public class ThreadSafeFileBuffer<T> : IDisposable
        {
            private readonly StreamWriter m_writer;
            private readonly ConcurrentQueue<T> m_buffer = new ConcurrentQueue<T>();
            private readonly Timer m_timer;

            public ThreadSafeFileBuffer(string filePath, int flushPeriodInSeconds = 5)
            {
                m_writer = new StreamWriter(filePath, true);
                var flushPeriod = TimeSpan.FromSeconds(flushPeriodInSeconds);
                m_timer = new Timer(FlushBuffer, null, flushPeriod, flushPeriod);
            }

            public void AddResult(T result)
            {

                m_buffer.Enqueue(result);
                Console.WriteLine("Buffer is up to {0} elements", m_buffer.Count);
            }

            public void Dispose()
            {
                Console.WriteLine("Turning off timer");
                m_timer.Dispose();
                Console.WriteLine("Flushing final buffer output");
                FlushBuffer(); // flush anything left over in the buffer
                Console.WriteLine("Closing file");
                m_writer.Dispose();
            }

            /// <summary>
            /// Since this is only done by one thread at a time (almost always the background flush thread, but one time via Dispose), no need to lock
            /// </summary>
            /// <param name="unused"></param>
            private void FlushBuffer(object unused = null)
            {
                T current;
                while (m_buffer.TryDequeue(out current))
                {
                    Console.WriteLine("Buffer is down to {0} elements", m_buffer.Count);
                    m_writer.WriteLine(current);
                }
                m_writer.Flush();
            }
        }



        public List<string> UnusedColorsString { get; set; } = new List<string>() {


            "| R | G | B | HEX     |",
            "|---|---|---|---------|",
        };
        class Vec3bComparer : IEqualityComparer<Vec3b>
        {
            public bool Equals(Vec3b x, Vec3b y)
            {
                return x.Item0 == y.Item0 && x.Item1 == y.Item1 && x.Item2 == y.Item2;
            }

            public int GetHashCode(Vec3b obj)
            {
                return HashCode.Combine(obj.Item0, obj.Item1, obj.Item2);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            // listBox.ItemsSource = UnusedColorsString;

        }
        private struct valuePair
        {
            public int Count = 0;
            public Vec3b HSV = new Vec3b();
            public Vec3b BGR =  new Vec3b();
            public valuePair()
            {

            }
        }
        private void button_Click(object sender, RoutedEventArgs e)
        {
           
            var files = Directory.GetFiles(textBox.Text);

            Dictionary<Vec3b, valuePair> colorFrequencies = new Dictionary<Vec3b, valuePair>(new Vec3bComparer());
            Vec3b hsvColor;
           Mat histMat;

            if (File.Exists(@".\testimg.png"))
            {
                histMat = Cv2.ImRead(@".\testimg.png", ImreadModes.Color);
                using var Mat3 = new Mat();
                Cv2.CvtColor(histMat, Mat3, ColorConversionCodes.BGR2HSV_FULL);
                for (int y = 0; y < histMat.Rows; y++)
                {
                    for (int x = 0; x < histMat.Cols; x++)
                    {
                        Vec3b color = histMat.At<Vec3b>(y, x);
                        if (colorFrequencies.ContainsKey(color))
                        {
                            var v = colorFrequencies[color];
                            v.Count++;
                        }
                        else
                        {
                            colorFrequencies[color] = new valuePair() { Count = 1, HSV = Mat3.At<Vec3b>(y, x), BGR = color };
                        }
                    }
                }
            }
            else
            {
               
                hsvColor = new Vec3b(255, 255, 255);
                colorFrequencies[hsvColor] = new valuePair() { Count = 1,  BGR = hsvColor };
                
                hsvColor = new Vec3b(0, 0, 0);
                colorFrequencies[hsvColor] = new valuePair() { Count = 1, BGR = hsvColor };
                foreach (var file in files)
                {
                    using var Mat1 = Cv2.ImRead(file, ImreadModes.Color);
                    // Cv2.CvtColor(Mat1, Mat1, ColorConversionCodes.BGR2HSV);
                    if (Mat1.Height <= 10) continue;
                    using var Mat2 = Mat1[new OpenCvSharp.Rect(0, 0, Mat1.Width, (int)Math.Floor(Mat1.Height / 2.0))];


                    using Mat deNoised = new Mat();
                    Cv2.MedianBlur(Mat2, deNoised, 5);



                    using var Mat3 = new Mat();
                    Cv2.CvtColor(deNoised, Mat3, ColorConversionCodes.BGR2HSV_FULL);

                



                    for (int y = 0; y < Mat2.Rows; y++)
                    {
                        for (int x = 0; x < Mat2.Cols; x++)
                        {
                            Vec3b color = Mat2.At<Vec3b>(y, x);
                            if (colorFrequencies.ContainsKey(color))
                            {
                                var v = colorFrequencies[color] ;
                                v.Count++;
                            }
                            else
                            {
                                
                                colorFrequencies[color] = new valuePair() { Count = 1, HSV = Mat3.At<Vec3b>(y, x), BGR = color };
                            }
                        }
                    }

                    // Example: Output some colors and their frequencies


                    //Mat2.Dispose();
                    //Mat1.Dispose();

                }

                histMat = new Mat((int)Math.Ceiling(colorFrequencies.Count / 1024.0), 1024, MatType.CV_8UC3);
                int x1 = 0;
                colorFrequencies = colorFrequencies.OrderBy(x => x.Value.Count).ToDictionary().OrderBy(x => x.Value.HSV.Item0).OrderBy(x => x.Value.HSV.Item1).OrderBy(x => x.Value.HSV.Item2).ToDictionary();

                foreach (var color in colorFrequencies.Keys)
                {
                    histMat.Set(Math.Abs(x1 / 1024), x1 % 1024, color);
                    x1++;
                }

                if (!Cv2.ImWrite(@".\testimg.png", histMat))
                {
                    Console.WriteLine("Cant write file");
                }// This is totally brute force


            }




            Cv2.CvtColor(histMat, histMat, ColorConversionCodes.BGR2HSV_FULL);

            // Cv2.ImShow("testImg", histMat);


            // clear the results file
            var f1 = new FileStream("UnusedColors.txt", FileMode.Create, FileAccess.Write);
            f1.Close(); 


            using (var resultsBuffer = new ThreadSafeFileBuffer<string>(@"UnusedColors.txt"))
            {
                foreach (var color in UnusedColorsString)
                {
                    resultsBuffer.AddResult(color );
                }
            }

            List<Vec3b> unusedColors = new List<Vec3b>();

   

        


            for (int r = 255; r > 0; r--)
                {
                    //Mat rgbMat = new Mat(1, 1, MatType.CV_8UC3, Scalar.FromRgb(0, 0, 0));
                    //Mat hsvMat = new Mat();
                    for (int g = 255; g > 0; g--)
                    {

                    using (var resultsBuffer = new ThreadSafeFileBuffer<string>(@"UnusedColors.txt"))
                    {

                        var lr = r;
                        var lg = g;

                        var x = Parallel.For(0, 255, new ParallelOptions { MaxDegreeOfParallelism = 15 }, (b) => 

                        //for (int b = 0; b < 256; b++)
                       {
                           var lb = b;
                           var lvec = new Vec3b((byte)lb, (byte)lr, (byte)lg);
                           if (
                           (colorFrequencies.ContainsKey(lvec) && colorFrequencies[lvec].Count <= 100) 
                            || !colorFrequencies.ContainsKey(lvec)

                           )
                           {

                               using var rgbMat1 = new Mat(1, 1, MatType.CV_8UC3, Scalar.FromRgb(lr, lg, lb));
                               using var hsvMat1 = new Mat();
                               Cv2.CvtColor(rgbMat1, hsvMat1, ColorConversionCodes.BGR2HSV_FULL);


                               var hsvColor1 = hsvMat1.Get<Vec3b>(0, 0);
                               //    var hsvColor2 = hsvMat1.Get<Vec3b>(0, 0);
                               //     var hsvColor1 = new Vec3b((byte)lb, (byte)lg, (byte)lr);// hsvMat1.Get<Vec3b>(0, 0);
                               Vec3b hsvColorLower;
                               Vec3b hsvColorUpper;


                               var constantVarianceHL = 255 * 0.005;
                               var constantVarianceSL = 255 * 0.04;
                               var constantVarianceVL = 255 * 0.60;
                               var constantVarianceHH = 255 * 0.002;
                               var constantVarianceSH = 255 * 0.01;
                               var constantVarianceVH = 255 * (60.0 / 100.0);



                               //var hTol = hsvColor1.Item0 * 10.0 / 100;
                               //var sTol = hsvColor1.Item1 * 20.0 / 100;
                               //var vTol = hsvColor1.Item2 * 100.0 / 100;

                               byte hv1 = (byte)Math.Max(Math.Round(hsvColor1.Item0 - constantVarianceHL,0), 0.0);
                               byte sv2 = (byte)Math.Max(Math.Round(hsvColor1.Item1 - constantVarianceSL,0), 0.0);
                               byte vv3 = (byte)Math.Max(Math.Round(hsvColor1.Item2 - constantVarianceVL,0), 0.0);
                               hsvColorLower = new Vec3b(hv1,sv2,vv3);

                               hv1 = (byte)Math.Min(Math.Round(hsvColor1.Item0 + constantVarianceHH, 0), 255.0);
                               sv2 = (byte)Math.Min(Math.Round(hsvColor1.Item1 + constantVarianceSH, 0), 255.0);
                               vv3 = (byte)Math.Min(Math.Round(hsvColor1.Item2 + constantVarianceVH, 0), 255.0);
                               hsvColorUpper = new Vec3b(hv1,sv2,vv3);



                               // Adjust the HSV range based on the tolerance


                                Mat outMat = new Mat();
                               Cv2.InRange(histMat, hsvColorLower, hsvColorUpper, outMat);

                               //using Mat result = new Mat();
                               //Cv2.BitwiseAnd(histMat, histMat, result, outMat);

                               //using Mat grayWithDelays = new Mat();
                               //Cv2.CvtColor(result, grayWithDelays, ColorConversionCodes.BGR2GRAY);
                               //Cv2.Threshold(grayWithDelays, grayWithDelays, 0, 255, ThresholdTypes.Binary); //
                               var t1 = outMat.Height;
                               var t2 = outMat.Width;
                               var r = new OpenCvSharp.Rect(0, t1 - 1, t2, 1);
                               Mat lastbit = outMat[r];
                               //Cv2.ImShow("durr", grayWithDelays);

                               var er = Cv2.Sum(lastbit); 
                              // outMat.Dispose();

                               Scalar v1;
                               v1 = Cv2.Sum(outMat);
                               //result.Dispose();
                               //grayWithDelays.Dispose();    

                               if (v1.Val0 <= 50  && er.Val0 <= 50)
                               {
                                   string s1 = string.Concat(lr.ToString("X2"), lg.ToString("X2"), lb.ToString("X2"));
                                   //      UnusedColorsString.Add(string.Concat("|", r, "|", g, "|", b, "|", "`#", s1, "`", "#", s1, "|"));
                                   //sr.WriteLine(string.Concat("|", r, "|", g, "|", b, "|", "`#", s1, "`", "#", s1, "|"));
                                   resultsBuffer.AddResult(string.Concat("|", lr, "|", lg, "|", lb, "|", "#", s1, "|", v1.Val0, "|", hsvColor1.Item0, "|", hsvColor1.Item1, "|", hsvColor1.Item2, "|"));
                               }
                               lastbit.Dispose();
                               outMat.Dispose();



                           }


                       });
                    }

                    };


                };

            histMat.Dispose();


            listBox.ItemsSource = UnusedColorsString;
            //sr.Close();
            //f1.Close();
         //   File.WriteAllLines("UnusedColors.txt", UnusedColorsString);
            //var f1 = new FileStream("UnusedColors.txt",FileMode.Create);
            //StreamWriter s = new StreamWriter(f1);
            //foreach ( var item in UnusedColorsString) {
            //    s.WriteLine(item.ToString());
            //}
            //f1.Close();









        }
    }
}