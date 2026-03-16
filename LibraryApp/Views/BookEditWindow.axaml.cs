using Avalonia.Controls;
using Avalonia.Interactivity;
using LibraryApp.Data;
using LibraryApp.Helpers;
using LibraryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryApp.Views;

public partial class BookEditWindow : Window
{
    private readonly int? _bookId;

    public BookEditWindow(int? bookId)
    {
        InitializeComponent();
        _bookId = bookId;

        using var db = new LibraryDbContext();
        AuthorBox.ItemsSource = db.Authors.OrderBy(a => a.LastName).ToList();
        GenreBox.ItemsSource = db.Genres.OrderBy(g => g.Name).ToList();

        if (bookId.HasValue)
        {
            TitleLabel.Text = "Редактирование книги";
            var book = db.Books.Include(b => b.Author).Include(b => b.Genre).First(b => b.Id == bookId);
            TitleBox.Text = book.Title;
            AuthorBox.SelectedItem = ((List<Author>)AuthorBox.ItemsSource!).FirstOrDefault(a => a.Id == book.AuthorId);
            GenreBox.SelectedItem = ((List<Genre>)GenreBox.ItemsSource!).FirstOrDefault(g => g.Id == book.GenreId);
            YearBox.Text = book.PublishYear.ToString();
            IsbnBox.Text = book.ISBN;
            QuantityBox.Text = book.QuantityInStock.ToString();
        }
        else
        {
            YearBox.Text = DateTime.Now.Year.ToString();
            QuantityBox.Text = "1";
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            await MsgBox.AlertAsync(this, "Ошибка", "Введите название книги.");
            TitleBox.Focus();
            return;
        }
        if (AuthorBox.SelectedItem is not Author selectedAuthor)
        {
            await MsgBox.AlertAsync(this, "Ошибка", "Выберите автора.");
            return;
        }
        if (GenreBox.SelectedItem is not Genre selectedGenre)
        {
            await MsgBox.AlertAsync(this, "Ошибка", "Выберите жанр.");
            return;
        }
        if (!int.TryParse(YearBox.Text, out int year) || year < 1 || year > DateTime.Now.Year + 1)
        {
            await MsgBox.AlertAsync(this, "Ошибка", "Введите корректный год издания.");
            YearBox.Focus();
            return;
        }
        if (!int.TryParse(QuantityBox.Text, out int quantity) || quantity < 0)
        {
            await MsgBox.AlertAsync(this, "Ошибка", "Количество должно быть неотрицательным числом.");
            QuantityBox.Focus();
            return;
        }

        using var db = new LibraryDbContext();

        if (_bookId.HasValue)
        {
            var book = db.Books.Find(_bookId.Value)!;
            book.Title = TitleBox.Text.Trim();
            book.AuthorId = selectedAuthor.Id;
            book.GenreId = selectedGenre.Id;
            book.PublishYear = year;
            book.ISBN = IsbnBox.Text?.Trim() ?? string.Empty;
            book.QuantityInStock = quantity;
        }
        else
        {
            db.Books.Add(new Book
            {
                Title = TitleBox.Text.Trim(),
                AuthorId = selectedAuthor.Id,
                GenreId = selectedGenre.Id,
                PublishYear = year,
                ISBN = IsbnBox.Text?.Trim() ?? string.Empty,
                QuantityInStock = quantity
            });
        }

        db.SaveChanges();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
