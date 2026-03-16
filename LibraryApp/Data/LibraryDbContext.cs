using LibraryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryApp.Data;

public class LibraryDbContext : DbContext
{
    public DbSet<Book> Books { get; set; }
    public DbSet<Author> Authors { get; set; }
    public DbSet<Genre> Genres { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=library.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.LastName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Country).HasMaxLength(100);
            entity.Ignore(a => a.FullName);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Name).IsRequired().HasMaxLength(100);
            entity.Property(g => g.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Title).IsRequired().HasMaxLength(200);
            entity.Property(b => b.ISBN).HasMaxLength(20);

            entity.HasOne(b => b.Author)
                  .WithMany(a => a.Books)
                  .HasForeignKey(b => b.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Genre)
                  .WithMany(g => g.Books)
                  .HasForeignKey(b => b.GenreId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed data
        modelBuilder.Entity<Author>().HasData(
            new Author { Id = 1, FirstName = "Лев", LastName = "Толстой", BirthDate = new DateOnly(1828, 9, 9), Country = "Россия" },
            new Author { Id = 2, FirstName = "Фёдор", LastName = "Достоевский", BirthDate = new DateOnly(1821, 11, 11), Country = "Россия" },
            new Author { Id = 3, FirstName = "Александр", LastName = "Пушкин", BirthDate = new DateOnly(1799, 6, 6), Country = "Россия" }
        );

        modelBuilder.Entity<Genre>().HasData(
            new Genre { Id = 1, Name = "Роман", Description = "Крупное прозаическое произведение" },
            new Genre { Id = 2, Name = "Поэзия", Description = "Стихотворные произведения" },
            new Genre { Id = 3, Name = "Повесть", Description = "Среднее по объёму прозаическое произведение" }
        );

        modelBuilder.Entity<Book>().HasData(
            new Book { Id = 1, Title = "Война и мир", ISBN = "978-5-17-118603-0", PublishYear = 1869, QuantityInStock = 5, AuthorId = 1, GenreId = 1 },
            new Book { Id = 2, Title = "Анна Каренина", ISBN = "978-5-17-118604-7", PublishYear = 1877, QuantityInStock = 3, AuthorId = 1, GenreId = 1 },
            new Book { Id = 3, Title = "Преступление и наказание", ISBN = "978-5-17-118605-4", PublishYear = 1866, QuantityInStock = 7, AuthorId = 2, GenreId = 1 },
            new Book { Id = 4, Title = "Идиот", ISBN = "978-5-17-118606-1", PublishYear = 1869, QuantityInStock = 2, AuthorId = 2, GenreId = 1 },
            new Book { Id = 5, Title = "Евгений Онегин", ISBN = "978-5-17-118607-8", PublishYear = 1833, QuantityInStock = 10, AuthorId = 3, GenreId = 2 }
        );
    }
}
