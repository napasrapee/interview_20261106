namespace ProjectMonitoring.Application.Features.Reports;

public sealed record ExportFile(byte[] Content, string ContentType, string FileName);
