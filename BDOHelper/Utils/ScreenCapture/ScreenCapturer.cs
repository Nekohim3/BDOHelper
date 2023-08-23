using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using System.Threading;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.DXGI.Resource;
#pragma warning disable CS8625

namespace BDOHelper.Utils.ScreenCapture
{

    public static class ScreenCapturer
    {

        private enum Status
        {
            Starts = 1,
            Active = 2,
            Stops = 3,
            Inactive = 4,
        }

        private static Exception GlobalException { get; set; }
        private static AutoResetEvent WaitHandle { get; set; }
        private static ConcurrentQueue<Bitmap> BitmapQueue { get; set; }
        private static Thread CaptureThread { get; set; }
        private static Thread CallbackThread { get; set; }

        private static volatile Status _status;

        public static bool SkipFirstFrame { get; set; }
        public static bool SkipFrames { get; set; }
        public static bool PreserveBitmap { get; set; }

        public static event EventHandler<OnScreenUpdatedEventArgs> OnScreenUpdated;
        public static event EventHandler<OnCaptureStopEventArgs> OnCaptureStop;

        public static bool IsActive => _status != Status.Inactive;
        public static bool IsNotActive => _status == Status.Inactive;

        public static bool StaticDelay { get; set; }
        public static int  Delay       { get; set; }

        private static DateTime _lastTime = DateTime.MinValue;
        static ScreenCapturer()
        {
            GlobalException = null;
            WaitHandle = null;
            BitmapQueue = null;
            CaptureThread = null;
            CallbackThread = null;
            _status = Status.Inactive;
            SkipFirstFrame = true;
            SkipFrames = true;
            PreserveBitmap = false;
        }

        public static void StartCapture(int delay = 0, bool staticDelay = false, int displayIndex = 0, int adapterIndex = 0)
        {
            StartCapture(null, delay, staticDelay, displayIndex, adapterIndex);
        }

        public static void StartCapture(Action<Bitmap> onScreenUpdated, int delay = 0, bool staticDelay = false, int displayIndex = 0, int adapterIndex = 0)
        {
            if (_status == Status.Inactive)
            {
                Delay                  = delay;
                StaticDelay            = staticDelay;
                WaitHandle             = new AutoResetEvent(false);
                BitmapQueue            = new ConcurrentQueue<Bitmap>();
                CaptureThread          = new Thread(() => CaptureMain(adapterIndex, displayIndex));
                CallbackThread         = new Thread(() => CallbackMain(onScreenUpdated));
                _status                = Status.Starts;
                CaptureThread.Priority = ThreadPriority.Highest;
                CaptureThread.Start();
                CallbackThread.Start();
            }
        }

        public static void StopCapture()
        {
            if (_status == Status.Active)
            {
                _status = Status.Stops;
            }
        }

