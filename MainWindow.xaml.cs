using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;

namespace FlyFFBot
{
    public partial class MainWindow : Window
    {
        private bool isDarkTheme = false;
        private DispatcherTimer? hpDetectionTimer;
        private bool isHpDetectionEnabled = false;
        private int maxHpBarWidth = 0; // Track the maximum width seen (100% HP)
        private int maxBufferHpBarWidth = 0; // Track buffer HP maximum width
        private bool isDetectionInProgress = false; // Prevent overlapping detection cycles
        private static SemaphoreSlim captureSemaphore = new SemaphoreSlim(1, 1); // Ensure only one capture at a time
        private static SemaphoreSlim processingSemaphore = new SemaphoreSlim(1, 1); // Ensure only one bitmap processing at a time

        public MainWindow()
        {
            InitializeComponent();
            InitializeBrowser();
            InitializeHpDetection();
        }

        private void InitializeHpDetection()
        {
            hpDetectionTimer = new DispatcherTimer();
            hpDetectionTimer.Interval = TimeSpan.FromMilliseconds(1000); // Check every 1 second (reduced frequency)
            hpDetectionTimer.Tick += async (s, e) => 
            {
                // Skip if previous detection is still running
                if (isDetectionInProgress)
                {
                    return;
                }

                isDetectionInProgress = true;
                try
                {
                    await DetectAndUpdateHp();
                    await DetectAndUpdateBufferHp();
                }
                catch (Exception ex)
                {
                    LogActivity($"Error in detection: {ex.Message}");
                }
                finally
                {
                    isDetectionInProgress = false;
                }
            };
        }

        public void StartHpDetection()
        {
            if (!isHpDetectionEnabled)
            {
                isHpDetectionEnabled = true;
                hpDetectionTimer?.Start();
            }
        }

        public void StopHpDetection()
        {
            if (isHpDetectionEnabled)
            {
                isHpDetectionEnabled = false;
                hpDetectionTimer?.Stop();
            }
        }

        private async Task DetectAndUpdateHp()
        {
            try
            {
                // Get HP region coordinates from settings
                int minX = Properties.Settings.Default.HpMinX;
                int maxX = Properties.Settings.Default.HpMaxX;
                int minY = Properties.Settings.Default.HpMinY;
                int maxY = Properties.Settings.Default.HpMaxY;

                if (minX >= maxX || minY >= maxY)
                {
                    LogActivity($"Invalid HP region: ({minX},{minY}) to ({maxX},{maxY})");
                    return; // Invalid region
                }

                // Capture HP region
                int width = maxX - minX;
                int height = maxY - minY;
                
                using (var bitmap = await CaptureBrowserRegionAsync(minX, minY, width, height))
                {
                    if (bitmap == null)
                    {
                        LogActivity($"Failed to capture Character HP region: ({minX},{minY}) size: {width}x{height}");
                        return;
                    }

                    // Analyze HP bar using width-based detection (like neuz-main)
                    // Find the rightmost red pixel to determine HP bar width
                    int hpBarWidth = 0;
                    int leftmostX = bitmap.Width;
                    int rightmostX = 0;

                    // Multiple red color references for HP bar (from neuz-main)
                    var hpColors = new[]
                    {
                        (174, 18, 55),
                        (188, 24, 62),
                        (204, 30, 70),
                        (220, 36, 78)
                    };

                    // OPTIMIZED: Use LockBits for direct pixel buffer access (50-100x faster than GetPixel)
                    var bitmapData = bitmap.LockBits(
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);

                    await processingSemaphore.WaitAsync(); // Wait for exclusive pixel processing
                    try
                    {
                        unsafe
                        {
                            byte* ptr = (byte*)bitmapData.Scan0;
                            int stride = bitmapData.Stride;
                            for (int y = 0; y < bitmap.Height; y++)
                            {
                                int localLeftmost = bitmap.Width;
                                int localRightmost = 0;
                                for (int x = 0; x < bitmap.Width; x++)
                                {
                                    int index = y * stride + x * 4;
                                    byte b = ptr[index];
                                    byte g = ptr[index + 1];
                                    byte r = ptr[index + 2];
                                    bool isHpPixel = false;
                                    foreach (var (hr, hg, hb) in hpColors)
                                    {
                                        if (Math.Abs(r - hr) <= 2 &&
                                            Math.Abs(g - hg) <= 2 &&
                                            Math.Abs(b - hb) <= 2)
                                        {
                                            isHpPixel = true;
                                            break;
                                        }
                                    }
                                    if (isHpPixel)
                                    {
                                        if (x < localLeftmost) localLeftmost = x;
                                        if (x > localRightmost) localRightmost = x;
                                    }
                                }
                                if (localLeftmost < bitmap.Width)
                                {
                                    if (localLeftmost < leftmostX) leftmostX = localLeftmost;
                                    if (localRightmost > rightmostX) rightmostX = localRightmost;
                                }
                            }
                        }
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                        processingSemaphore.Release(); // Release processing lock
                    }

                    // Calculate HP bar width
                    if (rightmostX > leftmostX)
                    {
                        hpBarWidth = rightmostX - leftmostX + 1;
                    }

                    // Update max width if we found a larger width (likely 100% HP)
                    if (hpBarWidth > maxHpBarWidth)
                    {
                        maxHpBarWidth = hpBarWidth;
                    }

                    // Calculate HP percentage based on width ratio
                    double hpPercentage = 0;
                    
                    if (hpBarWidth == 0)
                    {
                        // No red pixels detected - HP is empty
                        hpPercentage = 0;
                    }
                    else if (maxHpBarWidth > 0 && hpBarWidth > 0)
                    {
                        // Calculate based on max width seen
                        hpPercentage = ((double)hpBarWidth / maxHpBarWidth) * 100.0;
                    }
                    else if (hpBarWidth > 0)
                    {
                        // First detection, assume it's close to full
                        hpPercentage = 100.0;
                        maxHpBarWidth = hpBarWidth;
                    }
                    
                    // Update UI on main thread
                    UpdateHpBar(hpPercentage);
                }
            }
            catch (Exception ex)
            {
                LogActivity($"Error detecting HP: {ex.Message}");
            }
        }

