using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PaintCodeResources.Sample.WinForms
{
    public partial class Form1 : Form
    {
        //float animation = 0;

        private System.Threading.Timer timer;

        public Form1()
        {
            InitializeComponent();

            this.timer = new System.Threading.Timer(this.Animate, null, 200, 1000);
        }

        private void Animate(object state)
        {
            this.BeginInvoke((Action)(() =>
            {
                //this.animation += 0.04f;
                //if (this.animation > 1)
                //    this.animation = 0;

                this.skControl1.Invalidate();
            }));
        }

        private void skControl1_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            var surface = e.Surface;
            var surfaceWidth = e.Info.Width;
            var surfaceHeight = e.Info.Height;

            var canvas = surface.Canvas;
            canvas.Clear();
            // draw on the canvas
            var minute = (float)DateTime.Now.Minute;
            var hour = (float)DateTime.Now.Hour;
            var sec = (float)DateTime.Now.Second;
            StyleKitName.drawClock(canvas, null, new SKRect(0,0,surfaceWidth, surfaceHeight), PaintCode.ResizingBehavior.AspectFit, new SKColor(40, 40, 40), new SKColor(10, 10, 10), new SKColor(40, 190, 30), new SKColor(128, 128, 128), new SKColor(128, 128, 222), new SKColor(228, 228, 228), hour, minute, sec);
        }
    }
}