        private static void CaptureMain(int adapterIndex, int displayIndex)
        {
            Resource screenResource = null;
            try
            {
                using var factory1 = new Factory1();
                using var adapter1 = factory1.GetAdapter1(adapterIndex);
                using var device   = new Device(adapter1);
                using var output   = adapter1.GetOutput(displayIndex);
                using var output1  = output.QueryInterface<Output1>();
                var       width    = output1.Description.DesktopBounds.Right  - output1.Description.DesktopBounds.Left;
                var       height   = output1.Description.DesktopBounds.Bottom - output1.Description.DesktopBounds.Top;
                var       bounds   = new Rectangle(Point.Empty, new Size(width, height));
                var texture2DDescription = new Texture2DDescription
                                           {
                                               CpuAccessFlags    = CpuAccessFlags.Read,
                                               BindFlags         = BindFlags.None,
                                               Format            = Format.B8G8R8A8_UNorm,
                                               Width             = width,
                                               Height            = height,
                                               OptionFlags       = ResourceOptionFlags.None,
                                               MipLevels         = 1,
                                               ArraySize         = 1,
                                               SampleDescription = { Count = 1, Quality = 0 },
                                               Usage             = ResourceUsage.Staging
                                           };
                using var texture2D         = new Texture2D(device, texture2DDescription);
                using var outputDuplication = output1.DuplicateOutput(device);
                _status = Status.Active;
                var frameNumber = 0;
                do
                {
                    //var startDateTime = DateTime.Now;
                    try
                    {
                        var result = outputDuplication.TryAcquireNextFrame(100, out _, out screenResource);
                        if (result.Success)
                        {
                            frameNumber += 1;
                            using var screenTexture2D = screenResource.QueryInterface<Texture2D>();
                            device.ImmediateContext.CopyResource(screenTexture2D, texture2D);
                            var dataBox           = device.ImmediateContext.MapSubresource(texture2D, 0, MapMode.Read, MapFlags.None);
                            var bitmap            = new Bitmap(width, height, PixelFormat.Format32bppRgb);
                            var bitmapData        = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                            var dataBoxPointer    = dataBox.DataPointer;
                            var bitmapDataPointer = bitmapData.Scan0;
                            for (var y = 0; y < height; y++)
                            {
                                Utilities.CopyMemory(bitmapDataPointer, dataBoxPointer, width * 4);
                                dataBoxPointer    = IntPtr.Add(dataBoxPointer,    dataBox.RowPitch);
                                bitmapDataPointer = IntPtr.Add(bitmapDataPointer, bitmapData.Stride);
                            }
                            bitmap.UnlockBits(bitmapData);
                            device.ImmediateContext.UnmapSubresource(texture2D, 0);
                            while (SkipFrames && BitmapQueue.Count > 1)
                            {
                                BitmapQueue.TryDequeue(out var dequeuedBitmap);
                                dequeuedBitmap.Dispose();
                            }
                            if (frameNumber > 1 || SkipFirstFrame == false)
                            {
                                BitmapQueue.Enqueue(bitmap);
                                WaitHandle.Set();
                            }
                        }
                        else
                        {
                            if (ResultDescriptor.Find(result).ApiCode != Result.WaitTimeout.ApiCode)
                            {
                                result.CheckError();
                            }
                        }
                    }
                    finally
                    {
                        screenResource?.Dispose();
                        try
                        {
                            outputDuplication.ReleaseFrame();
                        }
                        catch (Exception e)
                        {

                        }
                    }

                    var dt = DateTime.Now;
                    if (Delay > 0)
                    {
                        if (StaticDelay)
                            Thread.Sleep(Delay);
                        else
                        {
                            if (_lastTime != DateTime.MinValue)
                            {
                                var diff = (int)(dt - _lastTime).TotalMilliseconds;
                                if (Delay - 12 > diff)
                                    Thread.Sleep(Delay - diff - 12);
                            }
                        }
                    }
                    _lastTime = DateTime.Now;
                } while (_status == Status.Active);
            }
            catch (Exception exception)
            {
                GlobalException = exception;
                _status = Status.Stops;
            }
            finally
            {
                CallbackThread.Join();
                var exception = GlobalException;
                while (BitmapQueue.Count > 0)
                {
                    BitmapQueue.TryDequeue(out var dequeuedBitmap);
                    dequeuedBitmap?.Dispose();
                }
                GlobalException = null;
                WaitHandle = null;
                BitmapQueue = null;
                CaptureThread = null;
                CallbackThread = null;
                _status = Status.Inactive;
                if (OnCaptureStop != null)
                    OnCaptureStop(null, new OnCaptureStopEventArgs(exception != null ? new Exception(exception.Message, exception) : null));
                else
                    if (exception != null)
                        ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        private static void CallbackMain(Action<Bitmap> onScreenUpdated)
        {
            try
            {
                while (_status <= Status.Active)
                {
                    while (WaitHandle.WaitOne(10) && BitmapQueue.TryDequeue(out var bitmap))
                    {
                        try
                        {
                            onScreenUpdated?.Invoke(bitmap);
                            OnScreenUpdated?.Invoke(null, new OnScreenUpdatedEventArgs(bitmap));
                        }
                        finally
                        {
                            if (!PreserveBitmap)
                            {
                                bitmap.Dispose();
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                GlobalException = exception;
                _status = Status.Stops;
            }
        }

    }

    public class OnCaptureStopEventArgs : EventArgs
    {

        public Exception Exception { get; set; }

        internal OnCaptureStopEventArgs(Exception exception)
        {
            this.Exception = exception;
        }

    }

    public class OnScreenUpdatedEventArgs : EventArgs
    {

        public Bitmap Bitmap { get; set; }

        internal OnScreenUpdatedEventArgs(Bitmap bitmap)
        {
            this.Bitmap = bitmap;
        }

    }

}