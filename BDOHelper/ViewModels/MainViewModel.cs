using System.Threading.Tasks;
using System;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Avalonia.Platform;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls.Platform;
using BDOHelper.Utils.ImageProcessing;
using BDOHelper.Utils.ScreenCapture;
using Bitmap = System.Drawing.Bitmap;
using Color = System.Drawing.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace BDOHelper.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        var lst     = new List<DateTime>();
        var counter = 0;
        var dt      = DateTime.Now;
        ScreenCapturer.StartCapture((c =>
                                                      {
                                                          ScreenCapturer.StopCapture();
                                                          var sbmp = new Sbmp(c);
                                                          sbmp.Save("F:\\test.bmp");
                                                      }));
        //var screenStateLogger = new ScreenStateLogger();
        //var q                 = false;
        //screenStateLogger.ScreenRefreshed += (sender, data) =>
        //                                     {
        //                                         var sbmp = new Sbmp(data, screenStateLogger.Width, screenStateLogger.Height);
        //                                         if (!q)
        //                                         {
        //                                             //sbmp.Save("F:\\test.bmp");
        //                                             q = true;
        //                                         }
        //                                         lst.Add(DateTime.Now);
        //                                         counter++;
        //                                         if (counter >= 100)
        //                                         {
        //                                             screenStateLogger.Stop();
        //                                             var temp = 0D;
        //                                             foreach (var x in lst)
        //                                             {
        //                                                 temp += x.Ticks / (double)counter;
        //                                             }

        //                                             var avg = new DateTime((long)temp);
        //                                         }
        //                                     };
        //screenStateLogger.Start();
    }
}

