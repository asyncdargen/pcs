using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LibraryApp.Helpers;

public static class MsgBox
{
    public static Task AlertAsync(Window owner, string title, string message)
        => ShowAsync(owner, title, message, false);

    public static Task<bool> ConfirmAsync(Window owner, string title, string message)
        => ShowAsync(owner, title, message, true);

    private static async Task<bool> ShowAsync(Window owner, string title, string message, bool yesNo)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        var okBtn = new Button { Content = yesNo ? "Да" : "OK", Width = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        okBtn.Classes.Add("primary");
        okBtn.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };

        StackPanel buttons;
        if (yesNo)
        {
            var noBtn = new Button { Content = "Нет", Width = 90, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            noBtn.Classes.Add("secondary");
            noBtn.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
            buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(okBtn);
            buttons.Children.Add(noBtn);
        }
        else
        {
            buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(okBtn);
        }

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                },
                buttons
            }
        };

        dialog.Content = content;
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}
