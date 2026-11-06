namespace ProjectMonitoring.Application.Features.Reports;

public sealed record ExportQuery(string? Format = null, long? DepartmentId = null);
