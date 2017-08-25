using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Windows;

namespace HyperSpectralWPF
{
    /// <summary>
    /// Interaction logic for HighlightMenu.xaml
    /// </summary>
    public partial class HighlightMenu
    {
        /// <summary>
        /// Represents the two different conditions that
        /// affect which pixels are highlighted.
        /// </summary>
        public enum HighlightCondition
        {
            ABOVE,
            BELOW
        };
        private HighlightCondition highlightCondition;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public HighlightMenu()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Returns the input value.
        /// </summary>
        /// <returns></returns>
        public float GetValue()
        {
            return float.Parse(ThresholdBox.Text);
        }

        /// <summary>
        /// Returns if values above or below the threshold
        /// are highlighted.
        /// </summary>
        /// <returns></returns>
        public HighlightCondition GetHighlightCondition()
        {
            return highlightCondition;
        }

        /// <summary>
        /// Checks if input is numeric,
        /// if it isn't, then it isn't registered as input.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        /// <summary>
        /// If the input is less than or equal to 255,
        /// then the dialog closes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ThresholdBox.Text != "" && float.Parse(ThresholdBox.Text) <= 255)
            {
                DialogResult = true;
                this.Close();
            }
        }

        /// <summary>
        /// Checkbox unchecked handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Above_Checked(object sender, RoutedEventArgs e)
        {
            highlightCondition = HighlightCondition.ABOVE;
        }

        /// <summary>
        /// Checkbox checked handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Below_Checked(object sender, RoutedEventArgs e)
        {
            highlightCondition = HighlightCondition.BELOW;
        }
    }
}
