//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DepthBasics
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using Microsoft.Kinect;
	using Emgu.CV;
	using System.Runtime.InteropServices;
	using Emgu.CV.UI;

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		/// <summary>
		/// Active Kinect sensor
		/// </summary>
		private KinectSensor sensor;

		/// <summary>
		/// Bitmap that will hold color information
		/// </summary>
		private WriteableBitmap colorBitmap;

		/// <summary>
		/// Intermediate storage for the depth data received from the camera
		/// </summary>
		private DepthImagePixel[] depthPixels;

		/// <summary>
		/// Intermediate storage for the depth data converted to color
		/// </summary>
		private byte[] colorPixels;

		private Emgu.CV.BackgroundSubtractorKNN backSubDepth, backSubRgb;

		/// <summary>
		/// Change this to switch from depth to rgb image.
		/// </summary>
		//bool MakeDepth = true;
		bool MakeDepth = false;

		/// <summary>
		/// Initializes a new instance of the MainWindow class.
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Execute startup tasks
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void WindowLoaded(object sender, RoutedEventArgs e)
		{
			// Look through all sensors and start the first connected one.
			// This requires that a Kinect is connected at the time of app startup.
			// To make your app robust against plug/unplug, 
			// it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
			foreach (var potentialSensor in KinectSensor.KinectSensors)
			{
				if (potentialSensor.Status == KinectStatus.Connected)
				{
					this.sensor = potentialSensor;
					break;
				}
			}

			if (null != this.sensor)
			{
				if (MakeDepth)
				{
					// Turn on the depth stream to receive depth frames
					this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

					// Allocate space to put the depth pixels we'll receive
					this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

					// Allocate space to put the color pixels we'll create
					this.colorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];

					// This is the bitmap we'll display on-screen
					this.colorBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

					// Set the image we display to point to the bitmap where we'll put the image data
					this.Image.Source = this.colorBitmap;

					// Add an event handler to be called whenever there is new depth frame data
					this.sensor.DepthFrameReady += this.SensorDepthFrameReady;
				} else
				{
					// Turn on the color stream to receive color frames
					this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

					// Allocate space to put the pixels we'll receive
					this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

					// This is the bitmap we'll display on-screen
					this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

					// Set the image we display to point to the bitmap where we'll put the image data
					this.Image.Source = this.colorBitmap;

					// Add an event handler to be called whenever there is new color frame data
					this.sensor.ColorFrameReady += this.SensorColorFrameReady;
				}

				// Start the sensor!
				try
				{
					this.sensor.Start();
				}
				catch (IOException)
				{
					this.sensor = null;
				}
			}
			Emgu.CV.CvInvoke.NamedWindow("Emgu Window");
			backSubDepth = new Emgu.CV.BackgroundSubtractorKNN(20, 1000, false);
			backSubRgb = new Emgu.CV.BackgroundSubtractorKNN(20, 1000, false);
		}

		/// <summary>
		/// Execute shutdown tasks
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (null != this.sensor)
			{
				this.sensor.Stop();
			}
		}

		/// <summary>
		/// Event handler for Kinect sensor's ColorFrameReady event
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
		{
			using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
			{
				if (colorFrame != null)
				{
					// Copy the pixel data from the image to a temporary array
					colorFrame.CopyPixelDataTo(this.colorPixels);

					var kernel = new Mat(9, 9, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
					var bitmap = BitmapFromWriteableBitmap(colorBitmap);
					var inputImg = bitmap.ToImage<Emgu.CV.Structure.Bgr, byte>();
					//Emgu.CV.CvInvoke.Imshow("Emgu Window", inputImg);
					//var outputImg = new Mat(inputImg.Width, inputImg.Height, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
					// CvInvoke.Erode(inputImg, inputImg, null, new System.Drawing.Point(-1, -1), 10, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));
					
					Emgu.CV.BackgroundSubtractorExtension.Apply(backSubRgb, inputImg, inputImg, -1);
					CvInvoke.Threshold(inputImg, inputImg, 127, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
					CvInvoke.MorphologyEx(inputImg, inputImg, Emgu.CV.CvEnum.MorphOp.Close, kernel, new System.Drawing.Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));
					
					Emgu.CV.CvInvoke.Imshow("Emgu Window", inputImg);

					// Write the pixel data into our bitmap
					this.colorBitmap.WritePixels(
						new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
						this.colorPixels,
						this.colorBitmap.PixelWidth * sizeof(int),
						0);
				}
			}
		}

		/// <summary>
		/// Event handler for Kinect sensor's DepthFrameReady event
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
		{
			using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
			{
				if (depthFrame != null)
				{
					// Copy the pixel data from the image to a temporary array
					depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

					// Get the min and max reliable depth for the current frame
					int minDepth = depthFrame.MinDepth;
					int maxDepth = depthFrame.MaxDepth;

					// Convert the depth to RGB
					int colorPixelIndex = 0;
					for (int i = 0; i < this.depthPixels.Length; ++i)
					{
						// Get the depth for this pixel
						short depth = depthPixels[i].Depth;

						// To convert to a byte, we're discarding the most-significant
						// rather than least-significant bits.
						// We're preserving detail, although the intensity will "wrap."
						// Values outside the reliable depth range are mapped to 0 (black).

						// Note: Using conditionals in this loop could degrade performance.
						// Consider using a lookup table instead when writing production code.
						// See the KinectDepthViewer class used by the KinectExplorer sample
						// for a lookup table example.
						byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

						// Write out blue byte
						this.colorPixels[colorPixelIndex++] = intensity;

						// Write out green byte
						this.colorPixels[colorPixelIndex++] = intensity;

						// Write out red byte                        
						this.colorPixels[colorPixelIndex++] = intensity;

						// We're outputting BGR, the last byte in the 32 bits is unused so skip it
						// If we were outputting BGRA, we would write alpha here.
						++colorPixelIndex;
					}
					var bitmap = BitmapFromWriteableBitmap(colorBitmap);
					var inputImg = bitmap.ToImage<Emgu.CV.Structure.Bgr, byte>();
					var outputImg = new Mat(inputImg.Width, inputImg.Height, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
					//var erodeMatrix = new Mat(9, 9, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
					//CvInvoke.Threshold(inputImg, inputImg, 127, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
					CvInvoke.Erode(inputImg, inputImg, null, new System.Drawing.Point(-1, -1), 10, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));
					//var outputImg = bitmap.ToImage<Emgu.CV.Structure.Bgr, byte>();
					//outputImg.SetZero();
					//Emgu.CV.CvInvoke.Imshow("Emgu Window", inputImg);
					//var outputImgPtr = CvInvoke.cvCreateImage(new System.Drawing.Size(inputImg.Width, inputImg.Height), Emgu.CV.CvEnum.IplDepth.IplDepth_8U, inputImg.NumberOfChannels);
					//Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte> outputImg = Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte>.FromIplImagePtr(outputImgPtr);
					//Emgu.CV.CvInvoke.Imshow("Emgu Window", outputImg);
					Emgu.CV.BackgroundSubtractorExtension.Apply(backSubDepth, inputImg, outputImg, -1);
					Emgu.CV.CvInvoke.Imshow("Emgu Window", outputImg);
					//var outputBitmap = outputImg.AsBitmap<Emgu.CV.Structure.Bgr, byte>();
					//var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(outputBitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					//var writeableBitmap = new WriteableBitmap(bitmapSource);
					//colorBitmap = writeableBitmap;
					// Write the pixel data into our bitmap
					this.colorBitmap.WritePixels(
						new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
						this.colorPixels,
						this.colorBitmap.PixelWidth * sizeof(int),
						0);
				}
			}
		}

		private System.Drawing.Bitmap BitmapFromWriteableBitmap(WriteableBitmap writeBmp)
		{
			System.Drawing.Bitmap bmp;
			using (MemoryStream outStream = new MemoryStream())
			{
				BitmapEncoder enc = new BmpBitmapEncoder();
				enc.Frames.Add(BitmapFrame.Create((BitmapSource)writeBmp));
				enc.Save(outStream);
				bmp = new System.Drawing.Bitmap(outStream);
			}
			return bmp;
		}
	}
}