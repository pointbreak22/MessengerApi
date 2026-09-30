namespace Domain.Enums
{
    /// <summary>
    /// Stored as an int column, so the numeric values are part of the database
    /// contract — append new roles, never renumber existing ones.
    /// </summary>
    public enum UserRole
    {
        User = 0,
        SuperAdmin = 1
    }
}
