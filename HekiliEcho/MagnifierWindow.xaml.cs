using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HekiliEcho
{
    using System;
    using System.Drawing;
    using System.Windows;
    using System.Windows.Controls.Primitives;
    using System.Windows.Interop;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using static HekiliEcho.ContinuousScreenCapture;


    public partial class MagnifierWindow : Window
    {
        //    private DispatcherTimer _refreshTimer;
        private double _scaleFactor = 2.0;


        private Rect _locationValues;
        public Rect CurrrentLocationValue
        {
            get => _locationValues;
            private set => _locationValues = value;
        }
        public double ScaledX => this.Left * _scaleFactor;
        public double ScaledY => this.Top * _scaleFactor;
        public double ScaledWidth => this.ActualWidth * _scaleFactor;
        public double ScaledHeight => this.ActualHeight * _scaleFactor;

        public new double Left
        {
            get => base.Left;
            set => base.Left = value;
        }

        public new double Top
        {
            get => base.Top;
            set => base.Top = value;
        }

        public new double Width
        {
            get => base.Width;
            set
            {
                base.Width = value;
            //    MagnifyContent(); // Refresh content if the width changes.
            }
        }

        public new double Height
        {
            get => base.Height;
            set
            {
                base.Height = value;
            //    MagnifyContent(); // Refresh content if the height changes.
            }
        }



    //    public event SizeChangedDelegate SizeChanged;

        public MagnifierWindow()
            {
                InitializeComponent();
            }






        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
            {
                Thumb thumb = sender as Thumb;
                if (thumb != null)
                {
                    this.Width = Math.Max(this.ActualWidth + e.HorizontalChange, thumb.Width);
                    this.Height = Math.Max(this.ActualHeight + e.VerticalChange, thumb.Height);
                }
            }




        // PInvoke call to release the HBitmap.
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
            
        }



        private void Window_LocationChanged(object sender, EventArgs e)
        {
            var lv = new Rect();
            lv.X = this.Left;
            lv.Y = this.Top;
            lv.Width = this.ActualWidth;
            lv.Height = this.ActualHeight;
            _locationValues = lv;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var lv = new Rect();
            lv.X = this.Left;
            lv.Y = this.Top;
            lv.Width = this.ActualWidth;
            lv.Height = this.ActualHeight;
            _locationValues = lv;
        }
    }



}




