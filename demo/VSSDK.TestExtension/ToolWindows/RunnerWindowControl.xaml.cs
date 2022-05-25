﻿using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace TestExtension
{
    public partial class RunnerWindowControl : UserControl, IToolWindowPaneAware
    {
        public RunnerWindowControl(Version vsVersion, RunnerWindowMessenger messenger)
        {
            InitializeComponent();

            lblHeadline.Content = $"Visual Studio v{vsVersion}";

            messenger.MessageReceived += OnMessageReceived;
        }

        private void OnMessageReceived(object sender, string e)
        {
            MessageList.Items.Add(e);
        }

        private void btnShowMessage_Click(object sender, RoutedEventArgs e)
        {
            ShowMessageAsync().FireAndForget();
        }

        private void btnHide_Click(object sender, RoutedEventArgs e)
        {
            HideAsync().FireAndForget();
        }

        private async Task ShowMessageAsync()
        {
            var rating = new RatingPrompt("SteveCadwallader.CodeMaidVS2022", Vsix.Name);
            await rating.PromptAsync();

            await VS.StatusBar.ShowMessageAsync("Test");
            string? text = await VS.StatusBar.GetMessageAsync();
            await VS.StatusBar.ShowMessageAsync(text + " OK");

            Exception ex = new Exception(nameof(TestExtension));
            await ex.LogAsync();

            VSConstants.MessageBoxResult button = await VS.MessageBox.ShowAsync("message", "title");
            Debug.WriteLine(button);
        }

        private async Task HideAsync()
        {
            await RunnerWindow.HideAsync();
        }

        public void SetPane(ToolWindowPane pane)
        {
            MessageList.Items.Add("Pane has been set.");
        }
    }
}