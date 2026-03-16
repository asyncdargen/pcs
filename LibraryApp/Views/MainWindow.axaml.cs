using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LibraryApp.Data;
using LibraryApp.Helpers;
using LibraryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryApp.Views;

public partial class MainWindow : Window
{
    private List<Book> _allBooks = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadFilters();
        LoadBooks();
    }

    private void LoadFilters()
    {
        using var db = new LibraryDbContext();

        var allAuthorsItem = new Author { Id = 0, FirstName = "Все", LastName = "авторы" };
        var authors = new List<Author> { allAuthorsItem };
        authors.AddRange(db.Authors.OrderBy(a => a.LastName).ToList());
        AuthorFilter.ItemsSource = authors;
        AuthorFilter.SelectedIndex = 0;

        var allGenresItem = new Genre { Id = 0, Name = "Все жанры" };
        var genres = new List<Genre> { allGenresItem };
        genres.AddRange(db.Genres.OrderBy(g => g.Name).ToList());
        GenreFilter.ItemsSource = genres;
        GenreFilter.SelectedIndex = 0;
    }

    private void LoadBooks()
    {
        using var db = new LibraryDbContext();
        _allBooks = db.Books
            .Include(b => b.Author)
            .Include(b => b.Genre)
            .OrderBy(b => b.Title)
            .ToList();

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var search = SearchBox.Text?.Trim().ToLower() ?? string.Empty;
        var selectedAuthor = AuthorFilter.SelectedItem as Author;
        var selectedGenre = GenreFilter.SelectedItem as Genre;

        var filtered = _allBooks.AsEnumerable();

        if (!string.IsNullOrEmpty(search))
            filtered = filtered.Where(b =>
                b.Title.ToLower().Contains(search) ||
                b.ISBN.ToLower().Contains(search) ||
                b.Author.FullName.ToLower().Contains(search));

        if (selectedAuthor != null && selectedAuthor.Id != 0)
            filtered = filtered.Where(b => b.AuthorId == selectedAuthor.Id);

        if (selectedGenre != null && selectedGenre.Id != 0)
            filtered = filtered.Where(b => b.GenreId == selectedGenre.Id);

        var result = filtered.ToList();
        BooksGrid.ItemsSource = result;
        StatusText.Text = $"Показано книг: {result.Count} из {_allBooks.Count}";
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();
    private void Filter_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ResetFilters_Click(object? sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        AuthorFilter.SelectedIndex = 0;
        GenreFilter.SelectedIndex = 0;
    }

    private void BooksGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = BooksGrid.SelectedItem != null;
        EditButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
    }

    private async void AddBook_Click(object? sender, RoutedEventArgs e)
    {
        var window = new BookEditWindow(null);
        if (await window.ShowDialog<bool>(this))
        {
            LoadFilters();
            LoadBooks();
        }
    }

    private async void EditBook_Click(object? sender, RoutedEventArgs e)
    {
        if (BooksGrid.SelectedItem is not Book selected) return;
        var window = new BookEditWindow(selected.Id);
        if (await window.ShowDialog<bool>(this))
        {
            LoadFilters();
            LoadBooks();
        }
    }

    private void BooksGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (BooksGrid.SelectedItem is Book)
            EditBook_Click(sender, e);
    }

    private async void DeleteBook_Click(object? sender, RoutedEventArgs e)
    {
        if (BooksGrid.SelectedItem is not Book selected) return;

        bool confirmed = await MsgBox.ConfirmAsync(this,
            "Подтверждение удаления",
            $"Удалить книгу «{selected.Title}»?");

        if (!confirmed) return;

        using var db = new LibraryDbContext();
        var book = db.Books.Find(selected.Id);
        if (book != null)
        {
            db.Books.Remove(book);
            db.SaveChanges();
        }
        LoadBooks();
    }

    private async void AuthorsButton_Click(object? sender, RoutedEventArgs e)
    {
        await new AuthorsWindow().ShowDialog(this);
        LoadFilters();
        LoadBooks();
    }

    private async void GenresButton_Click(object? sender, RoutedEventArgs e)
    {
        await new GenresWindow().ShowDialog(this);
        LoadFilters();
        LoadBooks();
    }
}
