using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.IO;

using Microsoft.Win32;
using Microsoft.Kinect;
using LightBuzz.Vitruvius;
using Microsoft.Kinect.Wpf.Controls;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HyperSpectralWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        /// <summary>
        /// Represents a pixel that has an
        /// x and y coordinate and a value.
        /// </summary>
        public struct Pixel
        {
            public int x;
            public int y;
            public float value;

            public Pixel(int x, int y, float value)
            {
                this.x = x;
                this.y = y;
                this.value = value;
            }
        }

        /// <summary>
        /// Instance variables
        /// </summary>
        private bool         selectPixelMode   = false;
        public  Pixel        selectedPixel     = new Pixel(-1, -1, -1);
        private bool         selectAreaMode    = false;
        private bool         gesturesEnabled   = true;
        private bool         mouseDown         = false;             // Set to 'true' when mouse is held down.
        private Storyboard   storyBoard        = null;              // The border animation
        private Point        mouseDownPosition = new Point(-1, -1); // The point where the mouse button was clicked down relative to the canvas.
        private Point        coordAtMouseDown  = new Point(-1, -1); // The coord where the mouse button was clicked down on the image.
        private Point        topLeft           = new Point(-1, -1);
        private Point        bottomRight       = new Point(-1, -1);
        private ImageManager imageManager      = null;

        /// <summary>
        /// Variables pertaining to the Kinect
        /// </summary>
        private KinectSensor      kinectSensor      = null;
        private VoiceRecognition  voiceRecognition  = null;
        private BodyFrameReader   bodyFrameReader   = null;
        private GestureController gestureController = null;
        private string            statusText        = null;
        private GestureType       lastGesture;

        public event PropertyChangedEventHandler PropertyChanged;
        public string StatusText
        {
            get { return statusText; }
            set
            {
                if (statusText != value)
                {
                    statusText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StatusText"));
                }
            }
        }
        
        /// <summary>
        /// Main window constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets up main program as soon as main window loads.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the default kinect sensor and open it
            kinectSensor = KinectSensor.GetDefault();

            // Set IsAvailableChanged event notifier
            kinectSensor.IsAvailableChanged += Sensor_IsAvailableChanged;

            kinectSensor.Open();

            // Create a new voice recognition instance
            voiceRecognition = new VoiceRecognition(kinectSensor, this);

            bodyFrameReader = kinectSensor.BodyFrameSource.OpenReader();
            bodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

            // Create a new gesture controller and assign the method that runs
            // when a gesture is recognized.
            gestureController = new GestureController();
            gestureController.GestureRecognized += GestureController_GestureRecognized;

            imageManager = new ImageManager(this);
            storyBoard = FindResource("BorderAnimation") as Storyboard;
            Storyboard.SetTarget(storyBoard, selectionBox);

            // Create the kinect region for the user view
            KinectRegion.SetKinectRegion(this, kinectRegion);

            App app = ((App)Application.Current);
            app.KinectRegion = kinectRegion;

            // Use the default sensor
            kinectRegion.KinectSensor = kinectSensor;
        }

        /// <summary>
        /// Handler for when a keyboard button is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && Keyboard.IsKeyDown(Key.O))
            {
                OpenButton_Click(sender, e);
            }
            else if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && Keyboard.IsKeyDown(Key.S))
            {
                SaveImageAsButton_Click(sender, e);
            }
            else if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && Keyboard.IsKeyDown(Key.G))
            {
                GraphButton_Click(sender, e);
            }
            else if (Keyboard.IsKeyDown(Key.Left))
            {
                imageManager.ShowPreviousImage();
            }
            else if (Keyboard.IsKeyDown(Key.Right))
            {
                imageManager.ShowNextImage();
            }
            else if (Keyboard.IsKeyDown(Key.F))
            {
                imageManager.FreezeCurrentImage();
            }
        }

        /// <summary>
        /// Handler for window closing event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            voiceRecognition.Close();

            if (null != this.kinectSensor)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles pixel select and area select mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Viewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (selectAreaMode)
            {
                // Capture and track the mouse.
                mouseDown = true;
                mouseDownPosition = e.GetPosition(MyCanvas);
                ImageViewer.CaptureMouse();

                coordAtMouseDown = new Point(float.Parse(XPosition.Text), float.Parse(YPosition.Text));

                // Initial placement of the drag selection box.         
                Canvas.SetLeft(selectionBox, mouseDownPosition.X);
                Canvas.SetTop(selectionBox, mouseDownPosition.Y);
                selectionBox.Width = 0;
                selectionBox.Height = 0;

                // Make the drag selection box visible.
                selectionBox.Visibility = Visibility.Visible;

                // If the dash border for the selection box is not already animated,
                // animate it.
                if (!(DependencyPropertyHelper.GetValueSource(selectionBox, Rectangle.StrokeDashOffsetProperty).IsAnimated))
                {
                    storyBoard.Begin();
                }
            }
            else if (selectPixelMode)
            {
                System.Windows.Point p = e.GetPosition(ImageViewer);
                double pixelWidth = ImageViewer.Source.Width;
                double pixelHeight = ImageViewer.Source.Height;
                double mouseX = pixelWidth * p.X / ImageViewer.ActualWidth;
                double mouseY = pixelHeight * p.Y / ImageViewer.ActualHeight;

                selectedPixel = new Pixel();
                selectedPixel.x = (int)mouseX;
                selectedPixel.y = (int)mouseY;
                selectedPixel.value = imageManager.GetImageData().GetData()[imageManager.GetImageIndex(), (int)mouseX, (int)mouseY];

                HighlightPixel(selectedPixel);

                SelectModeSwitch.IsChecked = false;
            }
        }

        /// <summary>
        /// Stops the drawing of the selection box once the mouse button is no
        /// longer pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Viewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (selectAreaMode)
            {
                // Release the mouse capture and stop tracking it.
                mouseDown = false;
                ImageViewer.ReleaseMouseCapture();

                // Calculate where the mouse up position is relative to the image viewer.
                Point coordAtMouseUp = new Point(float.Parse(XPosition.Text), float.Parse(YPosition.Text));

                // Calculate where the top left and bottom right corners are.
                topLeft = new Point();
                bottomRight = new Point();

                // Get x coordinate of top left and bottom right corners.
                if (coordAtMouseDown.X > coordAtMouseUp.X)
                {
                    bottomRight.X = coordAtMouseDown.X;
                    topLeft.X = coordAtMouseUp.X;
                }
                else if (coordAtMouseUp.X > coordAtMouseDown.X)
                {
                    bottomRight.X = coordAtMouseUp.X;
                    topLeft.X = coordAtMouseDown.X;
                }

                // Get y coordinate of top left and bottom right corners.
                if (coordAtMouseDown.Y > coordAtMouseUp.Y)
                {
                    bottomRight.Y = coordAtMouseDown.Y;
                    topLeft.Y = coordAtMouseUp.Y;
                }
                else if (coordAtMouseUp.Y > coordAtMouseDown.Y)
                {
                    bottomRight.Y = coordAtMouseUp.Y;
                    topLeft.Y = coordAtMouseDown.Y;
                }
                else if (coordAtMouseUp.Y == coordAtMouseDown.Y)
                {
                    bottomRight.Y = coordAtMouseUp.Y;
                    topLeft.Y = coordAtMouseDown.Y;
                }
            }
        }

        /// <summary>
        /// Prints the mouse position when the mouse hovers over the image.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Viewer_MouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(ImageViewer);
            double pixelWidth = ImageViewer.Source.Width;
            double pixelHeight = ImageViewer.Source.Height;
            double x = pixelWidth * p.X / ImageViewer.ActualWidth;
            double y = pixelHeight * p.Y / ImageViewer.ActualHeight;

            if (x < 0) x = 0;
            if (x > imageManager.GetImageData().GetWidth() - 1) x = imageManager.GetImageData().GetWidth() - 1;

            if (y < 0) y = 0;
            if (y > imageManager.GetImageData().GetHeight() - 1) y = imageManager.GetImageData().GetHeight() - 1;

            XPosition.Text = ((int)x).ToString();
            YPosition.Text = ((int)y).ToString();

            if (mouseDown && selectAreaMode)
            {
                // When the mouse is held down, reposition the drag selection box.
                Point mousePosition = e.GetPosition(MyCanvas);
                
                if (mouseDownPosition.X < mousePosition.X)
                {
                    Canvas.SetLeft(selectionBox, mouseDownPosition.X);
                    selectionBox.Width = mousePosition.X - mouseDownPosition.X;
                }
                else
                {
                    Canvas.SetLeft(selectionBox, mousePosition.X);
                    selectionBox.Width = mouseDownPosition.X - mousePosition.X;
                }

                if (mouseDownPosition.Y < mousePosition.Y)
                {
                    Canvas.SetTop(selectionBox, mouseDownPosition.Y);
                    selectionBox.Height = mousePosition.Y - mouseDownPosition.Y;
                }
                else
                {
                    Canvas.SetTop(selectionBox, mousePosition.Y);
                    selectionBox.Height = mouseDownPosition.Y - mousePosition.Y;
                }

                if (mousePosition.X > ImageViewer.Position().X + ImageViewer.Position().Width)
                {
                    selectionBox.Width = ImageViewer.Position().X + ImageViewer.Position().Width - mouseDownPosition.X;
                }

                if (mousePosition.Y > ImageViewer.Position().Y + ImageViewer.Position().Height - 30)
                {
                    selectionBox.Height = ImageViewer.Position().Y + ImageViewer.Position().Height - mouseDownPosition.Y - 30;
                }

                if (mousePosition.X < ImageViewer.Position().X)
                {
                    Canvas.SetLeft(selectionBox, ImageViewer.Position().X);
                    selectionBox.Width = mouseDownPosition.X - ImageViewer.Position().X;
                }

                if (mousePosition.Y < ImageViewer.Position().Y - 30)
                {
                    Canvas.SetTop(selectionBox, ImageViewer.Position().Y - 30);
                    selectionBox.Height = mouseDownPosition.Y - (ImageViewer.Position().Y - 30);
                }
            }
        }

        /// <summary>
        /// Sets the kinect status text whenever the kinect status changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            KinectStatus.Text = kinectSensor.IsAvailable ? "Running" : "Not Available";
        }
        
        /// <summary>
        /// Handles body frame events
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            // Body frame
            using (BodyFrame frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Body body = frame.Bodies().Closest();

                    if (body != null && gesturesEnabled)
                    {
                        if (body.IsTracked && imageManager.IsImageDisplayed())
                        {
                            LHSValue.Text = body.HandLeftState.ToString();
                            RHSValue.Text = body.HandRightState.ToString();

                            if (lastGesture == GestureType.SwipeLeft && body.HandRightState == HandState.Lasso)
                            {
                                imageManager.ShowPreviousImage();
                            }

                            if (lastGesture == GestureType.SwipeRight && body.HandLeftState == HandState.Lasso)
                            {
                                imageManager.ShowNextImage();
                            }
                        }

                        gestureController.Update(body);
                    }
                }
            }
        }

        /// <summary>
        /// Handler for when gestures are recognized
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GestureController_GestureRecognized(object sender, GestureEventArgs e)
        {
            var gesture = e.GestureType;
            lastGesture = gesture;
            this.GestureTextBlock.Text = "Last Gesture: " + gesture.ToString();

            if (imageManager.IsImageDisplayed())
            {
                switch (gesture)
                {
                    case GestureType.SwipeLeft:
                        imageManager.ShowPreviousImage();
                        break;

                    case GestureType.SwipeRight:
                        imageManager.ShowNextImage();
                        break;
                }
            }
        }
        
        /// <summary>
        /// Open file button event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new OpenFileDialog and set the filter to only show HDF5 files
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "HDF5 (*.h5)|*.h5";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Multiselect = false;
            
            // Show the open file dialog and retrieve the file url
            if (openFileDialog.ShowDialog() == true)
            {
                imageManager.ProcessImages(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// Highlights all values above a certain threshold as red
        /// </summary>
        /// <param name="value"></param>
        public void HighlightPixel(Pixel pixel)
        {
            PixelIndicator.Visibility = Visibility.Visible;
            Canvas.SetLeft(PixelIndicator, Mouse.GetPosition(Application.Current.MainWindow).X - 1);
            Canvas.SetTop(PixelIndicator, Mouse.GetPosition(Application.Current.MainWindow).Y - 31);
            PixelIndicator.ToolTip = "x: " + selectedPixel.x + ", y: " + selectedPixel.y + ", value: " + Math.Round(selectedPixel.value);
        }
        
        /// <summary>
        /// Saves the current image to the path specified by the user.
        /// </summary>
        /// <param name="filePath">The path specified by the user</param>
        public void SaveImageToFile(string filePath)
        {
            var image = (BitmapSource) ImageViewer.Source;
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(fileStream);
            }
        }

        /// <summary>
        /// Handler for slider threshold changing event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            imageManager.UpdateImageToSliderValue();
        }
        
        /// <summary>
        /// Gets rid of the unnecessary overflow bar when the toolbar is loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            ToolBar toolBar = sender as ToolBar;
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
            {
                overflowGrid.Visibility = Visibility.Collapsed;
            }
            var mainPanelBorder = toolBar.Template.FindName("MainPanelBorder", toolBar) as FrameworkElement;
            if (mainPanelBorder != null)
            {
                mainPanelBorder.Margin = new Thickness();
            }
        }
        
        /// <summary>
        /// Preferences button event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PreferencesButton_Click(object sender, RoutedEventArgs e)
        {
            PreferencesWindow preferences = new PreferencesWindow();
            preferences.Show();
        }
        
        /// <summary>
        /// Exit button event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Saves the current image to a .png file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveImageAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG (*.png)|*.png";

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveImageToFile(saveFileDialog.FileName);
            }
        }
        
        /// <summary>
        /// Opens up the blur options menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BlurButton_Click(object sender, RoutedEventArgs e)
        {
            BlurOptions blurOptions = new BlurOptions();
            if (blurOptions.ShowDialog() == true)
            {
                BlurOptions.BlurChoice choice = blurOptions.GetChoice();

                if (choice == BlurOptions.BlurChoice.MOSAIC)
                {
                    imageManager.ApplyBlur(imageManager.MosaicBlur());
                }
                else if (choice == BlurOptions.BlurChoice.BOX)
                {
                    imageManager.ApplyBlur(imageManager.BoxBlur());
                }
            }
        }

        /// <summary>
        /// Highlights all pixels above a certain threshold
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HighlightButton_Click(object sender, RoutedEventArgs e)
        {
            HighlightMenu highlightMenu = new HighlightMenu();
            if (highlightMenu.ShowDialog() == true)
            {
                float value = highlightMenu.GetValue();
                Console.WriteLine(value);

                imageManager.HighlightThreshold((int)value, highlightMenu.GetHighlightCondition());
            }
        }

        /// <summary>
        /// Displays the graph for the selected pixel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GraphButton_Click(object sender, RoutedEventArgs e)
        {
            // If selected pixel has been initialized (aka values != -1), then graph pixel
            if (selectedPixel.x != -1 && selectedPixel.y != -1 && selectedPixel.value != -1)
            {
                float[,,] data = imageManager.GetImageData().GetData();
                float[] pixelValues = new float[imageManager.GetImageData().GetLambdaCount()];
                for (int lambda = 0; lambda < imageManager.GetImageData().GetLambdaCount(); lambda++)
                {
                    pixelValues[lambda] = data[lambda, (int)selectedPixel.x, (int)selectedPixel.y];
                }

                Graph graph = new Graph(pixelValues, selectedPixel.x, selectedPixel.y, imageManager.GetWavelength());
                graph.Show();
            }
            else if (topLeft != null && bottomRight != null && selectionBox.Visibility != Visibility.Collapsed)
            {
                float[,,] data = imageManager.GetImageData().GetData();
                float[] pixelValues = new float[imageManager.GetImageData().GetLambdaCount()];
                for (int lambda = 0; lambda < imageManager.GetImageData().GetLambdaCount(); lambda++)
                {
                    float sum = 0, count = 0, avg = 0;
                    for (int y = (int)topLeft.Y; y < (int)bottomRight.Y; y++)
                    {
                        for (int x = (int)topLeft.X; y < (int)bottomRight.X; y++)
                        {
                            sum += data[lambda, x, y];
                            count++;
                        }
                    }
                    avg = sum / count;
                    pixelValues[lambda] = avg;
                }

                Graph graph = new Graph(pixelValues, topLeft, bottomRight, imageManager.GetWavelength());
                graph.Show();
            }
        }
        
        /// <summary>
        /// Enables select mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectPixelModeSwitch_Checked(object sender, RoutedEventArgs e)
        {
            selectPixelMode = true;
            SelectAreaModeSwitch.IsChecked = false;
            this.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// Disables select mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectPixelModeSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            selectPixelMode = false;
            this.Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// Enables select area mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectAreaModeSwitch_Checked(object sender, RoutedEventArgs e)
        {
            selectAreaMode = true;
            SelectModeSwitch.IsChecked = false;
            selectedPixel = new Pixel(-1, -1, -1);
            this.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// Disables select mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectAreaModeSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            selectAreaMode = false;
            this.Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// Enables gesture mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GestureModeSwitch_Checked(object sender, RoutedEventArgs e)
        {
            gesturesEnabled = true;
        }

        /// <summary>
        /// Disables gesture mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GestureModeSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            gesturesEnabled = false;
        }

        /// <summary>
        /// Enables voice recognition
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VoiceModeSwitch_Checked(object sender, RoutedEventArgs e)
        {
            voiceRecognition.Enable();
        }

        /// <summary>
        /// Disables voice recognition
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VoiceModeSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            voiceRecognition.Disable();
        }

        /// <summary>
        /// Return a reference to this class's image manager instance.
        /// </summary>
        /// <returns></returns>
        public ImageManager GetImageManager()
        {
            return this.imageManager;
        }
    }
}