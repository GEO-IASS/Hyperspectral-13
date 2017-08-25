using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static HyperSpectralWPF.HighlightMenu;

namespace HyperSpectralWPF
{
    /// <summary>
    /// This class deals with all functions related to image management,
    /// this includes retrieving image data, changing images, freezing
    /// images, blur effects, and adding highlights.
    /// </summary>
    public class ImageManager
    {
        /// <summary>
        /// Global constants
        /// </summary>
        private const float MAXIMUM_INTENSITY    = 255.0F;
        private const int   LOWEST_WAVELENGTH    = 528;
        private const int   WAVELENGTH_INCREMENT = 4;

        private MainWindow mainWindow;

        /// <summary>
        /// Variables pertaining to image processing
        /// </summary>
        private WriteableBitmap[]         bitmaps            = null;
        private ImageData                 imageData          = null;
        private readonly BackgroundWorker backgroundWorker   = new BackgroundWorker();
        private int                       imageIndex         = 0;
        private int                       wavelength;
        private bool                      ImageIsDisplayed   = false;
        private HighlightCondition        highlightCondition = HighlightCondition.ABOVE;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mainWindow"></param>
        public ImageManager(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
        }

        /// <summary>
        /// Returns this image manager's bitmaps
        /// </summary>
        /// <returns></returns>
        public WriteableBitmap[] GetBitmaps()
        {
            return bitmaps;
        }

        /// <summary>
        /// Returns this image manager's h5 image data
        /// </summary>
        /// <returns></returns>
        public ImageData GetImageData()
        {
            return imageData;
        }

        /// <summary>
        /// Sets the image index to specified value
        /// </summary>
        /// <param name="value"></param>
        public void SetImageIndex(int value)
        {
            imageIndex = value;
        }

        /// <summary>
        /// Returns the current image index
        /// </summary>
        /// <returns></returns>
        public int GetImageIndex()
        {
            return imageIndex;
        }

        /// <summary>
        /// Set the current wavelength to the specified value
        /// </summary>
        /// <param name="value"></param>
        public void SetWavelength(int value)
        {
            wavelength = value;
        }

        /// <summary>
        /// Returns the current wavelength
        /// </summary>
        /// <returns></returns>
        public int GetWavelength()
        {
            return wavelength;
        }

        /// <summary>
        /// Returns if an image is being displayed
        /// </summary>
        /// <returns></returns>
        public bool IsImageDisplayed()
        {
            return ImageIsDisplayed;
        }

        /// <summary>
        /// Set the currently displayed image to the current slider value
        /// </summary>
        public void UpdateImageToSliderValue()
        {
            imageIndex = (int)mainWindow.ImageSlider.Value;
            mainWindow.ImageViewer.Source = bitmaps[imageIndex];
            wavelength = LOWEST_WAVELENGTH + (imageIndex * WAVELENGTH_INCREMENT);
            mainWindow.LambdaTextBlock.Text = wavelength.ToString();
            mainWindow.IndexValueTextBlock.Text = (imageIndex + 1).ToString();
        }

        /// <summary>
        /// Initializes the image processing
        /// </summary>
        /// <param name="fileName"></param>
        public void ProcessImages(string fileName)
        {
            // Retrieve the h5 image data from the specified file
            imageData = new ImageData(fileName);

            mainWindow.MainContent.Visibility = Visibility.Hidden;
            mainWindow.MyProgressBar.Value = 0;
            mainWindow.LoadingBar.Visibility = Visibility.Visible;
            mainWindow.ImageSlider.Value = mainWindow.ImageSlider.Minimum;
            ImageIsDisplayed = false;

            backgroundWorker.RunWorkerAsync();
        }
        
        /// <summary>
        /// Processes all of the images in the file specified.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Image data variables
            float[,,] data  = imageData.GetData();
            double max      = imageData.GetMaximum();
            double min      = imageData.GetMinimum();
            int lambdaCount = imageData.GetLambdaCount();
            int width       = imageData.GetWidth();
            int height      = imageData.GetHeight();

            // Initialize the writeable bitmaps
            WriteableBitmap[] bitmaps = new WriteableBitmap[lambdaCount];

            // Go through each image in the image data
            for (int lambda = 0; lambda < lambdaCount; lambda++)
            {
                // Instantiate a new writeable bitmap to store this image's pixel data.
                bitmaps[lambda] = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);

                // Reserve the back buffer for updates.
                bitmaps[lambda].Lock();

