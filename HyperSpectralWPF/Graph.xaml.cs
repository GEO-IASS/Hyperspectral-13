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

using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;
using Microsoft.Win32;
using System.IO;
using OxyPlot.Axes;
using OxyPlot.Annotations;

namespace HyperSpectralWPF
{
    /// <summary>
    /// Interaction logic for Graph.xaml
    /// </summary>
    public partial class Graph
    {
        public PlotModel Model { get; set; }

        /// <summary>
        /// Represents the different types of data that
        /// can be graphed.
        /// </summary>
        private enum GraphType
        {
            PIXEL,
            AREA
        };
        private GraphType graphType;

        /// <summary>
        /// Represents each export choice
        /// </summary>
        private enum ExportSelection
        {
            PNG = 1,
            PDF = 2,
            SVG = 3,
            CSV = 4
        };

        /// <summary>
        /// Global constants
        /// </summary>
        private const int LOWEST_WAVELENGTH    = 528;
        private const int WAVELENGTH_INCREMENT = 4;

        /// <summary>
        /// Instance variables
        /// </summary>
        private int     x, y;
        private Point   topLeft;
        private Point   bottomRight;
        private float[] data;

        /// <summary>
        /// Constructor for graphing a pixel
        /// </summary>
        /// <param name="data">The data to graph</param>
        /// <param name="x">The x coordinate of the selected pixel</param>
        /// <param name="y">The y coordinate of the selected pixel</param>
        /// <param name="wavelength">The wavelength of the current image</param>
        public Graph(float[] data, int x, int y, int wavelength)
        {
            InitializeComponent();

            this.Title = "Graph for pixel (" + x + ", " + y + ")";

            this.data = data;
            this.x = x;
            this.y = y;

            // Create the plot model
            var tmp = new PlotModel { Title = "Spectrum", Subtitle = "for (" + x + ", " + y + ")" };

            // Create two line series (markers are hidden by default)
            var series1 = new OxyPlot.Series.LineSeries { Title = "Pixel values for (" + x + ", " + y + ")", MarkerType = MarkerType.Circle };
            for (int i = 0; i < data.Length; i++)
            {
                series1.Points.Add(new DataPoint(LOWEST_WAVELENGTH + (i* WAVELENGTH_INCREMENT), data[i]));
            }

            // Add the series to the plot model
            tmp.Series.Add(series1);
            
            // Set the titles for the x-axis and y-axis
            tmp.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = AxisPosition.Bottom, Title = "Wavelengths (nm)",  TitleColor = OxyColors.Red });
            tmp.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = AxisPosition.Left,   Title = "Pixel values", TitleColor = OxyColors.Blue });

            // Add an annotation that indicates which wavelength was clicked on
            tmp.Annotations.Add(new OxyPlot.Annotations.LineAnnotation { Type = LineAnnotationType.Vertical, X = wavelength, MaximumY = 255, Color = OxyColors.Green, Text = "Wavelength: " + wavelength + " nm" });

            // Set the Model property, the INotifyPropertyChanged event will make the WPF Plot control update its content
            this.Model = tmp;
            this.DataContext = this;

            this.graphType = GraphType.PIXEL;
        }

        /// <summary>
        /// Constructor for graphing an area
        /// </summary>
        /// <param name="data"></param>
        /// <param name="topLeft"></param>
        /// <param name="bottomRight"></param>
        /// <param name="wavelength"></param>
        public Graph(float[] data, Point topLeft, Point bottomRight, int wavelength)
        {
            InitializeComponent();

            this.Title = "Graph for area between (" + topLeft.X + ", " + topLeft.Y + ") and (" + bottomRight.X + ", " + bottomRight.Y + ")";

            this.data = data;
            this.topLeft = topLeft;
            this.bottomRight = bottomRight;

            // Create the plot model
            var tmp = new PlotModel { Title = "Spectrum", Subtitle = "for area between (" + topLeft.X + ", " + topLeft.Y + ") and (" + bottomRight.X + ", " + bottomRight.Y + ")" };

            // Create two line series (markers are hidden by default)
            var series1 = new OxyPlot.Series.LineSeries { Title = "Pixel values for area between (" + topLeft.X + ", " + topLeft.Y + ") and (" + bottomRight.X + ", " + bottomRight.Y + ")", MarkerType = MarkerType.Circle };
            for (int i = 0; i < data.Length; i++)
            {
                series1.Points.Add(new DataPoint(LOWEST_WAVELENGTH + (i * WAVELENGTH_INCREMENT), data[i]));
            }

            // Add the series to the plot model
            tmp.Series.Add(series1);

            // Set the titles for the x-axis and y-axis
            tmp.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = AxisPosition.Bottom, Title = "Wavelengths (nm)",      TitleColor = OxyColors.Red });
            tmp.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = AxisPosition.Left,   Title = "Avg Pixel values", TitleColor = OxyColors.Blue });

            // Add an annotation that indicates which wavelength was clicked on
            tmp.Annotations.Add(new OxyPlot.Annotations.LineAnnotation { Type = LineAnnotationType.Vertical, X = wavelength, MaximumY = 255, Color = OxyColors.Green, Text = "Wavelength: " + wavelength + " nm" });

            // Set the Model property, the INotifyPropertyChanged event will make the WPF Plot control update its content
            this.Model = tmp;
            this.DataContext = this;

            this.graphType = GraphType.AREA;
        }
        
        /// <summary>
        /// "Save as" button logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG (*.png)|*.png|PDF (*.pdf)|*.pdf|SVG (*.svg)|*.svg|Spreadsheet (*.csv)|*.csv";
            saveFileDialog.FilterIndex = 1;

            if (saveFileDialog.ShowDialog() == true)
            {
                ExportSelection exportSelection = (ExportSelection)saveFileDialog.FilterIndex;
                switch (exportSelection)
                {
                    case ExportSelection.PNG:
                        SaveAsPNG(saveFileDialog.FileName);
                        break;
                    case ExportSelection.PDF:
                        SaveAsPDF(saveFileDialog.FileName);
                        break;
                    case ExportSelection.SVG:
                        SaveAsSVG(saveFileDialog.FileName);
                        break;
                    case ExportSelection.CSV:
                        SaveAsCSV(saveFileDialog.FileName);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Saves the graph as a .png
        /// </summary>
        /// <param name="path"></param>
        private void SaveAsPNG(string path)
        {
            PngExporter pngExporter = new PngExporter { Width = (int)this.ActualWidth, Height = (int)this.ActualHeight, Background = OxyColors.White };
            pngExporter.ExportToFile(Model, path);
        }

        /// <summary>
        /// Saves the graph as a .pdf
        /// </summary>
        /// <param name="fileName"></param>
        private void SaveAsPDF(string fileName)
        {
            using (var stream = File.Create(fileName))
            {
                PdfExporter pdfExporter = new PdfExporter { Width = (int)this.ActualWidth, Height = (int)this.ActualHeight };
                pdfExporter.Export(Model, stream);
            }
        }

        /// <summary>
        /// Saves the graph as an .svg
        /// </summary>
        /// <param name="fileName"></param>
        private void SaveAsSVG(string fileName)
        {
            using (var stream = File.Create(fileName))
            {
                OxyPlot.SvgExporter exporter = new OxyPlot.SvgExporter { Width = 600, Height = 400 };
                exporter.Export(Model, stream);
            }
        }

        /// <summary>
        /// Saves the graph as a spreadsheet (.csv)
        /// </summary>
        /// <param name="fileName"></param>
        private void SaveAsCSV(string fileName)
        {
            // Write sample data to CSV file
            using (CsvFileWriter writer = new CsvFileWriter(fileName))
            {
                for (int i = 0; i < data.Length + 1; i++)
                {
                    CsvRow row = new CsvRow();
                    for (int j = 0; j < 2; j++)
                    {
                        if (i == 0 && j == 0)
                        {
                            row.Add("wavelength");
                        }
                        else if (i == 0 && j == 1)
                        {
                            if (graphType == GraphType.AREA)
                            {
                                row.Add("avg pixel value for area between (" + topLeft.X + ", " + topLeft.Y + 
                                    ") and (" + bottomRight.X + ", " + bottomRight.Y + ")");
                            }
                            else if (graphType == GraphType.PIXEL)
                            {
                                row.Add("pixel value");
                            }
                        }
                        else if (i != 0 && j == 0)
                        {
                            int wavelength = LOWEST_WAVELENGTH + ((i - 1) * WAVELENGTH_INCREMENT);
                            row.Add(wavelength.ToString());
                        }
                        else if (i != 0 && j == 1)
                        {
                            row.Add(data[i - 1].ToString());
                        }
                    }
                    writer.WriteRow(row);
                }
            }
        }
    }
}
