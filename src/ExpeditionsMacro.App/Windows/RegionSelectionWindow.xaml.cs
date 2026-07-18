using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExpeditionsMacro.Core.Geometry;

namespace ExpeditionsMacro.App.Windows;

public partial class RegionSelectionWindow : Window
{
    private Point? _start;

    public RegionSelectionWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public ScreenRegion? SelectedRegion { get; private set; }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        CaptureMouse();
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelection(_start.Value);
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_start is not null && e.LeftButton == MouseButtonState.Pressed) UpdateSelection(e.GetPosition(this));
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is null) return;
        Point end = e.GetPosition(this);
        Point startScreen = PointToScreen(_start.Value);
        Point endScreen = PointToScreen(end);
        int x = (int)Math.Round(Math.Min(startScreen.X, endScreen.X));
        int y = (int)Math.Round(Math.Min(startScreen.Y, endScreen.Y));
        int width = (int)Math.Round(Math.Abs(endScreen.X - startScreen.X));
        int height = (int)Math.Round(Math.Abs(endScreen.Y - startScreen.Y));
        ReleaseMouseCapture();
        _start = null;
        if (width < 24 || height < 24)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }
        SelectedRegion = new ScreenRegion(x, y, width, height);
        DialogResult = true;
    }

    private void UpdateSelection(Point current)
    {
        Point start = _start ?? current;
        double left = Math.Min(start.X, current.X);
        double top = Math.Min(start.Y, current.Y);
        SelectionBorder.Width = Math.Abs(current.X - start.X);
        SelectionBorder.Height = Math.Abs(current.Y - start.Y);
        Canvas.SetLeft(SelectionBorder, left);
        Canvas.SetTop(SelectionBorder, top);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) DialogResult = false;
    }
}