        private void UpdateHpBar(double hpPercentage)
        {
            // Clamp between 0 and 100
            hpPercentage = Math.Max(0, Math.Min(100, hpPercentage));

            // Update HP bar width based on the parent grid's actual width
            if (HPBarGrid.ActualWidth > 0)
            {
                if (hpPercentage == 0)
                {
                    // No HP - hide the bar completely
                    HPBar.Width = 0;
                }
                else
                {
                    double targetWidth = HPBarGrid.ActualWidth * (hpPercentage / 100.0);
                    HPBar.Width = Math.Max(1, targetWidth); // Minimum 1 pixel to be visible when HP > 0
                }
            }

            // Update HP text
            HPText.Text = $"{(int)hpPercentage}%";

            // Change color based on HP level
            WpfBrush barColor;
            if (hpPercentage >= 70)
            {
                barColor = new WpfBrush(WpfColor.FromRgb(72, 187, 120)); // Green #48BB78
            }
            else if (hpPercentage >= 40)
            {
                barColor = new WpfBrush(WpfColor.FromRgb(237, 137, 54)); // Orange #ED8936
            }
            else if (hpPercentage >= 20)
            {
                barColor = new WpfBrush(WpfColor.FromRgb(245, 101, 101)); // Light Red #F56565
            }
            else
            {
                barColor = new WpfBrush(WpfColor.FromRgb(229, 62, 62)); // Dark Red #E53E3E
            }

            HPBar.Background = barColor;
        }

