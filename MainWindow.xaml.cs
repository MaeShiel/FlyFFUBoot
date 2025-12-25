using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace FlyFFBot
{
    public partial class MainWindow : Window
    {
        private bool isDarkTheme = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeBrowser();
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
        string settingsHtml = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {
            margin: 0;
            padding: 20px;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
        }
        .close-btn {
            position: fixed;
            top: 20px;
            right: 20px;
            width: 40px;
            height: 40px;
            background: #f56565;
            color: white;
            border: none;
            border-radius: 50%;
            font-size: 24px;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            box-shadow: 0 4px 6px rgba(0,0,0,0.3);
            transition: all 0.3s ease;
            z-index: 1000;
        }
        .close-btn:hover {
            background: #e53e3e;
            transform: scale(1.1);
        }
        .container {
            max-width: 900px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            overflow: hidden;
        }
        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            text-align: center;
        }
        .header h1 {
            margin: 0;
            font-size: 32px;
            font-weight: 600;
        }
        .tabs {
            display: flex;
            background: #f7fafc;
            border-bottom: 2px solid #e2e8f0;
        }
        .tab {
            flex: 1;
            padding: 15px 20px;
            text-align: center;
            cursor: pointer;
            background: #f7fafc;
            border: none;
            font-size: 16px;
            font-weight: 500;
            color: #4a5568;
            transition: all 0.3s ease;
        }
        .tab:hover {
            background: #edf2f7;
        }
        .tab.active {
            background: white;
            color: #667eea;
            border-bottom: 3px solid #667eea;
        }
        .tab-content {
            display: none;
            padding: 30px;
        }
        .tab-content.active {
            display: block;
        }
        .setting-group {
            margin-bottom: 25px;
        }
        .setting-group label {
            display: block;
            font-weight: 600;
            color: #2d3748;
            margin-bottom: 8px;
        }
        .setting-group input, .setting-group select {
            width: 100%;
            padding: 10px 12px;
            border: 2px solid #e2e8f0;
            border-radius: 6px;
            font-size: 14px;
            transition: border-color 0.3s ease;
        }
        .setting-group input:focus, .setting-group select:focus {
            outline: none;
            border-color: #667eea;
        }
        .button-group {
            display: flex;
            gap: 10px;
            padding: 20px 30px;
            background: #f7fafc;
            border-top: 1px solid #e2e8f0;
        }
        .btn {
            flex: 1;
            padding: 12px 24px;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
        }
        .btn-save {
            background: #48bb78;
            color: white;
        }
        .btn-save:hover {
            background: #38a169;
            transform: translateY(-2px);
        }
        .btn-cancel {
            background: #f56565;
            color: white;
        }
        .btn-cancel:hover {
            background: #e53e3e;
            transform: translateY(-2px);
        }
    </style>
</head>
<body>
    <button class='close-btn' onclick='window.location.href=""https://universe.flyff.com""'>×</button>
    
    <div class='container'>
        <div class='header'>
            <h1>⚙️ Settings</h1>
        </div>
        
        <div class='tabs'>
            <button class='tab active' onclick='switchTab(0)'>General</button>
            <button class='tab' onclick='switchTab(1)'>Bot Config</button>
            <button class='tab' onclick='switchTab(2)'>Advanced</button>
        </div>
        
        <div class='tab-content active' id='tab0'>
            <div class='setting-group'>
                <label>Theme</label>
                <select>
                    <option>Light</option>
                    <option>Dark</option>
                    <option>Auto</option>
                </select>
            </div>
            <div class='setting-group'>
                <label>Language</label>
                <select>
                    <option>English</option>
                    <option>Korean</option>
                    <option>Japanese</option>
                </select>
            </div>
            <div class='setting-group'>
                <label>Auto Start Bot</label>
                <select>
                    <option>Disabled</option>
                    <option>Enabled</option>
                </select>
            </div>
        </div>
        
        <div class='tab-content' id='tab1'>
            <div class='setting-group'>
                <label>HP Threshold (%)</label>
                <input type='number' value='50' min='0' max='100'>
            </div>
            <div class='setting-group'>
                <label>MP Threshold (%)</label>
                <input type='number' value='30' min='0' max='100'>
            </div>
            <div class='setting-group'>
                <label>Attack Range</label>
                <input type='number' value='100' min='10' max='500'>
            </div>
            <div class='setting-group'>
                <label>Auto Pickup</label>
                <select>
                    <option>Disabled</option>
                    <option>Enabled</option>
                </select>
            </div>
        </div>
        
        <div class='tab-content' id='tab2'>
            <div class='setting-group'>
                <label>Detection Sensitivity</label>
                <input type='range' min='1' max='10' value='5'>
            </div>
            <div class='setting-group'>
                <label>Debug Mode</label>
                <select>
                    <option>Disabled</option>
                    <option>Enabled</option>
                </select>
            </div>
            <div class='setting-group'>
                <label>Log Level</label>
                <select>
                    <option>Info</option>
                    <option>Warning</option>
                    <option>Error</option>
                    <option>Debug</option>
                </select>
            </div>
        </div>
        
        <div class='button-group'>
            <button class='btn btn-save' onclick='saveSettings()'>Save Settings</button>
            <button class='btn btn-cancel' onclick='window.location.href=""https://universe.flyff.com""'>Cancel</button>
        </div>
    </div>
    
    <script>
        function switchTab(index) {
            const tabs = document.querySelectorAll('.tab');
            const contents = document.querySelectorAll('.tab-content');
            
            tabs.forEach((tab, i) => {
                if (i === index) {
                    tab.classList.add('active');
                } else {
                    tab.classList.remove('active');
                }
            });
            
            contents.forEach((content, i) => {
                if (i === index) {
                    content.classList.add('active');
                } else {
                    content.classList.remove('active');
                }
            });
        }
        
        function saveSettings() {
            alert('Settings saved successfully!');
            window.location.href='https://universe.flyff.com';
        }
    </script>
</body>
</html>";
        
        BrowserView.NavigateToString(settingsHtml);
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
