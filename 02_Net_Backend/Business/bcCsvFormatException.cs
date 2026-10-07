namespace Commerce.Api.Business;

/// <summary>El archivo CSV no cumple el formato esperado. Se traduce a HTTP 400.</summary>
public sealed class bcCsvFormatException : Exception
{
    public bcCsvFormatException(string message) : base(message) { }
    public bcCsvFormatException(string message, Exception inner) : base(message, inner) { }
}
