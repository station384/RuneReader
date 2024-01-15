using System;
using System.Diagnostics.Tracing;
using System.Drawing; // Include reference to System.Drawing
using System.Windows;
using System.Windows.Controls;
using Tesseract;


namespace HekiliEcho
{
    public class OcrResult
    {
        public System.Windows.Rect[] Regions { get; set; }
        public String DetectedText { get; set; }

        public OcrResult() { 
            Regions = new System.Windows.Rect[0];
            DetectedText = String.Empty;
        }
    }


    public class OcrModule
    {
        private TesseractEngine _tesseractEngine;
        public  OcrModule()
        {
            
            try
            {
                _tesseractEngine = new TesseractEngine(@"./tessdata", "en3", EngineMode.TesseractOnly,@"./tessdata/config.cfg");

                _tesseractEngine.SetVariable("tessedit_char_whitelist", "0123456789FCA");
                //_tesseractEngine.SetVariable("segment_penalty_dict_case_bad", "1.3125");
                //_tesseractEngine.SetVariable("segment_penalty_dict_case_ok", "1.1");
                //_tesseractEngine.SetVariable("segment_penalty_dict_nonword", "10.25");
                _tesseractEngine.SetVariable("user_words_suffix", "user-words");
                //_tesseractEngine.SetVariable("tessedit_pageseg_mode", "10");
                //_tesseractEngine.SetVariable("language_model_penalty_non_freq_dict_word", "1");
                //_tesseractEngine.SetVariable("language_model_penalty_non_dict_word", "1");


                //_tesseractEngine.SetVariable("tessedit_pageseg_mode", 6);




            }
            catch (Exception ex)
            {
             //   throw (ex);                         
            }
 
        }
        ~OcrModule()
        {
            _tesseractEngine.Dispose(); 
        }



        public System.Windows.Rect[] GetRegions(Tesseract.Page page)
        {
            System.Windows.Rect[] result = null;

                var layout = page.GetSegmentedRegions(PageIteratorLevel.Symbol);
                if (layout != null)
                {
                    var retrangles = layout.ToArray();
                    result = new System.Windows.Rect[retrangles.Length];
                    for (var x = 0; x < retrangles.Length; x++)
                    {
                        if (!layout[x].IsEmpty)
                        {
                            result[x].Height = layout[x].Height;
                            result[x].Width = layout[x].Width;
                            result[x].X = layout[x].X;
                            result[x].Y = layout[x].Y;
                        }

                    }

                }

            return result;
        }


        public System.Windows.Rect[] GetRegions(Bitmap bitmap)
        {
            System.Windows.Rect[] result = null;
            // Ensure the bitmap is in the correct format (24bpp RGB for Tesseract)
            Bitmap ocrBitmap = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            // Rect Area = new Rect(Region.X, Region.Y, Region.Width, Region.Height);
            // Initialize the OCR engine with the path to the tessdata and the language.
            // Process the image with OCR.
            using (var page = _tesseractEngine.Process(ocrBitmap, PageSegMode.SingleLine))
            {
                result = GetRegions(page);

            }
            return result;
        }

        public string PerformPointOcr(Tesseract.Page page)
        {
            string ocrResult = string.Empty;

            ocrResult = page.GetText();

            return ocrResult;

        }

        public string PerformPointOcr(Bitmap bitmap)
        {
            string ocrResult = string.Empty;
            // Ensure the bitmap is in the correct format (24bpp RGB for Tesseract)
            Bitmap ocrBitmap = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            // Rect Area = new Rect(Region.X, Region.Y, Region.Width, Region.Height);
            // Initialize the OCR engine with the path to the tessdata and the language.
            // Process the image with OCR.


            using (Tesseract.Page page = _tesseractEngine.Process(ocrBitmap, PageSegMode.SingleLine))
            {
                // Return the recognized text.
                ocrResult = PerformPointOcr(page);
            }
            return ocrResult;
        }

        public string PerformPointOcr(Bitmap bitmap, System.Windows.Rect Region)
        {
            string ocrResult = string.Empty;
            // Ensure the bitmap is in the correct format (24bpp RGB for Tesseract)
            Bitmap ocrBitmap = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            // Rect Area = new Rect(Region.X, Region.Y, Region.Width, Region.Height);
            // Initialize the OCR engine with the path to the tessdata and the language.
            // Process the image with OCR.

            Tesseract.Rect rect = new Tesseract.Rect((int)Region.X,(int)Region.Y,(int)Region.Width, (int)Region.Height);



            using (Tesseract.Page page = _tesseractEngine.Process(ocrBitmap, rect, PageSegMode.SingleLine))
            {
                // Return the recognized text.
                ocrResult = PerformPointOcr(page);
            }
            return ocrResult;
        }



        public OcrResult PerformFullOcr(Bitmap bitmap)
        {
            OcrResult ocrResult = new OcrResult();
            // Ensure the bitmap is in the correct format (24bpp RGB for Tesseract)
            Bitmap ocrBitmap = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            // Rect Area = new Rect(Region.X, Region.Y, Region.Width, Region.Height);
            // Initialize the OCR engine with the path to the tessdata and the language.
            // Process the image with OCR.
            

            using (Tesseract.Page page = _tesseractEngine.Process(ocrBitmap, PageSegMode.SingleLine))
            {
                // Return the recognized text.
                ocrResult.Regions = GetRegions(page);
                ocrResult.DetectedText = page.GetText();
            }
            return ocrResult;
        }
    }
}