        private async Task DetectAndUpdateBufferHp()
        {
            try
            {
                // Buffer HP region coordinates
                int minX = 111;
                int maxX = 216;
                int minY = 34;
                int maxY = 47;

                if (minX >= maxX || minY >= maxY)
                {
                    return; // Invalid region
                }

                // Capture Buffer HP region
                int width = maxX - minX;
                int height = maxY - minY;
                
                using (var bitmap = await CaptureBrowserRegionAsync(minX, minY, width, height))
                {
                    if (bitmap == null)
                    {
                        return;
                    }

                    // Analyze HP bar using width-based detection (like main HP)
                    // Find the rightmost red pixel to determine HP bar width
                    int hpBarWidth = 0;
                    int leftmostX = bitmap.Width;
                    int rightmostX = 0;

                    // Multiple red color references for HP bar
                    var hpColors = new[]
                    {
                        (174, 18, 55),
                        (188, 24, 62),
                        (204, 30, 70),
                        (220, 36, 78)
                    };

                    // OPTIMIZED: Use LockBits for direct pixel buffer access
                    var bitmapData = bitmap.LockBits(
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);

                    await processingSemaphore.WaitAsync(); // Wait for exclusive pixel processing
                    try
                    {
                        unsafe
                        {
                            byte* ptr = (byte*)bitmapData.Scan0;
                            int stride = bitmapData.Stride;
                            for (int y = 0; y < bitmap.Height; y++)
                            {
                                int localLeftmost = bitmap.Width;
                                int localRightmost = 0;
                                for (int x = 0; x < bitmap.Width; x++)
                                {
                                    int index = y * stride + x * 4;
                                    byte b = ptr[index];
                                    byte g = ptr[index + 1];
                                    byte r = ptr[index + 2];
                                    bool isHpPixel = false;
                                    foreach (var (hr, hg, hb) in hpColors)
                                    {
                                        if (Math.Abs(r - hr) <= 2 &&
                                            Math.Abs(g - hg) <= 2 &&
                                            Math.Abs(b - hb) <= 2)
                                        {
                                            isHpPixel = true;
                                            break;
                                        }
                                    }
                                    if (isHpPixel)
                                    {
                                        if (x < localLeftmost) localLeftmost = x;
                                        if (x > localRightmost) localRightmost = x;
                                    }
                                }
                                if (localLeftmost < bitmap.Width)
                                {
                                    if (localLeftmost < leftmostX) leftmostX = localLeftmost;
                                    if (localRightmost > rightmostX) rightmostX = localRightmost;
                                }
                            }
                        }
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                        processingSemaphore.Release(); // Release processing lock
                    }

                    // Calculate HP bar width
                    if (rightmostX > leftmostX)
                    {
                        hpBarWidth = rightmostX - leftmostX + 1;
                    }

                    // Update max width if we found a larger width (likely 100% HP)
                    if (hpBarWidth > maxBufferHpBarWidth)
                    {
                        maxBufferHpBarWidth = hpBarWidth;
                    }

                    // Calculate HP percentage based on width ratio
                    double hpPercentage = 0;
                    
                    if (hpBarWidth == 0)
                    {
                        // No red pixels detected - HP is empty
                        hpPercentage = 0;
                    }
                    else if (maxBufferHpBarWidth > 0 && hpBarWidth > 0)
                    {
                        // Calculate based on max width seen
                        hpPercentage = ((double)hpBarWidth / maxBufferHpBarWidth) * 100.0;
                    }
                    else if (hpBarWidth > 0)
                    {
                        // First detection, assume it's close to full
                        hpPercentage = 100.0;
                        maxBufferHpBarWidth = hpBarWidth;
                    }
                    
                    // Update UI on main thread
                    UpdateBufferHpBar(hpPercentage);
                }
            }
            catch (Exception ex)
            {
                LogActivity($"Error detecting Buffer HP: {ex.Message}");
            }
        }

        private void UpdateBufferHpBar(double hpPercentage)
        {
            // Clamp between 0 and 100
            hpPercentage = Math.Max(0, Math.Min(100, hpPercentage));

            // Update Buffer HP bar width based on the parent grid's actual width
            if (BufferHPBarGrid.ActualWidth > 0)
            {
                if (hpPercentage == 0)
                {
                    // No HP - hide the bar completely
                    BufferHPBar.Width = 0;
                }
                else
                {
                    double targetWidth = BufferHPBarGrid.ActualWidth * (hpPercentage / 100.0);
                    BufferHPBar.Width = Math.Max(1, targetWidth); // Minimum 1 pixel to be visible when HP > 0
                }
            }

            // Update HP text
            BufferHPText.Text = $"{(int)hpPercentage}%";

            // Change color based on HP level
            WpfBrush barColor;
            if (hpPercentage >= 70)
            {
                barColor = new WpfBrush(WpfColor.FromRgb(72, 187, 120)); // Green #48BB78
            }
            else if (hpPercentage >= 40)
            {
                barColor = new WpfBrush(WpfColor.FromRgb(237, 137, 54)); // Orange #ED8936
            }
            else if (hpPercentage >= 20)
            {
                barColor = new WpfBrush(WpfColor.FromRgb(245, 101, 101)); // Light Red #F56565
            }
            else
            {
                barColor = new WpfBrush(WpfColor.FromRgb(229, 62, 62)); // Dark Red #E53E3E
            }

            BufferHPBar.Background = barColor;
        }

