using System;
using ZXing;
using OpenCvSharp;


namespace RuneReader.Classes.OpenCV;

public class OpenCvLuminanceSource : LuminanceSource
{
    private readonly byte[] _luminance;

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
        _luminance = new byte[mat.Width * mat.Height];

        // Copy pixel data from Mat to byte array
        mat.GetArray(out _luminance);
    }

    public override byte[] Matrix => _luminance;

    public override byte[] getRow(int y, byte[] row)
    {
        Array.Copy(_luminance, y * Width, row, 0, Width);
        return row;
    }
}