using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZXing;
using OpenCvSharp;


namespace RuneReader
{

    public class OpenCvLuminanceSource : LuminanceSource
    {
        private readonly byte[] luminances;

        public OpenCvLuminanceSource(Mat mat) : base(mat.Width, mat.Height)
        {
            // Ensure the mat is single-channel grayscale
            if (mat.Channels() >= 3)
            {
                // Convert the image to grayscale if it is in color
                Mat grayMat = new Mat();
                Cv2.CvtColor(mat, grayMat, ColorConversionCodes.BGR2GRAY);
                mat = grayMat;
            }

            // Prepare the byte array to store luminance data
            luminances = new byte[mat.Width * mat.Height];

            // Copy pixel data from Mat to byte array
            mat.GetArray(out luminances);
        }

        public override byte[] Matrix => luminances;

        public override byte[] getRow(int y, byte[] row)
        {
            Array.Copy(luminances, y * Width, row, 0, Width);
            return row;
        }
    }
}