        private async void InitializeBrowser()
        {
            try
            {
                BrowserLoadingOverlay.Visibility = Visibility.Visible;
                await BrowserView.EnsureCoreWebView2Async(null);
                BrowserLoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                BrowserLoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Failed to initialize browser: {ex.Message}", "Browser Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            isDarkTheme = !isDarkTheme;
            SwitchTheme(isDarkTheme);
        }

        private void SwitchTheme(bool darkTheme)
        {
            var theme = darkTheme ? "DarkTheme.xaml" : "LightTheme.xaml";
            var themeUri = new System.Uri($"Themes/{theme}", System.UriKind.Relative);
            
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            mergedDictionaries.Clear();
            mergedDictionaries.Add(new ResourceDictionary() { Source = themeUri });

            // Update theme icon
            ThemeIcon.Text = darkTheme ? "☀️" : "🌙";
        }

        private void StartBot_Click(object sender, RoutedEventArgs e)
        {
            StartHpDetection();
            LogActivity("Bot has started");
        }

        private void StopBot_Click(object sender, RoutedEventArgs e)
        {
            StopHpDetection();
            LogActivity("Bot has stopped");
        }

        private void LogActivity(string message)
        {
            if (ActivityLog != null)
            {
                string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
                
                // Append to TextBox
                if (!string.IsNullOrEmpty(ActivityLog.Text))
                {
                    ActivityLog.AppendText(Environment.NewLine);
                }
                ActivityLog.AppendText(logEntry);
                
                // Auto-scroll to bottom
                ActivityLog.ScrollToEnd();
                
                // Keep log size manageable (max 100 lines)
                var lines = ActivityLog.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                if (lines.Length > 100)
                {
                    var newLines = lines.Skip(lines.Length - 100);
                    ActivityLog.Text = string.Join(Environment.NewLine, newLines);
                    ActivityLog.ScrollToEnd();
                }
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (ActivityLog != null)
            {
                ActivityLog.Clear();
                LogActivity("Log cleared");
            }
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            if (ActivityLog != null && !string.IsNullOrEmpty(ActivityLog.Text))
            {
                try
                {
                    Clipboard.SetText(ActivityLog.Text);
                    LogActivity("Log copied to clipboard");
                }
                catch (Exception ex)
                {
                    LogActivity($"Failed to copy log: {ex.Message}");
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Open settings in a separate window instead of navigating the browser
            var settingsWindow = new SettingsWindow(this);
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        public async Task<Bitmap?> CaptureBrowserRegionAsync(int x, int y, int width, int height)
        {
            // Wait for semaphore to ensure only one capture at a time
            await captureSemaphore.WaitAsync();
            try
            {
                if (BrowserView.CoreWebView2 == null)
                {
                    return null;
                }

                // Capture the entire browser view
                using (var stream = new MemoryStream())
                {
                    await BrowserView.CoreWebView2.CapturePreviewAsync(
                        CoreWebView2CapturePreviewImageFormat.Png,
                        stream);

                    stream.Position = 0;
                    
                    // Load the full screenshot
                    using (var fullBitmap = new Bitmap(stream))
                    {
                        // Validate region bounds
                        if (x < 0 || y < 0 || 
                            x + width > fullBitmap.Width || 
                            y + height > fullBitmap.Height)
                        {
                            return null;
                        }

                        // Create a new bitmap for the cropped region (deep copy to avoid shared resources)
                        var croppedBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        using (var graphics = Graphics.FromImage(croppedBitmap))
                        {
                            graphics.DrawImage(fullBitmap, 
                                new Rectangle(0, 0, width, height),
                                new Rectangle(x, y, width, height),
                                GraphicsUnit.Pixel);
                        }
                        
                        return croppedBitmap;
                    }
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                // Always release the semaphore
                captureSemaphore.Release();
            }
        }
    
    private void AdminButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to admin page in the same browser view
            var adminHtml = @"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Admin Panel</title>
                    <style>
                        body { 
                            font-family: Arial, sans-serif; 
                            padding: 20px; 
                            background: #1a202c; 
                            color: #fff;
                            margin: 0;
                        }
                        .close-btn {
                            position: fixed;
                            top: 20px;
                            right: 20px;
                            background: #f56565;
                            color: white;
                            border: none;
                            width: 40px;
                            height: 40px;
                            border-radius: 50%;
                            font-size: 24px;
                            cursor: pointer;
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            box-shadow: 0 4px 6px rgba(0,0,0,0.3);
                            transition: all 0.2s;
                        }
                        .close-btn:hover {
                            background: #e53e3e;
                            transform: scale(1.1);
                        }
                        .content {
                            max-width: 800px;
                            margin: 0 auto;
                            padding: 20px;
                        }
                        h1 { color: #9f7aea; }
                        p { color: #a0aec0; }
                    </style>
                </head>
                <body>
                    <button class='close-btn' onclick='window.location.href=""https://universe.flyff.com""'>×</button>
                    <div class='content'>
                        <h1>Admin Panel</h1>
                        <p>Development tools and testing area</p>
                        <p>Click the × button to return to the game</p>
                    </div>
                </body>
                </html>
            ";
            
            BrowserView.CoreWebView2.NavigateToString(adminHtml);
        }
    }
}
