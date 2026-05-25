namespace DZ_HealthChecks.DTOs;

public record ProductCreateDto(string Name, string Description, decimal Price, int Stock);

public record ProductUpdateDto(string Name, string Description, decimal Price, int Stock);
