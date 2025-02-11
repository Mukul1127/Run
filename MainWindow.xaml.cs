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
using System.Linq;
using System.Text.RegularExpressions;
using WinUIEx;

namespace Run
{
    public sealed partial class MainWindow : WinUIEx.WindowEx
    {
        // Window Properties
        public const int XOffset = 10;
        public const int YOffset = 10;

        private readonly nint hwnd;
        private static readonly Regex environmentVariableRegex = new(@"%(\w+)%|\$Env:(\w+)");
        private static readonly string[] ExecutableExtensions = { ".exe", ".com", ".pif", ".bat", ".cmd", ".ps1", ".msc" };

        public MainWindow()
        {
            InitializeComponent();
            hwnd = WindowNative.GetWindowHandle(this);

            // Get display height
            var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd), DisplayAreaFallback.Nearest);
            int screenHeight = (int)displayArea.WorkArea.Height;

            // Position from BOTTOM LEFT corner
            this.Move(XOffset, screenHeight - YOffset - 216);

            this.SetIcon("Assets/Logo.ico");

            // Fix being able to double click titlebar to maximize even when disabled
            NativeWindowHelper.ForceDisableMaximize(this);

            // Add listeners to TextBoxRun
            KeyboardAccelerator enter = new()
            {
                Key = VirtualKey.Enter
            };
            enter.Invoked += Enter_Invoked;
            TextBoxRun.KeyboardAccelerators.Add(enter);

            KeyboardAccelerator ctrlShiftEnter = new()
            {
                Key = VirtualKey.Enter,
                Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift
            };
            ctrlShiftEnter.Invoked += CtrlShiftEnter_Invoked;
            TextBoxRun.KeyboardAccelerators.Add(ctrlShiftEnter);
        }

        // TextBoxRun
        private void TextBoxRun_Loaded(object sender, RoutedEventArgs e)
        {
            TextBoxRun.Focus(FocusState.Programmatic);
        }

        private void TextBoxRun_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                Close();
            }
        }

        private void TextBoxRun_TextChanged(object sender, RoutedEventArgs e)
        {
            this.ButtonOk.IsEnabled = !String.IsNullOrWhiteSpace(TextBoxRun.Text);
        }

        private void Enter_Invoked(object sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            RunCommand(TextBoxRun.Text.Trim().Split(" "));
            args.Handled = true;
        }

        private void CtrlShiftEnter_Invoked(object sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            RunCommand(TextBoxRun.Text.Trim().Split(" "), true);
            args.Handled = true;
        }

        // ButtonOk
        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            string[] command = TextBoxRun.Text.Trim().Split(" ");
            RunCommand(command);
        }

        // Button Cancel
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ButtonBrowse
        private async void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new();
            picker.FileTypeFilter.Add("*");

            InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                TextBoxRun.Text = file.Path;
            }
        }

        // Helpers
        private async void ShowErrorDialog(string message)
        {
            // TODO: Replace with better solution
            ContentDialog errorDialog = new()
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

            if (!ExecutableExtensions.Contains(Path.GetExtension(commandString), StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(commandString) || File.Exists(commandString) || Uri.IsWellFormedUriString(commandString, UriKind.Absolute))
                {
                    StartProcess("explorer", commandString, elevated);
                    return;
                }
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
                Close();
            }
            catch (Exception ex)
            {
                ShowErrorDialog(ex.Message + " Elevated: " + elevated);
            }
        }

        private static string ExpandEnvironmentVariables(string command)
        {
            return environmentVariableRegex.Replace(command, match =>
            {
                string variable = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                return Environment.GetEnvironmentVariable(variable) ?? match.Value;
            });
        }
    }
}
