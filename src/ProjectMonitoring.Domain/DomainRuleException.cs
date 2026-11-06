namespace ProjectMonitoring.Domain;

public sealed class DomainRuleException(string message) : Exception(message);
