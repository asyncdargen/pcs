using Avalonia.Controls;
using Avalonia.Interactivity;
using LibraryApp.Data;
using LibraryApp.Helpers;
using LibraryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryApp.Views;

public partial class GenresWindow : Window
{
    private int? _editingGenreId;

    public GenresWindow()
    {
        InitializeComponent();
        LoadGenres();
    }

    private void LoadGenres()
    {
        using var db = new LibraryDbContext();
        GenresGrid.ItemsSource = db.Genres
            .Include(g => g.Books)
            .OrderBy(g => g.Name)
            .ToList();
        GenreStatusText.Text = $"Жанров в базе: {db.Genres.Count()}";
    }

    private void GenresGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool has = GenresGrid.SelectedItem != null;
        EditBtn.IsEnabled = has;
        DeleteBtn.IsEnabled = has;
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        _editingGenreId = null;
        ClearFormFields();
        NameBox.Focus();
    }

    private void Edit_Click(object? sender, RoutedEventArgs e)
    {
        if (GenresGrid.SelectedItem is not Genre g) return;
        _editingGenreId = g.Id;
        NameBox.Text = g.Name;
        DescriptionBox.Text = g.Description ?? string.Empty;
        NameBox.Focus();
    }

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (GenresGrid.SelectedItem is not Genre g) return;

        using var db = new LibraryDbContext();
        if (db.Books.Any(b => b.GenreId == g.Id))
        {
            await MsgBox.AlertAsync(this, "Ошибка удаления",
                $"Невозможно удалить жанр «{g.Name}»: есть связанные книги.");
            return;
        }

        bool confirmed = await MsgBox.ConfirmAsync(this, "Подтверждение",
            $"Удалить жанр «{g.Name}»?");
        if (!confirmed) return;

        var entity = db.Genres.Find(g.Id);
        if (entity != null) { db.Genres.Remove(entity); db.SaveChanges(); }
        ClearFormFields();
        LoadGenres();
    }

    private async void SaveGenre_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            await MsgBox.AlertAsync(this, "Ошибка", "Введите название жанра.");
            NameBox.Focus();
            return;
        }

        using var db = new LibraryDbContext();

        if (_editingGenreId.HasValue)
        {
            var genre = db.Genres.Find(_editingGenreId.Value)!;
            genre.Name = NameBox.Text.Trim();
            genre.Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();
        }
        else
        {
            db.Genres.Add(new Genre
            {
                Name = NameBox.Text.Trim(),
                Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim()
            });
        }

        db.SaveChanges();
        ClearFormFields();
        LoadGenres();
    }

    private void ClearForm_Click(object? sender, RoutedEventArgs e) => ClearFormFields();

    private void ClearFormFields()
    {
        _editingGenreId = null;
        NameBox.Text = string.Empty;
        DescriptionBox.Text = string.Empty;
    }
}
