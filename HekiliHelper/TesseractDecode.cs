using System.Drawing; // Include reference to System.Drawing
using Tesseract;


namespace HekiliHelper
{
    internal class TesseractDecode
    {
    }

    public class OcrModule
    {
        private TesseractEngine _tesseractEngine;
        public  OcrModule()
        {
            
            try
            {
                _tesseractEngine = new TesseractEngine(@"./tessdata", "en3", EngineMode.TesseractOnly,@"./tessdata/config.cfg");
                //_tesseractEngine.SetVariable("tessedit_char_whitelist", "0123456789FCA");

                //_tesseractEngine.SetVariable("language_model_penalty_non_dict_word", 0.975); //default:0.15
                //_tesseractEngine.SetVariable("language_model_penalty_non_freq_dict_word", 0.575); //0.1
                //_tesseractEngine.SetVariable("segment_penalty_dict_case_bad", 1.3125); //1.3125
                //_tesseractEngine.SetVariable("segment_penalty_dict_case_ok", 1.1); //1.1
                //_tesseractEngine.SetVariable("segment_penalty_dict_nonword", 10.25); //10.25

                //_tesseractEngine.SetVariable("tessedit_pageseg_mode", 6);
                //   result = _tesseractEngine.SetVariable("tessedit_preserve_min_wd_len", 1);
                //_tesseractEngine.SetVariable("min_sane_x_ht_pixels", 80.0);
                //_tesseractEngine.SetVariable("min_sane_x_ht_pixels", 50.0);








            }
            catch { 
            
            }
 
        }
        ~OcrModule()
        {
            _tesseractEngine.Dispose(); 
        }

            public string PerformOcr(Bitmap bitmap)//,  Rectangle Region)
        {
            // Ensure the bitmap is in the correct format (24bpp RGB for Tesseract)
            Bitmap ocrBitmap = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            // Rect Area = new Rect(Region.X, Region.Y, Region.Width, Region.Height);
            // Initialize the OCR engine with the path to the tessdata and the language.
            // Process the image with OCR.
            using (var page = _tesseractEngine.Process(ocrBitmap, PageSegMode.SingleWord))
            {
                // Return the recognized text.
                return page.GetText();
            }
        }
    }
}
