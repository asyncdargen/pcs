using Avalonia.Controls;
using Avalonia.Interactivity;
using LibraryApp.Data;
using LibraryApp.Helpers;
using LibraryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryApp.Views;

public partial class AuthorsWindow : Window
{
    private int? _editingAuthorId;

    public AuthorsWindow()
    {
        InitializeComponent();
        LoadAuthors();
    }

    private void LoadAuthors()
    {
        using var db = new LibraryDbContext();
        AuthorsGrid.ItemsSource = db.Authors
            .Include(a => a.Books)
            .OrderBy(a => a.LastName)
            .ToList();
        AuthorStatusText.Text = $"Авторов в базе: {db.Authors.Count()}";
    }

    private void AuthorsGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool has = AuthorsGrid.SelectedItem != null;
        EditBtn.IsEnabled = has;
        DeleteBtn.IsEnabled = has;
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        _editingAuthorId = null;
        ClearFormFields();
        LastNameBox.Focus();
    }

    private void Edit_Click(object? sender, RoutedEventArgs e)
    {
        if (AuthorsGrid.SelectedItem is not Author a) return;
        _editingAuthorId = a.Id;
        LastNameBox.Text = a.LastName;
        FirstNameBox.Text = a.FirstName;
        BirthDateBox.Text = a.BirthDate?.ToString("dd.MM.yyyy") ?? string.Empty;
        CountryBox.Text = a.Country;
        LastNameBox.Focus();
    }

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (AuthorsGrid.SelectedItem is not Author a) return;

        using var db = new LibraryDbContext();
        if (db.Books.Any(b => b.AuthorId == a.Id))
        {
            await MsgBox.AlertAsync(this, "Ошибка удаления",
                $"Невозможно удалить автора «{a.FullName}»: есть связанные книги.");
            return;
        }

        bool confirmed = await MsgBox.ConfirmAsync(this, "Подтверждение",
            $"Удалить автора «{a.FullName}»?");
        if (!confirmed) return;

        var entity = db.Authors.Find(a.Id);
        if (entity != null) { db.Authors.Remove(entity); db.SaveChanges(); }
        ClearFormFields();
        LoadAuthors();
    }

    private async void SaveAuthor_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LastNameBox.Text) || string.IsNullOrWhiteSpace(FirstNameBox.Text))
        {
            await MsgBox.AlertAsync(this, "Ошибка", "Введите фамилию и имя автора.");
            return;
        }

        DateOnly? birthDate = null;
        if (!string.IsNullOrWhiteSpace(BirthDateBox.Text))
        {
            if (!DateOnly.TryParseExact(BirthDateBox.Text, "dd.MM.yyyy", out var parsed))
            {
                await MsgBox.AlertAsync(this, "Ошибка", "Неверный формат даты. Используйте ДД.ММ.ГГГГ.");
                return;
            }
            birthDate = parsed;
        }

        using var db = new LibraryDbContext();

        if (_editingAuthorId.HasValue)
        {
            var author = db.Authors.Find(_editingAuthorId.Value)!;
            author.LastName = LastNameBox.Text.Trim();
            author.FirstName = FirstNameBox.Text.Trim();
            author.BirthDate = birthDate;
            author.Country = CountryBox.Text?.Trim() ?? string.Empty;
        }
        else
        {
            db.Authors.Add(new Author
            {
                LastName = LastNameBox.Text.Trim(),
                FirstName = FirstNameBox.Text.Trim(),
                BirthDate = birthDate,
                Country = CountryBox.Text?.Trim() ?? string.Empty
            });
        }

        db.SaveChanges();
        ClearFormFields();
        LoadAuthors();
    }

    private void ClearForm_Click(object? sender, RoutedEventArgs e) => ClearFormFields();

    private void ClearFormFields()
    {
        _editingAuthorId = null;
        LastNameBox.Text = string.Empty;
        FirstNameBox.Text = string.Empty;
        BirthDateBox.Text = string.Empty;
        CountryBox.Text = string.Empty;
    }
}