                // Iterate through the pixels along the x & y axis in the currently loaded image
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double n = data[lambda, y, x];
                        byte pixelValue = (byte)(((n - min) / (max - min)) * MAXIMUM_INTENSITY);
                        unsafe
                        {
                            // Get a pointer to the back buffer.
                            int pBackBuffer = (int)bitmaps[lambda].BackBuffer;

                            // Find the address of the pixel to draw.
                            pBackBuffer += y * bitmaps[lambda].BackBufferStride;
                            pBackBuffer += x * 4;

                            // Compute the pixel's color.
                            int color_data = pixelValue << 16; // R
                            color_data    |= pixelValue << 8;  // G
                            color_data    |= pixelValue << 0;  // B

                            // Assign the color data to the pixel.
                            *((int*)pBackBuffer) = color_data;
                        }
                        // Specify the area of the bitmap that changed.
                        bitmaps[lambda].AddDirtyRect(new Int32Rect(x, y, 1, 1));
                    }
                }

                // Release the back buffer and make it available for display.
                bitmaps[lambda].Unlock();

                // Make this bitmap unmodifiable so that any thread can access it.
                bitmaps[lambda].Freeze();

                // Report progress to background worker
                float quotient = ((lambda + 1) * 1.0f) / (lambdaCount * 1.0f);
                float percentage = quotient * 100.0f;
                (sender as BackgroundWorker).ReportProgress((int)percentage);
            }

            // Pass the images to the main thread via the result
            e.Result = bitmaps;
        }

        /// <summary>
        /// Updates the progress bar when progress changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            mainWindow.MyProgressBar.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// Updates the UI once the background backgroundWorker is done processing the images.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Get the bitmaps that were processed by the background thread
            bitmaps = (WriteableBitmap[])e.Result;

            // Hide the loading bar and show the main content
            mainWindow.LoadingBar.Visibility = Visibility.Hidden;
            mainWindow.FrozenArea.Visibility = Visibility.Hidden;
            mainWindow.FrozenArea.Width  = 0;
            mainWindow.FrozenArea.Height = 0;
            mainWindow.MainContent.Visibility = Visibility.Visible;

            // Display the file and image information
            mainWindow.FileTextBlock.Text   = "Filename: " + imageData.GetFileName();
            mainWindow.WidthTextBlock.Text  = "Width: "    + imageData.GetWidth();
            mainWindow.HeightTextBlock.Text = "Height: "   + imageData.GetHeight();

            // Set the image index to the first image and calculate the current
            // wavelength being displayed.
            imageIndex = 0;
            wavelength = LOWEST_WAVELENGTH + (imageIndex * WAVELENGTH_INCREMENT);

            mainWindow.LambdaTextBlock.Text     = wavelength.ToString();
            mainWindow.IndexValueTextBlock.Text = (imageIndex + 1).ToString();

            float viewerScale = 500.0f / (imageData.GetHeight() * 1.0f);

            // Set the current visible image
            mainWindow.ImageViewer.Source              = bitmaps[imageIndex];
            mainWindow.ImageViewer.Width               = (imageData.GetWidth()  * 1.0f) * viewerScale;
            mainWindow.ImageViewer.Height              = (imageData.GetHeight() * 1.0f) * viewerScale;
            mainWindow.ImageViewer.HorizontalAlignment = HorizontalAlignment.Center;
            mainWindow.ImageViewer.VerticalAlignment   = VerticalAlignment.Center;

            ImageIsDisplayed = true;

            // Enable the toolbar and file menu buttons
            mainWindow.SelectModeSwitch.IsEnabled     = true;
            mainWindow.SelectAreaModeSwitch.IsEnabled = true;
            mainWindow.GestureModeSwitch.IsEnabled    = true;
            mainWindow.VoiceModeSwitch.IsEnabled      = true;
            mainWindow.SaveImageAsBtn.IsEnabled       = true;
            mainWindow.OpenButton.IsEnabled           = true;
            mainWindow.FileMenuOpenButton.IsEnabled   = true;
            mainWindow.FileMenuSaveButton.IsEnabled   = true;
            mainWindow.BlurButton.IsEnabled           = true;
            mainWindow.GraphButton.IsEnabled          = true;
            mainWindow.HighlightButton.IsEnabled      = true;

            // Check the gesture and voice switches
            mainWindow.GestureModeSwitch.IsChecked = true;
            mainWindow.VoiceModeSwitch.IsChecked   = true;
        }
        
        /// <summary>
        /// Freezes the current image and displays it on the left side
        /// so that the user can compare the two images.
        /// </summary>
        public void FreezeCurrentImage()
        {
            mainWindow.FrozenArea.Width    = mainWindow.ImageViewer.Width;
            mainWindow.FrozenArea.Height   = mainWindow.ImageViewer.Height;

            mainWindow.FrozenImage1.Source = mainWindow.ImageViewer.Source;
            mainWindow.FrozenImage1.Width  = mainWindow.ImageViewer.Width;
            mainWindow.FrozenImage1.Height = mainWindow.ImageViewer.Height;

            mainWindow.FrozenImage1.VerticalAlignment   = VerticalAlignment.Center;
            mainWindow.FrozenImage1.HorizontalAlignment = HorizontalAlignment.Center;

            mainWindow.FrozenImage1.Visibility = Visibility.Visible;
            mainWindow.FrozenArea.Visibility   = Visibility.Visible;
        }

        /// <summary>
        /// Shows the previous image in the data
        /// </summary>
        public void ShowPreviousImage()
        {
            if (bitmaps != null && bitmaps.Length > 0)
            {
                if (imageIndex > 0)
                {
                    imageIndex = imageIndex - 1;

                    mainWindow.ImageViewer.Source = bitmaps[imageIndex];
                    mainWindow.ImageSlider.Value = imageIndex;

                    wavelength = LOWEST_WAVELENGTH + (imageIndex * WAVELENGTH_INCREMENT);
                    mainWindow.LambdaTextBlock.Text = wavelength.ToString();
                    mainWindow.IndexValueTextBlock.Text = (imageIndex + 1).ToString();
                }
            }
        }

        /// <summary>
        /// Shows the next image in the data
        /// </summary>
        public void ShowNextImage()
        {
            if (bitmaps != null && bitmaps.Length > 0)
            {
                if (imageIndex < bitmaps.Length - 1)
                {
                    imageIndex = imageIndex + 1;

                    mainWindow.ImageViewer.Source = bitmaps[imageIndex];
                    mainWindow.ImageSlider.Value = imageIndex;

                    wavelength = LOWEST_WAVELENGTH + (imageIndex * WAVELENGTH_INCREMENT);
                    mainWindow.LambdaTextBlock.Text = wavelength.ToString();
                    mainWindow.IndexValueTextBlock.Text = (imageIndex + 1).ToString();
                }
            }
        }

        /// <summary>
        /// Goes to an image specified by the index.
        /// </summary>
        /// <param name="index"></param>
        public void GoToImage(int index)
        {
            if (ImageIsDisplayed && imageData != null)
            {
                if (index < (imageData.GetLambdaCount() + 1) && index > 0)
                {
                    if (bitmaps != null && bitmaps.Length > 0)
                    {
                        imageIndex = index - 1;

                        mainWindow.ImageViewer.Source = bitmaps[imageIndex];
                        mainWindow.ImageSlider.Value = imageIndex;

                        wavelength = LOWEST_WAVELENGTH + (imageIndex * WAVELENGTH_INCREMENT);
                        mainWindow.LambdaTextBlock.Text = wavelength.ToString();
                        mainWindow.IndexValueTextBlock.Text = (imageIndex + 1).ToString();
                    }
                }
            }
        }

        /// <summary>
        /// Applies the given blur effect to the image
        /// </summary>
        /// <param name="blur"></param>
        public void ApplyBlur(float[,] blur)
        {
            float[,] data = blur;
            double max     = imageData.GetMaximum();
            double min     = imageData.GetMinimum();
            int width      = imageData.GetWidth();
            int height     = imageData.GetHeight();

            WriteableBitmap mosaic = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            mosaic.Lock();

            // Go through each pixel in each image
            for (int y = 0; y < mosaic.Height; y++)
            {
                for (int x = 0; x < mosaic.Width; x++)
                {
                    double n = data[y, x];
                    byte pixelValue = (byte)(((n - min) / (max - min)) * MAXIMUM_INTENSITY);
                    unsafe
                    {
                        // Get a pointer to the back buffer.
                        int pBackBuffer = (int)mosaic.BackBuffer;

                        // Find the address of the pixel to draw.
                        pBackBuffer += y * mosaic.BackBufferStride;
                        pBackBuffer += x * 4;

                        // Compute the pixel's color.
                        int color_data = pixelValue << 16; // R
                        color_data |= pixelValue << 8;  // G
                        color_data |= pixelValue << 0;  // B

                        // Assign the color data to the pixel.
                        *((int*)pBackBuffer) = color_data;
                    }
                    // Specify the area of the bitmap that changed.
                    mosaic.AddDirtyRect(new Int32Rect(x, y, 1, 1));
                }
            }

            // Release the back buffer and make it available for display.
            mosaic.Unlock();

            mainWindow.ImageViewer.Source = mosaic;
        }

        /// <summary>
        /// Creates a mosaic blur effect and returns the blurred
        /// pixel data.
        /// </summary>
        /// <returns></returns>
        public float[,] MosaicBlur()
        {
            int lambdaCount = imageData.GetLambdaCount();
            int width       = imageData.GetWidth();
            int height      = imageData.GetHeight();
            int pixelLength = 10;

            float[,,] oldPixelData = imageData.GetData();
            float[,]  newPixelData = new float[height, width];

            for (int y = 0; y < height; y += pixelLength)
            {
                for (int x = 0; x < width; x += pixelLength)
                {
                    float sum = 0, avg = 0;
                    for (int yy = y; yy < y + pixelLength; yy++)
                    {
                        for (int xx = x; xx < x + pixelLength; xx++)
                        {
                            if (xx < width && yy < height)
                            {
                                sum += oldPixelData[imageIndex, yy, xx];
                            }
                        }
                    }

                    avg = sum / (pixelLength * pixelLength);
                    for (int yy = y; yy < y + pixelLength; yy++)
                    {
                        for (int xx = x; xx < x + pixelLength; xx++)
                        {
                            if (xx < width && yy < height)
                            {
                                newPixelData[yy, xx] = avg;
                            }
                        }
                    }
                }
            }

            return newPixelData;
        }

        /// <summary>
        /// Creates a box blur effect and returns the blurred pixel data.
        /// </summary>
        /// <returns>The blurred pixel data</returns>
        public float[,] BoxBlur()
        {
            int lambdaCount = imageData.GetLambdaCount();
            int width       = imageData.GetWidth();
            int height      = imageData.GetHeight();
            int pixelLength = 10;

            float[,,] oldPixelData = imageData.GetData();
            float[,] newPixelData = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0, avg = 0;
                    for (int yy = y; yy < y + pixelLength; yy++)
                    {
                        for (int xx = x; xx < x + pixelLength; xx++)
                        {
                            if (xx < width && yy < height)
                            {
                                sum += oldPixelData[imageIndex, xx, yy];
                            }
                        }
                    }

                    avg = sum / (pixelLength * pixelLength);
                    newPixelData[x, y] = avg;
                }
            }

            return newPixelData;
        }

        /// <summary>
        /// Highlights all values above a certain threshold as red
        /// </summary>
        /// <param name="value"></param>
        public void HighlightThreshold(int threshold, HighlightCondition highlightCondition = HighlightCondition.ABOVE)
        {
            this.highlightCondition = highlightCondition;
            double max              = imageData.GetMaximum();
            double min              = imageData.GetMinimum();
            int width               = imageData.GetWidth();
            int height              = imageData.GetHeight();
            float[,,] data         = imageData.GetData();

            WriteableBitmap highlightedImage = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            highlightedImage.Lock();

            // Go through each pixel in each image
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double n = data[imageIndex, y, x];
                    byte pixelValue = (byte)(((n - min) / (max - min)) * MAXIMUM_INTENSITY);
                    unsafe
                    {
                        // Get a pointer to the back buffer.
                        int pBackBuffer = (int)highlightedImage.BackBuffer;

                        // Find the address of the pixel to draw.
                        pBackBuffer += y * highlightedImage.BackBufferStride;
                        pBackBuffer += x * 4;

                        // Compute the pixel's color.
                        int color_data;
                        if (highlightCondition == HighlightCondition.ABOVE)
                        {
                            if (n > threshold)
                            {
                                color_data = pixelValue << 16; // R
                                color_data |= 0;               // G
                                color_data |= 0;               // B
                            }
                            else
                            {
                                color_data = pixelValue << 16; // R
                                color_data |= pixelValue << 8; // G
                                color_data |= pixelValue << 0; // B
                            }
                        }
                        else
                        {
                            if (n < threshold)
                            {
                                color_data = pixelValue << 16; // R
                                color_data |= 0;               // G
                                color_data |= 0;               // B
                            }
                            else
                            {
                                color_data = pixelValue << 16; // R
                                color_data |= pixelValue << 8; // G
                                color_data |= pixelValue << 0; // B
                            }
                        }

                        // Assign the color data to the pixel.
                        *((int*)pBackBuffer) = color_data;
                    }
                    // Specify the area of the bitmap that changed.
                    highlightedImage.AddDirtyRect(new Int32Rect(x, y, 1, 1));
                }
            }
            // Release the back buffer and make it available for display.
            highlightedImage.Unlock();

            mainWindow.ImageViewer.Source = highlightedImage;
        }
    }
}
