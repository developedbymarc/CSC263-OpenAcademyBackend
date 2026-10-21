namespace OpenAcademy.Entities;

/// <summary>
/// Stamped automatically by <c>ApplicationDbContext.SaveChanges</c>:
/// CreatedAt/UpdatedAt on insert, UpdatedAt on every update.
/// </summary>
public interface ITimestamped
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}