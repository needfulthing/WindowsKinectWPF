// NOTE: THIS CODE IS FOR KINECT 360 WITH KINECT SDK 1.8 AND NUGET EMGU 4.2

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
		/// <summary>The Kinect sensor object.</summary>
		private KinectSensor Sensor;

		/// <summary>The <see cref="WriteableBitmap"/> whose data is linked to the PWF output image.</summary>
		private WriteableBitmap WpfBitmap;

		/// <summary>This array receives the depth data from the camera.</summary>
		private DepthImagePixel[] DepthPixels;

		/// <summary>This array receives the color data from the camera.</summary>
		private byte[] ColorPixels;

		/// <summary>The background substractor for the color image.</summary>
		private Emgu.CV.BackgroundSubtractorKNN BackSubRgb;

		// The depth image does not need a background substractor. Just left it here for reference:
		//private Emgu.CV.BackgroundSubtractorKNN backSubDepth;
		//private Emgu.CV.BackgroundSubtractorMOG2 backSubDepth2; // another BG substractor with worse results

		/// <summary>Change this to switch from depth to rgb image.</summary>
		bool MakeDepth = true;
		//bool MakeDepth = false;

		/// <summary>Holds the signed short raw data array directly received from the Kinect depth image.</summary>
		/// <remarks>Noone except Microsoft will ever know why the raw data is returned in a impractical signed short array :p</remarks>
		short[] ShortArray;

		/// <summary>Gets the Kinect raw data converted from a signed short array to a byte array for further processing.</summary>
		byte[] ByteArray;

		/// <summary>A kernal matrix used as a parameter in OpenCV functions.</summary>
		Mat Kernel9;

		/// <summary>If set, depth image output will be dropped.</summary>
		bool DisplayEventTriggered;
		//private readonly object TimerLock = new object();

		/// <summary>Initializes a new instance of the MainWindow class.</summary>
		public MainWindow() {
			InitializeComponent();
		}

		/// <summary>Interupts the standard Kinect depth camera image output for a timed display event.</summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		/// <remarks>Currently only one event exists.</remarks>
		public void DisplayEvent(Object source, System.Timers.ElapsedEventArgs e) {
			DisplayEventTriggered = true;
			System.Threading.Thread.Sleep(50);

			for (var i = -400; i <= 0; i += 4) {
				// Updates to the UI image (and all other UI elements) are only possible in the UI thread.
				// You can assign code from other event handlers to the UI thread by using Dispatcher.Invoke():
				Dispatcher.Invoke(() => {
					var inputImg = new Image<Emgu.CV.Structure.Gray, byte>(WpfBitmap.PixelWidth, WpfBitmap.PixelHeight);
					//inputImg.Draw(new System.Drawing.Rectangle(0, 0, WpfBitmap.PixelWidth, WpfBitmap.PixelHeight), new Emgu.CV.Structure.Gray(0), -1);
					inputImg.Draw("HAPPY", new System.Drawing.Point(100, i + 100), Emgu.CV.CvEnum.FontFace.HersheyPlain, 8, new Emgu.CV.Structure.Gray(255), 15);
					inputImg.Draw("BIRTHDAY", new System.Drawing.Point(10, i + 240), Emgu.CV.CvEnum.FontFace.HersheyPlain, 8, new Emgu.CV.Structure.Gray(255), 15);
					inputImg.Draw("POST-IT", new System.Drawing.Point(40, i + 380), Emgu.CV.CvEnum.FontFace.HersheyPlain, 8, new Emgu.CV.Structure.Gray(255), 15);
					DrawImageToScreen(TileEffect(inputImg, new Emgu.CV.Structure.Bgr(0, 255, 255)));
				});
				System.Threading.Thread.Sleep(10);
				//inputImg.Draw(new System.Drawing.Rectangle(0, 0, 100, 100), new Emgu.CV.Structure.Gray(128), -1);
				//DrawImageToScreen(TileEffect(inputImg, new Emgu.CV.Structure.Bgr(0, 255, 255)));
			}
			System.Threading.Thread.Sleep(2000);
			DisplayEventTriggered = false;
		}

		/// <summary>Create tile image aka Post-It effect.</summary>
		/// <param name="inputImg">The gray input image.</param>
		/// <returns>A Bgr output image with a tile effect drawn over all squares whose center point is not black (=0).</returns>
		internal Image<Emgu.CV.Structure.Bgr, byte> TileEffect(Image<Emgu.CV.Structure.Gray, byte> inputImg, Emgu.CV.Structure.Bgr color) {
			var outputImg = new Image<Emgu.CV.Structure.Bgr, byte>(inputImg.Width, inputImg.Height);

			// Draw 8x8 squares in a 10x10 grid:
			for (var i = 0; i < inputImg.Rows - 1; i += 10) {
				for (var j = 0; j < inputImg.Cols - 1; j += 10) {
					var pix = inputImg.Data[i + 5, j + 5, 0];
					if (pix != 0) {
						outputImg.Draw(new System.Drawing.Rectangle(j, i, 8, 8), color, -1);
					}
				}
			}
			return outputImg;
		}

		/// <summary>Runs startup code after the WPF windows has been loaded.</summary>
		/// <param name="sender">Object sending the event</param>
		/// <param name="e">Event arguments</param>
		private void WindowLoaded(object sender, RoutedEventArgs e) {
			// Create timer for the display events:
			var timer = new System.Timers.Timer(6000) {
				AutoReset = true
			};
			timer.Elapsed += DisplayEvent;
			timer.Enabled = true;

			// Look through all sensors and start the first connected one. This requires that a Kinect is connected
			// at the time of app startup. To make your app robust against plug/unplug, it is recommended to
			// use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
			foreach (var potentialSensor in KinectSensor.KinectSensors) {
				if (potentialSensor.Status == KinectStatus.Connected) {
					this.Sensor = potentialSensor;
					break;
				}
			}

			if (null != this.Sensor) {
				if (MakeDepth) {
					// Turn on the depth stream to receive depth frames:
					this.Sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

					// Allocate space to put the depth pixels we'll receive:
					this.DepthPixels = new DepthImagePixel[this.Sensor.DepthStream.FramePixelDataLength];

					// Allocate space to put the color pixels we'll create:
					this.ColorPixels = new byte[this.Sensor.DepthStream.FramePixelDataLength * sizeof(int)];

					// This is the bitmap we'll display on-screen:
					this.WpfBitmap = new WriteableBitmap(this.Sensor.DepthStream.FrameWidth, this.Sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr24, null);
					//this.colorBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Gray8, null);
					//this.grayBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Gray8, null);

					// Set the image we display to point to the bitmap where we'll put the image data
					this.Image.Source = this.WpfBitmap;
					//this.Image.Source = this.grayBitmap;

					// Add an event handler to be called whenever there is new depth frame data
					this.Sensor.DepthFrameReady += this.SensorDepthFrameReady;

					// Initialize the array for the Kinect's depth raw data array.
					ShortArray = new short[this.Sensor.DepthStream.FramePixelDataLength];
					ByteArray = new byte[this.Sensor.DepthStream.FramePixelDataLength];
				} else {
					// Turn on the color stream to receive color frames
					this.Sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

					// Allocate space to put the pixels we'll receive
					this.ColorPixels = new byte[this.Sensor.ColorStream.FramePixelDataLength];

					// This is the bitmap we'll display on-screen
					this.WpfBitmap = new WriteableBitmap(this.Sensor.ColorStream.FrameWidth, this.Sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

					// Set the image we display to point to the bitmap where we'll put the image data
					this.Image.Source = this.WpfBitmap;

					// Add an event handler to be called whenever there is new color frame data
					this.Sensor.ColorFrameReady += this.SensorColorFrameReady;
				}

				// Start the sensor!
				try {
					this.Sensor.Start();
				} catch (IOException) {
					this.Sensor = null;
				}
			}

			// Fullscreen mode seems not to be working. See https://github.com/opencv/opencv/issues/10438
			// For workaround, setWindowProperty would be needed, but it seems not to be implemented in Emgu 4.2
			// Emgu.CV.CvInvoke.NamedWindow("Emgu Window", Emgu.CV.CvEnum.NamedWindowType.Fullscreen);

			// Initialize the OpenCV background substactor(s):
			BackSubRgb = new Emgu.CV.BackgroundSubtractorKNN(200, 70, false);
			// backSubDepth = new Emgu.CV.BackgroundSubtractorKNN(2000, 100, false);
			// Another substactor type with worse results:
			// backSubDepth2 = new BackgroundSubtractorMOG2(2000, 10, false);

			// Initialize the kernel matrix parameter used as a parameter in OpenCV functions:
			Kernel9 = new Mat(9, 9, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
		}

		/// <summary>
		/// Execute shutdown tasks
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (null != this.Sensor) {
				this.Sensor.Stop();
			}
		}

		/// <summary>Write pixels to the <see cref="WriteableBitmap"/> that is linked to the WPF form image.</summary>
		/// <param name="outputImg">The output image array.</param>
		internal void DrawImageToScreen(Image<Emgu.CV.Structure.Bgr, byte> outputImg) {
			WpfBitmap.WritePixels(
				new Int32Rect(0, 0, WpfBitmap.PixelWidth, WpfBitmap.PixelHeight)
				, outputImg.Bytes
				, WpfBitmap.PixelWidth * 3
				, 0);
		}

		/// <summary>
		/// Event handler for Kinect sensor's DepthFrameReady event
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e) {
			if (DisplayEventTriggered) return;
			using (DepthImageFrame depthFrame = e.OpenDepthImageFrame()) {
				if (depthFrame != null) {
					depthFrame.CopyPixelDataTo(ShortArray);

					// The depth data from the Kinect 360 comes in a 2 byte (typed as a signed short) value for each pixel with the first 3 bit being skeleton info
					// (all zero when not activated) and the last 13 bit being the depth value. The next step ignores
					// the state of the 3 less significant skeleton bits and converts the whole 2 byte value to a single unsigned byte value.
					// The single byte value is then filled into a byte array from which a new Image<Emgu.CV.Structure.Gray, byte>
					// is created in the next step:
					for (var i = 0; i < ShortArray.Length; i++) {
						// This division maps the 16 bit raw data value to an 8 bit value:
						var b = ((ushort)ShortArray[i]) / 258;
						// Threshold values define the valid capture depth:
						//if (b > 128 || b < 64) { // <- CHANGE THESE VALUES TO DEFINE THE DEPTH RANGE IN WHICH OBJECTS WILL BE TRACKED

						// The next setting captures objects in a larger area than the values above, also removes the
						// "white shadow" effect an object gets when it is to close to the camera:
						if (b < 96) {
							ByteArray[i] = 255; // valid depth
						} else {
							ByteArray[i] = 0; // invalid depth
						}
					}

					var inputImg = new Image<Emgu.CV.Structure.Gray, byte>(WpfBitmap.PixelWidth, WpfBitmap.PixelHeight);
					inputImg.Bytes = ByteArray;
					CvInvoke.MorphologyEx(inputImg, inputImg, Emgu.CV.CvEnum.MorphOp.Close, Kernel9, new System.Drawing.Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));
					DrawImageToScreen(TileEffect(inputImg, new Emgu.CV.Structure.Bgr(0, 255, 255)));
				}
			}
		}

		/// <summary>Event handler for Kinect sensor's ColorFrameReady event.</summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e) {
			using (ColorImageFrame colorFrame = e.OpenColorImageFrame()) {
				if (colorFrame != null) {
					// Copy the pixel data from the image to a temporary array
					colorFrame.CopyPixelDataTo(this.ColorPixels);

					var bitmap = BitmapFromWriteableBitmap(WpfBitmap);
					var inputImg = bitmap.ToImage<Emgu.CV.Structure.Bgr, byte>();
					//Emgu.CV.CvInvoke.Imshow("Emgu Window", inputImg);
					//var outputImg = new Mat(inputImg.Width, inputImg.Height, Emgu.CV.CvEnum.DepthType.Cv8U, 3);

					Emgu.CV.BackgroundSubtractorExtension.Apply(BackSubRgb, inputImg, inputImg, -1);
					//CvInvoke.Threshold(inputImg, inputImg, 127, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
					CvInvoke.Erode(inputImg, inputImg, null, new System.Drawing.Point(-1, -1), 3, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));
					CvInvoke.MorphologyEx(inputImg, inputImg, Emgu.CV.CvEnum.MorphOp.Close, Kernel9, new System.Drawing.Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new Emgu.CV.Structure.MCvScalar(1));

					Emgu.CV.CvInvoke.Imshow("Emgu Window", inputImg);

					// Write the pixel data into our bitmap
					this.WpfBitmap.WritePixels(
						new Int32Rect(0, 0, this.WpfBitmap.PixelWidth, this.WpfBitmap.PixelHeight),
						this.ColorPixels,
						this.WpfBitmap.PixelWidth * sizeof(int),
						0);
				}
			}
		}

		/// <summary>Helper function that converts a <see cref="WriteableBitmap"/> object to a <see cref="System.Drawing.Bitmap"/> object.</summary>
		/// <param name="writeBmp">The <see cref="WriteableBitmap"/> object to convert.</param>
		/// <returns>A <see cref="System.Drawing.Bitmap"/> for further processing.</returns>
		/// <remarks>Not really used anymore as it is easier to directly access and manipulate the raw pixel data from the Kinect camera (like in <see cref="SensorDepthFrameReady"/>).</remarks>
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