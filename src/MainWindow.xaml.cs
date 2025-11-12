using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace LazyLineReader;

public partial class MainWindow : Window
{
    private bool searching;
    private CancellationTokenSource? ctk;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindowModel ViewModel => (MainWindowModel)DataContext;

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.Dispose();
        ctk?.Dispose();
        ctk = null;

        base.OnClosed(e);
    }

    private void BtnOpenOnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog();

        if (dlg.ShowDialog(this) == true)
        {
            ViewModel.FilePath = dlg.FileName;

            var stream = dlg.OpenFile();

            if (Path.GetExtension(dlg.FileName) == ".gz")
            {
                try
                {
                    var gzStream = new GZipStream(stream, CompressionMode.Decompress);
                    stream = gzStream;
                }
                catch
                {
                }
            }
            ViewModel.Open(stream);
        }
    }

    private void SearchBoxOnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox txb)
            return;

        if (string.IsNullOrEmpty(txb.Text))
        {
            txb.Opacity = 0.5;
            imgSearch.Visibility = Visibility.Visible;
        }
        else
        {
            txb.Opacity = 1;
            imgSearch.Visibility = Visibility.Collapsed;
        }
        ViewModel.Search(null);
    }

    private void SearchBoxOnKeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    searchBox.Focus();
                }
                break;

            case Key.Escape:
                if (!searching)
                {
                    searchBox.Text = string.Empty;
                    ViewModel.Search(null);
                }
                break;

            case Key.F3:
            case Key.Enter:
                if (string.IsNullOrEmpty(searchBox.Text))
                    break;

                if (e.Key == Key.Enter && sender != searchBox)
                    break;

                if (!searching)
                {
                    //searching = true;

                    //if (ctk == null)
                    //    ctk = new CancellationTokenSource();

                    //var mainTask = ViewModel.SearchAsync(searchBox.Text, ctk.Token);
                    //var context = TaskScheduler.FromCurrentSynchronizationContext();

                    //mainTask.ContinueWith(task =>
                    //{
                    //    if (task.Result == null &&
                    //        MessageBox.Show("Read more lines?", this.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    //    {
                    //        return ViewModel.ReadAndSearchAsync(searchBox.Text, ctk.Token);
                    //    }
                    //    return task;

                    //}, context).ContinueWith(task =>
                    //{
                    //    var match = task.Unwrap().Result;

                    //    if (match != null)
                    //    {
                    //        txtBox.Focus();
                    //        txtBox.Select(match.Index, match.Length);
                    //    }
                    //    searching = false;

                    //}, context);

                    //mainTask.Start();

                    searching = true;

                    var match = ViewModel.Search(searchBox.Text);

                    if (match == null &&
                        MessageBox.Show("Read more lines?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        match = ViewModel.ReadAndSearch(searchBox.Text);
                    }

                    if (match != null)
                    {
                        txtBox.Focus();
                        txtBox.Select(match.Index, match.Length);
                    }
                    searching = false;
                }
                break;
        }
    }

    private void OnReadNext4(object sender, RoutedEventArgs e) => ViewModel.ReadNext(4);

    private void OnReadNext20(object sender, RoutedEventArgs e) => ViewModel.ReadNext(20);
}
