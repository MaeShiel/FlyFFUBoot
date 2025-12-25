using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;

namespace FlyFFBot
{
    public partial class SettingsWindow : Window
    {
        private MainWindow? _mainWindow;

        public SettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load saved HP region coordinates
            HpMinX.Text = Properties.Settings.Default.HpMinX.ToString();
            HpMaxX.Text = Properties.Settings.Default.HpMaxX.ToString();
            HpMinY.Text = Properties.Settings.Default.HpMinY.ToString();
            HpMaxY.Text = Properties.Settings.Default.HpMaxY.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save HP region coordinates
            if (int.TryParse(HpMinX.Text, out int minX) && 
                int.TryParse(HpMaxX.Text, out int maxX) &&
                int.TryParse(HpMinY.Text, out int minY) && 
                int.TryParse(HpMaxY.Text, out int maxY))
            {
                if (minX >= maxX || minY >= maxY)
                {
                    MessageBox.Show("Min values must be less than Max values.", "Invalid Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Properties.Settings.Default.HpMinX = minX;
                Properties.Settings.Default.HpMaxX = maxX;
                Properties.Settings.Default.HpMinY = minY;
                Properties.Settings.Default.HpMaxY = maxY;
                Properties.Settings.Default.Save();
                
                MessageBox.Show($"Settings saved!\nHP Region: ({minX},{minY}) to ({maxX},{maxY})", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            else
            {
                MessageBox.Show("Please enter valid numeric coordinates for HP region.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void CaptureHpRegion_Click(object sender, RoutedEventArgs e)
        {
            // Validate coordinates
            if (!int.TryParse(HpMinX.Text, out int minX) || 
                !int.TryParse(HpMaxX.Text, out int maxX) ||
                !int.TryParse(HpMinY.Text, out int minY) || 
                !int.TryParse(HpMaxY.Text, out int maxY))
            {
                MessageBox.Show("Please enter valid coordinates before capturing.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (minX >= maxX || minY >= maxY)
            {
                MessageBox.Show("Min values must be less than Max values.", "Invalid Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_mainWindow == null)
            {
                MessageBox.Show("Cannot access browser window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Calculate dimensions
                int width = maxX - minX;
                int height = maxY - minY;

                // Capture from browser
                var screenshot = await _mainWindow.CaptureBrowserRegionAsync(minX, minY, width, height);
                
                if (screenshot == null)
                {
                    MessageBox.Show("Failed to capture browser region.", "Capture Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create Screenshots folder if it doesn't exist
                string screenshotFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                Directory.CreateDirectory(screenshotFolder);

                // Generate filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string filename = $"HP_Region_{timestamp}.png";
                string fullPath = Path.Combine(screenshotFolder, filename);

                // Save to file
                screenshot.Save(fullPath, ImageFormat.Png);

                // Convert to BitmapImage for WPF preview
                using (MemoryStream memory = new MemoryStream())
                {
                    screenshot.Save(memory, ImageFormat.Png);
                    memory.Position = 0;
                    
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    ScreenshotPreview.Source = bitmapImage;
                    ScreenshotPreviewBorder.Visibility = Visibility.Visible;
                }

                screenshot.Dispose();
                
                MessageBox.Show($"Region captured from browser!\nSize: {width}x{height} pixels\n\nSaved to:\n{fullPath}", 
                              "Capture Complete", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to capture region: {ex.Message}", "Capture Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
