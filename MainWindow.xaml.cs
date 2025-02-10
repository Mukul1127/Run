using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Microsoft.UI.Xaml.Input;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Run
{
    public sealed partial class MainWindow : Window
    {
        // Window Properties
        public const int X = 10;
        public const int Y = 10;
        public const int W = 416;
        public const int H = 216;

        private readonly nint hwnd;
        private readonly Regex environmentVariableRegex = new(@"%(\w+)%|\$Env:(\w+)");

        public MainWindow()
        {
            this.InitializeComponent();
            this.hwnd = WindowNative.GetWindowHandle(this);
            AppWindow appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(this.hwnd));
            if (appWindow == null)
            {
                throw new Exception("Unable to customize window: Failed to get window");
            }

            // Get display height
            var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(this.hwnd), DisplayAreaFallback.Nearest);
            int screenHeight = (int)displayArea.WorkArea.Height;

            // Position from BOTTOM LEFT corner
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(X, screenHeight - Y - H, W, H));

            // Set icon
            appWindow.SetIcon("Assets/Logo.ico");

            // Disable resizing and remove maximize and minimize buttons
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            // Add listeners to txtRunCommand
            KeyboardAccelerator ctrlShiftEnter = new KeyboardAccelerator();
            ctrlShiftEnter.Key = VirtualKey.Enter;
            ctrlShiftEnter.Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift;
            ctrlShiftEnter.Invoked += CtrlShiftEnter_Invoked;
            txtRunCommand.KeyboardAccelerators.Add(ctrlShiftEnter);
            KeyboardAccelerator enter = new KeyboardAccelerator();
            enter.Key = VirtualKey.Enter;
            enter.Invoked += Enter_Invoked;
            txtRunCommand.KeyboardAccelerators.Add(enter);
        }

        private void TxtRunCommand_Loaded(object sender, RoutedEventArgs e)
        {
            txtRunCommand.Focus(FocusState.Programmatic);
        }

        private void TxtRunCommand_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                Close();
            }
        }

        private void TxtRunCommand_TextChanged(object sender, RoutedEventArgs e)
        {
            this.btnOk.IsEnabled = !String.IsNullOrWhiteSpace(txtRunCommand.Text);
        }

        private void Enter_Invoked(object sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            RunCommand(txtRunCommand.Text.Trim().Split(" "));
            args.Handled = true;
        }

        private void CtrlShiftEnter_Invoked(object sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            RunCommand(txtRunCommand.Text.Trim().Split(" "), true);
            args.Handled = true;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string[] command = txtRunCommand.Text.Trim().Split(" ");
            RunCommand(command);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new();
            picker.FileTypeFilter.Add("*");

            InitializeWithWindow.Initialize(picker, this.hwnd);

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                txtRunCommand.Text = file.Path;
            }
        }

        private async void ShowErrorDialog(string message)
        {
            // TODO: Remove
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await errorDialog.ShowAsync();
        }

        private void RunCommand(string[] command, bool elevated = false)
        {
            if (command.Length == 0)
            {
                ShowErrorDialog("Unable to start process: Invalid command recived");
            }

            string commandString = ExpandEnvironmentVariables(String.Join(" ", command));

            if (Directory.Exists(commandString) || File.Exists(commandString) || Uri.IsWellFormedUriString(commandString, UriKind.Absolute))
            {
                StartProcess("explorer", commandString, elevated);
                return;
            }

            StartProcess(command[0], ExpandEnvironmentVariables(String.Join(" ", command.Skip(1))), elevated);
        }

        private void StartProcess(string exe, string arguments, bool elevated = false)
        {
            try
            {
                ProcessStartInfo info = new()
                {
                    UseShellExecute = true,
                    FileName = exe,
                    Arguments = arguments,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
                if (elevated)
                {
                    info.Verb = "runas";
                }
                using Process? process = Process.Start(info);
                if (process == null)
                {
                    throw new Exception("Unable to start process: No process resource started");
                }
                Close();
            }
            catch (Exception ex)
            {
                ShowErrorDialog(ex.Message + " Elevated: " + elevated);
            }
        }

        private string ExpandEnvironmentVariables(string command)
        {
            return this.environmentVariableRegex.Replace(command, match =>
            {
                string variable = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                return Environment.GetEnvironmentVariable(variable) ?? match.Value;
            });
        }
    }
}
