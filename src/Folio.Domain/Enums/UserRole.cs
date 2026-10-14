namespace Folio.Domain.Enums;

/// <summary>
/// The role assigned to a <see cref="Entities.User"/>. Authorization checks based on
/// role are an Application-layer concern; the Domain only stores the assignment.
/// </summary>
public enum UserRole
{
    Administrador,
    Contador,
    Consulta,
}
