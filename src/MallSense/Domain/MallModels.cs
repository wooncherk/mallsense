using System.ComponentModel.DataAnnotations;

namespace MallSense.Domain;

public sealed class Mall
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(160)] public required string Name { get; set; }
    [MaxLength(80)] public string TimeZoneId { get; set; } = "Asia/Kuala_Lumpur";
    [MaxLength(3)] public string CurrencyCode { get; set; } = "MYR";
    [MaxLength(500)] public string? Address { get; set; }
}

public sealed class Floor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MallId { get; set; }
    [MaxLength(80)] public required string Name { get; set; }
    public int Level { get; set; }
    // Optional schematic asset; coordinates below are relative to this floor.
    [MaxLength(500)] public string? MapAssetPath { get; set; }
}

public sealed class NavigationNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FloorId { get; set; }
    [MaxLength(160)] public required string Label { get; set; }
    public NodeKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class NavigationEdge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public double DistanceMetres { get; set; }
    // Two directed rows are used for a two-way walkway.
    public bool IsStepFree { get; set; }
    public bool IsOpen { get; set; } = true;
    [MaxLength(500)] public required string Instruction { get; set; }
}

public sealed class Place
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MallId { get; set; }
    public Guid NavigationNodeId { get; set; }
    public Guid? TenantId { get; set; }
    [MaxLength(160)] public required string Name { get; set; }
    public PlaceKind Kind { get; set; }
    [MaxLength(80)] public required string Category { get; set; }
    [MaxLength(40)] public string? UnitNumber { get; set; }
    [MaxLength(2000)] public required string Description { get; set; }
    public ContentSource Source { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PlaceTag
{
    public Guid PlaceId { get; set; }
    [MaxLength(80)] public required string Value { get; set; }
}

public sealed class OpeningHours
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaceId { get; set; }
    public DayOfWeek Day { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public bool IsClosed { get; set; }
    public bool ClosesNextDay { get; set; }
}

public sealed class MallEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaceId { get; set; }
    [MaxLength(160)] public required string Title { get; set; }
    [MaxLength(2000)] public required string Description { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsPublished { get; set; }
}

// External identity reference only: credentials belong to a future identity provider.
public sealed class StaffMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MallId { get; set; }
    public Guid? TenantId { get; set; }
    [MaxLength(200)] public required string IdentitySubject { get; set; }
    public StaffRole Role { get; set; }
}
