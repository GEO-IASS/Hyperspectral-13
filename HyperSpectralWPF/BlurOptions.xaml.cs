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

namespace HyperSpectralWPF
{
    /// <summary>
    /// Interaction logic for BlurOptions.xaml
    /// </summary>
    public partial class BlurOptions
    {
        public enum BlurChoice
        {
            NONE,
            MOSAIC,
            BOX
        };
        private BlurChoice blurChoice = BlurChoice.NONE;

        public BlurOptions()
        {
            InitializeComponent();
        }

        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            // ... A List.
            List<string> data = new List<string>();
            data.Add("Mosaic blur");
            data.Add("Box blur");

            // Get the ComboBox reference.
            var comboBox = sender as ComboBox;

            // Assign the ItemsSource to the List.
            comboBox.ItemsSource = data;

            // Make the first item selected.
            comboBox.SelectedIndex = 0;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the ComboBox.
            var comboBox = sender as ComboBox;

            // Set SelectedItem as Window Title.
            string value = comboBox.SelectedItem as string;
            
            switch (value)
            {
                case "Mosaic blur":
                    blurChoice = BlurChoice.MOSAIC;
                    break;
                case "Box blur":
                    blurChoice = BlurChoice.BOX;
                    break;
            }
        }

        public BlurChoice GetChoice()
        {
            return blurChoice;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }
    }
}
