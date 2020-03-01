//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DepthBasics {
	using System;
	using System.IO;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using Microsoft.Kinect;
	using Emgu.CV;

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		/// <summary>
		/// Active Kinect sensor
		/// </summary>
		private KinectSensor sensor;

		/// <summary>
		/// Bitmap that will hold color information
		/// </summary>
		private WriteableBitmap colorBitmap, grayBitmap;

		/// <summary>
		/// Intermediate storage for the depth data received from the camera
		/// </summary>
		private DepthImagePixel[] depthPixels;

		/// <summary>
		/// Intermediate storage for the depth data converted to color
		/// </summary>
		private byte[] colorPixels;

		private Emgu.CV.BackgroundSubtractorKNN backSubDepth, backSubRgb;
		private Emgu.CV.BackgroundSubtractorMOG2 backSubDepth2;

		/// <summary>
		/// Change this to switch from depth to rgb image.
		/// </summary>
		bool MakeDepth = true;
		//bool MakeDepth = false;

		short[] shortArray;
		byte[] byteArray;
		Mat kernel9;

		/// <summary>
		/// Initializes a new instance of the MainWindow class.
		/// </summary>
		public MainWindow() {
			InitializeComponent();
		}

		/// <summary>
		/// Execute startup tasks
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void WindowLoaded(object sender, RoutedEventArgs e) {
			// Look through all sensors and start the first connected one.
			// This requires that a Kinect is connected at the time of app startup.
			// To make your app robust against plug/unplug, 
			// it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
			foreach (var potentialSensor in KinectSensor.KinectSensors) {
				if (potentialSensor.Status == KinectStatus.Connected) {
					this.sensor = potentialSensor;
					break;
				}
			}

			if (null != this.sensor) {
				if (MakeDepth) {
					// Turn on the depth stream to receive depth frames
					this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

					// Allocate space to put the depth pixels we'll receive
					this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

					// Allocate space to put the color pixels we'll create
					this.colorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];

					// This is the bitmap we'll display on-screen
					this.colorBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr24, null);

					//this.grayBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Gray8, null);

					// Set the image we display to point to the bitmap where we'll put the image data
					this.Image.Source = this.colorBitmap;
					//this.Image.Source = this.grayBitmap;

					// Add an event handler to be called whenever there is new depth frame data
					this.sensor.DepthFrameReady += this.SensorDepthFrameReady;

					shortArray = new short[this.sensor.DepthStream.FramePixelDataLength];
					byteArray = new byte[this.sensor.DepthStream.FramePixelDataLength];
				} else {
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
				try {
					this.sensor.Start();
				} catch (IOException) {
					this.sensor = null;
				}
			}
			// not working? see https://github.com/opencv/opencv/issues/10438
			// for workaround, setWindowProperty would be needed, but it seems not to be implemented in Emgu 4.2
			Emgu.CV.CvInvoke.NamedWindow("Emgu Window", Emgu.CV.CvEnum.NamedWindowType.Fullscreen);
			backSubDepth = new Emgu.CV.BackgroundSubtractorKNN(2000, 100, false);
			backSubDepth2 = new BackgroundSubtractorMOG2(2000, 10, false);
			backSubRgb = new Emgu.CV.BackgroundSubtractorKNN(200, 70, false);
			kernel9 = new Mat(9, 9, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
		}

		/// <summary>
		/// Execute shutdown tasks
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (null != this.sensor) {
				this.sensor.Stop();
			}
		}

		/// <summary>
		/// Event handler for Kinect sensor's ColorFrameReady event
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e) {
			using (ColorImageFrame colorFrame = e.OpenColorImageFrame()) {
				if (colorFrame != null) {
					// Copy the pixel data from the image to a temporary array
					colorFrame.CopyPixelDataTo(this.colorPixels);


					var bitmap = BitmapFromWriteableBitmap(colorBitmap);
					var inputImg = bitmap.ToImage<Emgu.CV.Structure.Bgr, byte>();
					//Emgu.CV.CvInvoke.Imshow("Emgu Window", inputImg);
					//var outputImg = new Mat(inputImg.Width, inputImg.Height, Emgu.CV.CvEnum.DepthType.Cv8U, 3);

					Emgu.CV.BackgroundSubtractorExtension.Apply(backSubRgb, inputImg, inputImg, -1);
					//CvInvoke.Threshold(inputImg, inputImg, 127, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
					CvInvoke.Erode(inputImg, inputImg, null, new System.Drawing.Point(-1, -1), 3, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));
					CvInvoke.MorphologyEx(inputImg, inputImg, Emgu.CV.CvEnum.MorphOp.Close, kernel9, new System.Drawing.Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));

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
		private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e) {
			using (DepthImageFrame depthFrame = e.OpenDepthImageFrame()) {
				if (depthFrame != null) {
					depthFrame.CopyPixelDataTo(shortArray);

					// The depth data from Kinect comes in a 2 byte (typed as a signed short) value for each pixel with the first 3 bit being skeleton info
					// (all zero when not activated) and the last 13 bit being the depth value. The next step ignores
					// the state of the 3 less significant skeleton bits and converts the whole 2 byte value to a single unsigned byte value.
					// The single byte value is then filled into a byte array from which a new Image<Emgu.CV.Structure.Gray, byte>
					// is created in the next step:
					for (var i = 0; i < shortArray.Length; i++) {
						// Division maps 2 byte value to a single byte value:
						var b = ((ushort)shortArray[i]) / 258;
						// Threshold values define the valid capture depth:
						if (b > 128 || b < 64) {
							byteArray[i] = 255; // valid depth
						} else {
							byteArray[i] = 0; // invalid depth
						}
					}

					var inputImg = new Image<Emgu.CV.Structure.Gray, byte>(colorBitmap.PixelWidth, colorBitmap.PixelHeight);
					inputImg.Bytes = byteArray;
					CvInvoke.MorphologyEx(inputImg, inputImg, Emgu.CV.CvEnum.MorphOp.Close, kernel9, new System.Drawing.Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));

					var outputImg = new Mat(inputImg.Width, inputImg.Height, Emgu.CV.CvEnum.DepthType.Cv8U, 3).ToImage<Emgu.CV.Structure.Bgr, byte>();
					CvInvoke.CvtColor(inputImg, outputImg, Emgu.CV.CvEnum.ColorConversion.Gray2Rgb);
					Emgu.CV.CvInvoke.Imshow("Emgu Window", outputImg);

					/*
					grayBitmap.WritePixels(
						new Int32Rect(0, 0, grayBitmap.PixelWidth, grayBitmap.PixelHeight)
						, inputImg.Bytes
						, grayBitmap.PixelWidth * sizeof(byte)
						, 0);
					*/
					/*
					colorBitmap.WritePixels(
						new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight)
						, outputImg.Bytes
						, colorBitmap.PixelWidth * sizeof(byte) * 3
						, 0);
					*/
					//var inputImg = bitmap.ToImage<Emgu.CV.Structure.Bgr, ushort>();

					//var outputImg = new Mat(inputImg.Width, inputImg.Height, Emgu.CV.CvEnum.DepthType.Cv8U, 3);

					//var erodeMatrix = new Mat(9, 9, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
					//CvInvoke.Threshold(inputImg, inputImg, 0, 200, Emgu.CV.CvEnum.ThresholdType.Binary);

					//outputImg.SetZero();

					// === Not working!
					// var outputImgPtr = CvInvoke.cvCreateImage(new System.Drawing.Size(inputImg.Width, inputImg.Height), Emgu.CV.CvEnum.IplDepth.IplDepth_8U, inputImg.NumberOfChannels);
					// Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte> outputImg = Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte>.FromIplImagePtr(outputImgPtr);
					// ===

					//Emgu.CV.BackgroundSubtractorExtension.Apply(backSubDepth2, inputImg, inputImg, -1);
					//CvInvoke.Erode(inputImg, inputImg, null, new System.Drawing.Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));
					//CvInvoke.MorphologyEx(inputImg, inputImg, Emgu.CV.CvEnum.MorphOp.Close, kernel9, new System.Drawing.Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));
					//Emgu.CV.CvInvoke.Imshow("Emgu Window", inputImg);

					//var outputBitmap = outputImg.AsBitmap<Emgu.CV.Structure.Bgr, byte>();
					//var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(outputBitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					//var writeableBitmap = new WriteableBitmap(bitmapSource);
					//colorBitmap = writeableBitmap;
					// Write the pixel data into our bitmap
					/*
					this.colorBitmap.WritePixels(
						new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
						this.colorPixels,
						this.colorBitmap.PixelWidth * sizeof(int),
						0);
					*/
				}
			}
		}

		private System.Drawing.Bitmap BitmapFromWriteableBitmap(WriteableBitmap writeBmp) {
			System.Drawing.Bitmap bmp;
			using (MemoryStream outStream = new MemoryStream()) {
				BitmapEncoder enc = new BmpBitmapEncoder();
				enc.Frames.Add(BitmapFrame.Create((BitmapSource)writeBmp));
				enc.Save(outStream);
				bmp = new System.Drawing.Bitmap(outStream);
			}
			return bmp;
		}
	}
}