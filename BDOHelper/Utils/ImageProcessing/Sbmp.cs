using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BDOHelper.Utils.ImageProcessing
{
    public class Sbmp : IDisposable
    {
        public byte[] Data;
        public int    Height;
        public int    Width;
        public int    Size => Width * Height;


        #region Ctor

        public Sbmp(byte[] b, int w, int h)
        {
            Data   = b;
            Width  = w;
            Height = h;
        }

        public Sbmp(Bitmap source)
        {
            if (source.PixelFormat != PixelFormat.Format32bppArgb)
                source = Convert32(source);
            Width  = source.Width;
            Height = source.Height;
            Data   = new byte[Width * Height * 4];
            var rect       = new System.Drawing.Rectangle(0, 0, Width, Height);
            var bitmapData = source.LockBits(rect, ImageLockMode.ReadWrite, source.PixelFormat);
            Marshal.Copy(bitmapData.Scan0, Data, 0, Data.Length);
            bitmapData = null;
            source.Dispose();
        }

        public Sbmp(string str)
        {
            System.Drawing.Image image;
            using (var myStream = new FileStream(str, FileMode.Open, FileAccess.Read))
                image = System.Drawing.Image.FromStream(myStream);
            var source = (Bitmap)image;
            if (source.PixelFormat != PixelFormat.Format32bppArgb)
                source = Convert32(source);
            Width  = source.Width;
            Height = source.Height;
            Data    = new byte[Width * Height * 4];
            var rect       = new System.Drawing.Rectangle(0, 0, Width, Height);
            var bitmapData = source.LockBits(rect, ImageLockMode.ReadWrite, source.PixelFormat);
            Marshal.Copy(bitmapData.Scan0, Data, 0, Data.Length);
            bitmapData = null;
            source.Dispose();
        }

        #endregion

        #region Funcs

        public void InitMask()
        {

        }

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || y < 0)
                return Color.FromArgb(0, 0, 0, 0);
            var i = (y * Width + x) * 4;
            if (i > Data.Length - 4)
                return Color.FromArgb(0, 0, 0, 0);
            var b = Data[i];
            var g = Data[i + 1];
            var r = Data[i + 2];
            var a = Data[i + 3];
            return Color.FromArgb(a, r, g, b);
        }

        public void SetPixel(int x, int y, Color color)
        {
            var i = (y * Width + x) * 4;
            Data[i]     = color.B;
            Data[i + 1] = color.G;
            Data[i + 2] = color.R;
            Data[i + 3] = color.A;
        }

        public Sbmp Copy() => new(Data.ToArray(), Width, Height);

        public Sbmp Crop(System.Drawing.Rectangle r)
        {
            var bbuf = new byte[r.Width * r.Height * 4];
            for (int i = r.X, ii = 0; i < r.X + r.Width; i++, ii++)
            {
                for (int j = r.Y, jj = 0; j < r.Y + r.Height; j++, jj++)
                {
                    var x     = (jj * r.Width + ii) * 4;
                    var color = GetPixel(i, j);
                    bbuf[x]     = color.B;
                    bbuf[x + 1] = color.G;
                    bbuf[x + 2] = color.R;
                    bbuf[x + 3] = color.A;
                }
            }
            return new Sbmp(bbuf, r.Width, r.Height);
        }

        public Bitmap GetBmp()
        {
            var bmp  = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var bd   = bmp.LockBits(new System.Drawing.Rectangle(0, 0, Width, Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
            Marshal.Copy(Data, 0, bd.Scan0, Data.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }

        public void Save(string fileName) => GetBmp().Save(fileName);

        public bool Compare(Sbmp b, Point p, int thr = 0, int npc = -1) => Compare(b, p.X, p.Y, thr, npc);

        public bool Compare(Sbmp b, int x, int y, int thr = 0, int npc = -1)
        {
            var counter = 0;
            for (var i = 0; i < b.Width; i++)
            for (var j = 0; j < b.Height; j++)
            {
                var c = GetPixel(x + i, y + j);
                var e = b.GetPixel(i, j);
                if (Math.Abs(c.R - e.R) <= thr && Math.Abs(c.G - e.G) <= thr &&
                    Math.Abs(c.B - e.B) <= thr)
                    counter++;
            }

            if (npc >= 0)
                return counter * 100 / b.Size >= npc;
            else
                return b.Size == counter;
        }

        public bool Find(Sbmp b, Point p, int offsetXm, int offsetYm, int offsetX, int offsetY, int thr = 0, int npc = -1) => Find(b, p.X, p.Y, offsetXm, offsetYm, offsetX, offsetY, thr, npc);

        public bool Find(Sbmp b, int   x, int y,        int offsetX,  int offsetY, int thr = 0, int npc = -1)              => Find(b, x,   y,   offsetX,  offsetY,  offsetX, offsetY, thr, npc);

        public bool Find(Sbmp b, Point p, int offsetX, int offsetY, int thr = 0, int npc = -1) => Find(b, p.X, p.Y, offsetX, offsetY, offsetX, offsetY, thr, npc);

        public bool Find(Sbmp b, int x, int y, int offsetXm, int offsetYm, int offsetX, int offsetY, int thr = 0, int npc = -1)
        {
            for(var i = x - offsetXm; i < x + offsetX; i++)
                for(var j = y - offsetYm; j < y + offsetY; j++)
                    if (Compare(b, x, y, thr, npc))
                        return true;
            return false;
        }

        public bool CompareToMask(Sbmp b, int x, int y, int npc = -1, bool useMonochrome = true)
        {
            if (x > Width || x < 0 || y > Height || y < 0)
                return false;
            var counter = 0;
            for (int i = x, ii = 0; i < Width && ii < b.Width; i++, ii++)
            for (int j = y, jj = 0; j < Height && jj < b.Height; j++, jj++)
            {
                var cb = GetPixel(i, j);
                var cm = b.GetPixel(ii, jj);
                switch (cm.A)
                {
                    case 0:
                        counter++;
                        break;
                    case 255 when useMonochrome:
                    {
                        if (Monochrome(cb) == Monochrome(cm))
                            counter++;
                        break;
                    }
                    case 255:
                    {
                        if (cb == cm)
                            counter++;
                        break;
                    }
                }
            }

            return npc >= 0 ? (double)counter * 100 / b.Size >= npc : b.Size == counter;
        }

        public void Monochrome(int thr = 150)
        {
            for (var i = 0; i < Width; i++)
            for (var j = 0; j < Height; j++)
                SetPixel(i, j, Monochrome(GetPixel(i, j), thr));
        }

        public Color Monochrome(Color c, int thr = 150) => (int)((c.R * 0.3f) + (c.G * 0.59f) + (c.B * 0.11f)) < thr ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(255, 255, 255, 255);


        #endregion

        #region Static funcs

        public static Bitmap Convert32(Bitmap orig)
        {
            var       clone = new Bitmap(orig.Width, orig.Height, PixelFormat.Format32bppPArgb);
            using var gr    = Graphics.FromImage(clone);
            gr.DrawImage(orig, new System.Drawing.Rectangle(0, 0, clone.Width, clone.Height));
            return clone;
        }

        #endregion

        public void Dispose()
        {
            Data = null;
        }
    }
}
