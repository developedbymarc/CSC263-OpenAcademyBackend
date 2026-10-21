using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Entities.Courses;

public class Category
{
    public int Id { get; set; }
    [Required] [MaxLength(128)] public string Name { get; set; } = null!;
    [Required] [MaxLength(256)] public string Slug { get; set; } = null!;

    public ICollection<Course> Courses { get; } = new List<Course>();
}